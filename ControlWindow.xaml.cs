using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace rpgFogOfWar
{
    public partial class ControlWindow : Window
    {
        private AudienceWindow? audience;
        private float currentShapeSize = 120f;
        private float currentShapeRotation = 0f;
        public SKBitmap? currentImage;
        public SKMatrix transform = SKMatrix.CreateIdentity();
        public SKBitmap? fogMask;
        public bool fogRevealed = false;
        private List<Marker> markers = new List<Marker>();
        private List<ShapeOverlay> shapes = new List<ShapeOverlay>();
        private ShapeOverlay? previewShape = null;
        private bool isDrawingShape = false;
        private SKPoint shapeStart;
        private ShapeType currentShapeType = ShapeType.Circle;
        private bool isRevealing = false;
        private float revealRadius = 80f;
        private SKPoint mirrorMousePos = new SKPoint(0, 0);
        private System.Windows.Point? lastPanPoint;
        private Stack<object> undoStack = new Stack<object>();

        public ControlWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            audience = new AudienceWindow();
            audience.Control = this;

            var screens = System.Windows.Forms.Screen.AllScreens;

            if (screens.Length > 0)
            {
                var ctrlScreen = screens[0];
                Left = ctrlScreen.Bounds.Left + 100;
                Top = ctrlScreen.Bounds.Top + 100;
            }

            if (screens.Length > 1)
            {
                var audScreen = screens[1];
                audience.Left = audScreen.Bounds.Left;
                audience.Top = audScreen.Bounds.Top;
                audience.Width = audScreen.Bounds.Width;
                audience.Height = audScreen.Bounds.Height;
                audience.WindowState = WindowState.Normal;
                audience.Show();
                audience.WindowState = WindowState.Maximized;
            }
            else
            {
                audience.WindowState = WindowState.Maximized;
                audience.Show();
            }

            this.Activate();
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e) => LoadImage();

        private void LoadImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                currentImage = SKBitmap.Decode(dlg.FileName);
                fogRevealed = false;
                CreateFogMask();
                transform = SKMatrix.CreateIdentity();
                markers.Clear();
                shapes.Clear();
                undoStack.Clear();
                InvalidateAll();
            }
        }

        private void CreateFogMask()
        {
            if (currentImage == null) return;
            fogMask = new SKBitmap(currentImage.Width, currentImage.Height);
            using var canvas = new SKCanvas(fogMask);
            canvas.Clear(new SKColor(0, 0, 0, 180));
        }

        private void SkControl_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            if (currentImage == null) return;

            canvas.SetMatrix(transform);
            canvas.DrawBitmap(currentImage, 0, 0);

            if (!fogRevealed && fogMask != null)
                canvas.DrawBitmap(fogMask, 0, 0);

            DrawOverlays(canvas);

            // Mirror mouse indicator - now correctly transformed
            canvas.DrawCircle(mirrorMousePos, 15, new SKPaint
            {
                Color = SKColors.Yellow,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4,
                IsAntialias = true
            });
        }

        public void DrawOverlays(SKCanvas canvas)
        {
            foreach (var m in markers) m.Draw(canvas);
            foreach (var s in shapes) s.Draw(canvas);
            if (previewShape != null) previewShape.Draw(canvas);
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F) LoadImage();
            if (e.Key == Key.R)
            {
                fogRevealed = false;
                CreateFogMask();
                InvalidateAll();
            }
            if (e.Key == Key.Z && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                if (undoStack.Count > 0)
                {
                    var last = undoStack.Pop();
                    if (last is Marker m) markers.Remove(m);
                    if (last is ShapeOverlay s) shapes.Remove(s);
                    InvalidateAll();
                }
            }
        }

        private void SkControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            var skPos = new SKPoint((float)pos.X, (float)pos.Y);

            if (e.ChangedButton == MouseButton.Left)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    lastPanPoint = pos;
                    return;
                }

                if (!fogRevealed)
                {
                    isRevealing = true;
                    RevealAtPoint(new SKPoint((float)pos.X, (float)pos.Y));
                    return;
                }

                // Place shape
                isDrawingShape = true;
                shapeStart = mirrorMousePos; // Not really used anymore
            }

            else if (e.ChangedButton == MouseButton.Right)
            {
                pos = e.GetPosition(skControl);
                var screenPoint = new SKPoint((float)pos.X, (float)pos.Y);

                // Normal Right Click = Place Marker
                var inverse = transform.Invert();
                var worldPoint = inverse.MapPoint(screenPoint);

                // Check for Delete first (Shift + Right Click)
                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    inverse = transform.Invert();
                    worldPoint = inverse.MapPoint(screenPoint);

                    var toDelete = markers.FirstOrDefault(m => m.HitTest(worldPoint));
                    if (toDelete != null)
                    {
                        markers.Remove(toDelete);
                        InvalidateAll();
                        return;                    // Important: exit early
                    }
                }



                var (text, color) = GetSelectedCondition();
                var size = GetSelectedMarkerSize();

                var marker = new Marker(worldPoint, size, text, color);
                markers.Add(marker);
                undoStack.Push(marker);

                InvalidateAll();
            }
        }

        private void SkControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            var screenPoint = new SKPoint((float)pos.X, (float)pos.Y);

            var inverse = transform.Invert();
            mirrorMousePos = inverse.MapPoint(screenPoint);

            if (isRevealing && e.LeftButton == MouseButtonState.Pressed)
                RevealAtPoint(screenPoint);

            // Panning
            if (Keyboard.IsKeyDown(Key.LeftShift) && e.LeftButton == MouseButtonState.Pressed && lastPanPoint.HasValue)
            {
                var deltaX = (float)(pos.X - lastPanPoint.Value.X);
                var deltaY = (float)(pos.Y - lastPanPoint.Value.Y);
                transform = transform.PreConcat(SKMatrix.CreateTranslation(deltaX, deltaY));
                lastPanPoint = pos;
                InvalidateAll();
                return;
            }

            // Live Shape Preview
            if (isDrawingShape)
            {
                pos = e.GetPosition(skControl);
                screenPoint = new SKPoint((float)pos.X, (float)pos.Y);

                inverse = transform.Invert();
                var worldPoint = inverse.MapPoint(screenPoint);

                previewShape = new ShapeOverlay(currentShapeType, worldPoint, currentShapeSize, currentShapeRotation);
                InvalidateAll();
            }
        }

        private void SkControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isRevealing = false;
                lastPanPoint = null;

                if (isDrawingShape)
                {
                    var finalShape = new ShapeOverlay(currentShapeType, mirrorMousePos, currentShapeSize, currentShapeRotation);
                    shapes.Add(finalShape);
                    undoStack.Push(finalShape);

                    // Reset rotation for next shape
                    currentShapeRotation = 0f;
                    isDrawingShape = false;
                    previewShape = null;
                    InvalidateAll();
                }
            }
        }

        private void SkControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            var point = new SKPoint((float)pos.X, (float)pos.Y);

            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                // Rotate shape
                currentShapeRotation += e.Delta > 0 ? 15f : -15f;
            }
            else
            {
                // Resize shape or zoom map
                if (isDrawingShape)
                {
                    currentShapeSize *= e.Delta > 0 ? 1.15f : 0.85f;
                    currentShapeSize = Math.Max(30f, currentShapeSize);
                }
                else
                {
                    // Normal map zoom
                    float zoom = e.Delta > 0 ? 1.1f : 0.9f;
                    transform = transform.PreConcat(SKMatrix.CreateScale(zoom, zoom, point.X, point.Y));
                }
            }

            InvalidateAll();
        }

        private void RevealAtPoint(SKPoint screenPoint)
        {
            if (fogMask == null || currentImage == null) return;

            var inverse = transform.Invert();
            var imagePoint = inverse.MapPoint(screenPoint);

            using var canvas = new SKCanvas(fogMask);
            var erasePaint = new SKPaint
            {
                Color = SKColors.Transparent,
                BlendMode = SKBlendMode.Clear,
                IsAntialias = true
            };

            canvas.DrawCircle(imagePoint, revealRadius, erasePaint);
            InvalidateAll();
        }

        private (string text, SKColor color) GetSelectedCondition()
        {
            var selected = ConditionCombo.SelectedItem as ComboBoxItem;
            string name = selected?.Content?.ToString() ?? "CONDITION";

            return name switch
            {
                "Poisoned" => ("POISONED", SKColors.LimeGreen),
                "Blinded" => ("BLINDED", SKColors.DarkGray),
                "Restrained" => ("RESTRAINED", SKColors.OrangeRed),
                "Prone" => ("PRONE", SKColors.Purple),
                "Paralyzed" => ("PARALYZED", SKColors.MediumPurple),
                "Stunned" => ("STUNNED", SKColors.Yellow),
                "Charmed" => ("CHARMED", SKColors.Pink),
                "Frightened" => ("FRIGHTENED", SKColors.Orange),
                "Incapacitated" => ("INCAPACITATED", SKColors.LightBlue),
                _ => ("CONDITION", SKColors.Red)
            };
        }

        private double GetSelectedMarkerSize()
        {
            if (rbLarge.IsChecked == true) return 1.5;
            if (rbMedium.IsChecked == true) return 1.0;
            return 0.6;
        }

        private void ShapeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update current shape type
            currentShapeType = ShapeCombo.SelectedIndex switch
            {
                1 => ShapeType.Square,
                2 => ShapeType.Rectangle,
                3 => ShapeType.Cone,
                _ => ShapeType.Circle
            };

            // Only activate preview after everything is loaded
            if (skControl != null)
            {
                isDrawingShape = true;
                InvalidateAll();
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            markers.Clear();
            shapes.Clear();
            undoStack.Clear();
            InvalidateAll();
        }

        private void InvalidateAll()
        {
            skControl?.InvalidateVisual();
            audience?.skAudience?.InvalidateVisual();
        }

        protected override void OnClosed(EventArgs e)
        {
            audience?.Close();
            base.OnClosed(e);
        }
    }
}