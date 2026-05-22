using SkiaSharp;
using SkiaSharp.Views.Desktop;        // For SKPaintSurfaceEventArgs
using System.Windows;
using System.Windows.Input;

namespace rpgFogOfWar
{
    public partial class ControlWindow : Window
    {
        private AudienceWindow? audience;
        public SKBitmap? currentImage;     // ← Add
        public SKMatrix transform = SKMatrix.CreateIdentity();

        public SKBitmap? fogMask;           // 20% grey mask
        public bool fogRevealed = false;
        private Stack<object> undoStack = new Stack<object>();
        private ShapeOverlay? previewShape = null;
        private System.Windows.Point? lastPanPoint;

        // Overlays
        private List<Marker> markers = new List<Marker>();
        private List<ShapeOverlay> shapes = new List<ShapeOverlay>();
        private bool isRevealing = false;
        private float revealRadius = 120f;   // adjustable reveal size
        private ShapeType currentShapeType = ShapeType.Circle;
        private bool isDrawingShape = false;
        private SKPoint shapeStart;

        private SKPoint mirrorMousePos = new SKPoint(0, 0);

        public ControlWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            audience = new AudienceWindow();
            audience.Control = this;

            var screens = System.Windows.Forms.Screen.AllScreens;

            // Position Control window on Monitor 0
            if (screens.Length > 0)
            {
                var ctrlScreen = screens[0];
                Left = ctrlScreen.Bounds.Left + 100;
                Top = ctrlScreen.Bounds.Top + 100;
            }

            // === FIXED AUDIENCE WINDOW SETUP ===
            if (screens.Length > 1)
            {
                var audScreen = screens[1];
                audience.Left = audScreen.Bounds.Left;
                audience.Top = audScreen.Bounds.Top;
                audience.Width = audScreen.Bounds.Width;
                audience.Height = audScreen.Bounds.Height;

                audience.WindowState = WindowState.Normal;   // Important: Normal first!
                audience.ShowActivated = false;
                audience.Topmost = true;
                audience.Show();

                // Maximize AFTER showing
                audience.WindowState = WindowState.Maximized;
            }
            else
            {
                // Fallback for single monitor
                audience.WindowState = WindowState.Maximized;
                audience.ShowActivated = false;
                audience.Topmost = true;
                audience.Show();
            }

            this.Activate();        // Bring Control window to front
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            LoadImage();
        }

        private void CreateFogMask()
        {
            if (currentImage == null) return;

            fogMask = new SKBitmap(currentImage.Width, currentImage.Height);
            using var canvas = new SKCanvas(fogMask);
            canvas.Clear(new SKColor(0, 0, 0, 180)); // 20% grey
        }

        private void SkControl_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            if (currentImage == null) return;

            canvas.SetMatrix(transform);

            // Draw base image first
            canvas.DrawBitmap(currentImage, 0, 0);

            // Draw semi-transparent fog mask on top
            if (!fogRevealed && fogMask != null)
            {
                var maskPaint = new SKPaint
                {
                    BlendMode = SKBlendMode.Darken   // Makes it look more like fog
                };
                canvas.DrawBitmap(fogMask, 0, 0, maskPaint);
            }

            DrawOverlays(canvas);

            // Mirror mouse indicator
            canvas.DrawCircle(mirrorMousePos, 15, new SKPaint
            {
                Color = SKColors.Yellow,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4
            });

            if (previewShape != null)
                previewShape.Draw(canvas);
        }

        private void SkAudience_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            if (currentImage == null) return;

            canvas.SetMatrix(transform);

            canvas.DrawBitmap(currentImage, 0, 0);

            if (fogRevealed)
                DrawOverlays(canvas);
        }

        public void DrawOverlays(SKCanvas canvas)
        {
            var paint = new SKPaint { IsAntialias = true };

            // Markers
            foreach (var m in markers)
            {
                m.Draw(canvas);
            }

            // Shapes
            foreach (var s in shapes)
            {
                s.Draw(canvas);
            }
        }
        private void LoadImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dlg.ShowDialog() == true)
            {
                currentImage = SKBitmap.Decode(dlg.FileName);
                fogRevealed = false;
                CreateFogMask();
                transform = SKMatrix.CreateIdentity();
                InvalidateAll();
            }
        }

        // ====================== INPUT ======================

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F)
            {
                LoadImage();
            }
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
                if (!fogRevealed)
                {
                    isRevealing = true;
                    RevealAtPoint(skPos);
                }
                else
                {
                    // Start drawing shape if already revealed
                    isDrawingShape = true;
                    shapeStart = skPos;
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                var size = GetSelectedMarkerSize();
                var marker = new Marker(skPos, size);
                markers.Add(marker);
                InvalidateAll();
            }
        }

        private void SkControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            mirrorMousePos = new SKPoint((float)pos.X, (float)pos.Y);

            if (isRevealing && e.LeftButton == MouseButtonState.Pressed)
            {
                RevealAtPoint(mirrorMousePos);
            }

            if (isDrawingShape)
            {
                previewShape = new ShapeOverlay(currentShapeType, shapeStart, mirrorMousePos);
                InvalidateAll();
            }

            // SHIFT + Right drag to delete marker
            if (e.RightButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.LeftShift))
            {
                var skPos = new SKPoint((float)pos.X, (float)pos.Y);
                var toDelete = markers.FirstOrDefault(m => m.HitTest(skPos));
                if (toDelete != null)
                {
                    markers.Remove(toDelete);
                    InvalidateAll();
                }
            }
        }

        private void SkControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isRevealing = false;
                if (isDrawingShape)
                {
                    var end = new SKPoint((float)e.GetPosition(skControl).X, (float)e.GetPosition(skControl).Y);
                    shapes.Add(new ShapeOverlay(currentShapeType, shapeStart, end));
                    isDrawingShape = false;
                    previewShape = null;
                    InvalidateAll();
                }
            }
        }

        private void SkControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            float zoom = e.Delta > 0 ? 1.1f : 0.9f;

            var point = new SKPoint((float)pos.X, (float)pos.Y);
            transform = transform.PreConcat(SKMatrix.CreateScale(zoom, zoom, point.X, point.Y));

            InvalidateAll();
        }

        private double GetSelectedMarkerSize()
        {
            if (rbLarge.IsChecked == true) return 1.5;
            if (rbMedium.IsChecked == true) return 1.0;
            return 0.5;
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            markers.Clear();
            shapes.Clear();
            InvalidateAll();
        }

        private void InvalidateAll()
        {
            skControl.InvalidateVisual();
            if (audience?.skAudience != null)
            {
                audience.skAudience.InvalidateVisual();
            }
        }

        private void RevealAtPoint(SKPoint point)
        {
            if (fogMask == null || currentImage == null) return;

            using var canvas = new SKCanvas(fogMask);
            var erasePaint = new SKPaint
            {
                Color = SKColors.Transparent,
                BlendMode = SKBlendMode.Clear,   // This erases the mask
                IsAntialias = true
            };

            // Convert screen point to image space (important for zoomed/panned maps)
            var inverse = transform.Invert();
            var imagePoint = inverse.MapPoint(point);

            canvas.DrawCircle(imagePoint, revealRadius, erasePaint);

            InvalidateAll();
        }

        protected override void OnClosed(EventArgs e)
        {
            audience?.Close();
            base.OnClosed(e);
        }
    }
}