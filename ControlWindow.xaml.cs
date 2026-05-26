using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace rpgFogOfWar
{
    public partial class ControlWindow : Window
    {
        private AudienceWindow? audience;
        private bool shapeModeActive = false;   // New flag
        public SKBitmap? currentImage;
        private string? currentImagePath;
        public SKMatrix transform = SKMatrix.CreateIdentity();
        public SKBitmap? fogMask;
        public bool fogRevealed = false;
        private List<Marker> markers = new List<Marker>();
        private List<ShapeOverlay> shapes = new List<ShapeOverlay>();
        private ShapeOverlay? previewShape = null;
        private bool isDrawingShape = false;
        private bool isRevealing = false;
        private float revealRadius = 60f;
        public SKPoint mirrorMousePos = new SKPoint(0, 0);
        private System.Windows.Point? lastPanPoint;
        private bool isFullyLoaded = false;
        private Stack<object> undoStack = new Stack<object>();
        private ShapeType currentShapeType = ShapeType.Circle;
        private float currentShapeSize = 120f;
        private float currentShapeRotation = 0f;
        private System.Windows.Threading.DispatcherTimer? animationTimer;
        private int currentFrameIndex = 0;
        private List<SKBitmap>? gifFrames;
        private List<int>? frameDurations;   // in milliseconds


        public ControlWindow()
        {
            Debug.WriteLine("ControlWindow Constructor called");
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
            isFullyLoaded = true;
            Debug.WriteLine("Window fully loaded - shape selection enabled");

        }

        private void LoadImage_Click(object sender, RoutedEventArgs e) => LoadImage();

        private void LoadImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images & Animated GIFs|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };

            if (dlg.ShowDialog() == true)
            {
                currentImagePath = dlg.FileName;
                LoadImageFile(dlg.FileName);
            }
        }

        private void LoadImageFile(string filePath)
        {
            // Stop any existing animation
            animationTimer?.Stop();

            gifFrames?.ForEach(f => f.Dispose());
            gifFrames = null;
            frameDurations = null;
            currentFrameIndex = 0;

            if (filePath.ToLower().EndsWith(".gif"))
            {
                LoadAnimatedGif(filePath);
            }
            else
            {
                currentImage = SKBitmap.Decode(filePath);
                gifFrames = null;
            }

            fogRevealed = false;
            CreateFogMask();
            transform = SKMatrix.CreateIdentity();
            markers.Clear();
            shapes.Clear();
            undoStack.Clear();

            StartAnimationIfNeeded();
            InvalidateAll();
        }

        private void LoadAnimatedGif(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var managedStream = new SKManagedStream(stream);
            using var codec = SKCodec.Create(managedStream);

            if (codec == null)
            {
                System.Windows.MessageBox.Show("Failed to load GIF", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            gifFrames = new List<SKBitmap>();
            frameDurations = new List<int>();

            for (int i = 0; i < codec.FrameCount; i++)
            {
                var bitmap = new SKBitmap(codec.Info.Width, codec.Info.Height);
                var options = new SKCodecOptions(i);

                codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);

                gifFrames.Add(bitmap);
                frameDurations.Add(codec.FrameInfo[i].Duration > 0 ? codec.FrameInfo[i].Duration : 100);
            }

            currentImage = gifFrames.FirstOrDefault();
            Debug.WriteLine($"Loaded animated GIF with {gifFrames.Count} frames");
        }


        private void StartAnimationIfNeeded()
        {
            if (gifFrames == null || gifFrames.Count <= 1)
                return;

            animationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(frameDurations?[currentFrameIndex] ?? 100)
            };

            animationTimer.Tick += (s, e) =>
            {
                currentFrameIndex = (currentFrameIndex + 1) % gifFrames.Count;
                currentImage = gifFrames[currentFrameIndex];

                if (animationTimer != null)
                    animationTimer.Interval = TimeSpan.FromMilliseconds(frameDurations?[currentFrameIndex] ?? 100);

                InvalidateAll();
            };

            animationTimer.Start();
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

            canvas.DrawCircle(mirrorMousePos, 10, new SKPaint
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
            if (e.Key == Key.S && Keyboard.IsKeyDown(Key.LeftCtrl))
                SaveSession();
            if (e.Key == Key.O && Keyboard.IsKeyDown(Key.LeftCtrl))
                LoadSession();
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
            var screenPoint = new SKPoint((float)pos.X, (float)pos.Y);
            var inverse = transform.Invert();
            var worldPoint = inverse.MapPoint(screenPoint);

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

                // Only start drawing shape if shape mode is active
                if (shapeModeActive)
                {
                    isDrawingShape = true;
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    var toDelete = markers.FirstOrDefault(m => m.HitTest(worldPoint));
                    if (toDelete != null)
                    {
                        markers.Remove(toDelete);
                        InvalidateAll();
                        return;
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

            if (Keyboard.IsKeyDown(Key.LeftShift) && e.LeftButton == MouseButtonState.Pressed && lastPanPoint.HasValue)
            {
                var deltaX = (float)(pos.X - lastPanPoint.Value.X);
                var deltaY = (float)(pos.Y - lastPanPoint.Value.Y);
                transform = transform.PreConcat(SKMatrix.CreateTranslation(deltaX, deltaY));
                lastPanPoint = pos;
                InvalidateAll();
                return;
            }

            if (isDrawingShape)
            {
                previewShape = new ShapeOverlay(currentShapeType, mirrorMousePos, currentShapeSize, currentShapeRotation);
                InvalidateAll();
            }
        }

        private void SkControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isRevealing = false;
                lastPanPoint = null;

                if (isDrawingShape && shapeModeActive)
                {
                    var finalShape = new ShapeOverlay(currentShapeType, mirrorMousePos, currentShapeSize, currentShapeRotation);
                    shapes.Add(finalShape);
                    undoStack.Push(finalShape);

                    // Do NOT reset shapeModeActive - user can keep placing same shape
                    isDrawingShape = true;           // Stay in preview mode
                    previewShape = null;
                    InvalidateAll();
                }
            }
        }

        private void SkControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            var point = new SKPoint((float)pos.X, (float)pos.Y);

            if (isDrawingShape)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    currentShapeRotation += e.Delta > 0 ? 15f : -15f;
                }
                else
                {
                    currentShapeSize *= e.Delta > 0 ? 1.12f : 0.88f;
                    currentShapeSize = Math.Clamp(currentShapeSize, 30f, 800f);
                }

                previewShape = new ShapeOverlay(currentShapeType, mirrorMousePos, currentShapeSize, currentShapeRotation);
                InvalidateAll();
            }
            else
            {
                float zoom = e.Delta > 0 ? 1.1f : 0.9f;
                transform = transform.PreConcat(SKMatrix.CreateScale(zoom, zoom, point.X, point.Y));
                InvalidateAll();
            }
        }

        private void RevealAtPoint(SKPoint screenPoint)
        {
            if (fogMask == null || currentImage == null) return;

            var inverse = transform.Invert();
            var imagePoint = inverse.MapPoint(screenPoint);

            using var canvas = new SKCanvas(fogMask);
            var erasePaint = new SKPaint { Color = SKColors.Transparent, BlendMode = SKBlendMode.Clear, IsAntialias = true };
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

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            markers.Clear();
            shapes.Clear();
            undoStack.Clear();
            InvalidateAll();
        }

        private void ShapeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isFullyLoaded)
                return;
            currentShapeType = ShapeCombo.SelectedIndex switch
            {
                2 => ShapeType.Square,
                3 => ShapeType.Rectangle,
                4 => ShapeType.Cone,
                _ => ShapeType.Circle
            };
            Debug.WriteLine($"ShapeCombo_SelectionChanged: currentShapeType set to {currentShapeType}");
            shapeModeActive = true;
            isDrawingShape = true;
            currentShapeSize = 120f;
            currentShapeRotation = 0f;

            InvalidateAll();
        }

        private void SaveSession_Click(object sender, RoutedEventArgs e) => SaveSession();

        private void SaveSession()
        {
            if (currentImage == null)
            {
                System.Windows.MessageBox.Show("No map loaded to save.", "Save Session", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Map Session|*.mapSes",
                DefaultExt = "mapSes"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var session = new SessionData
                    {
                        ImagePath = currentImagePath,
                        FogRevealed = fogRevealed,
                        TranslateX = transform.TransX,
                        TranslateY = transform.TransY,
                        Scale = transform.ScaleX
                    };

                    // Save Markers
                    foreach (var m in markers)
                    {
                        session.Markers.Add(new MarkerData
                        {
                            X = m.Center.X,
                            Y = m.Center.Y,
                            SizeMultiplier = m.SizeMultiplier,
                            Text = m.Text,
                            ColorHex = m.Color.ToString()
                        });
                    }

                    // Save Shapes
                    foreach (var s in shapes)
                    {
                        session.Shapes.Add(new ShapeData
                        {
                            Type = s.Type.ToString(),
                            CenterX = s.Center.X,
                            CenterY = s.Center.Y,
                            Size = s.Size,
                            Rotation = s.Rotation
                        });
                    }

                    string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);

                    System.Windows.MessageBox.Show("Session saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Failed to save session:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadSession_Click(object sender, RoutedEventArgs e) => LoadSession();

        private void LoadSession()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Map Session|*.mapSes"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var session = JsonSerializer.Deserialize<SessionData>(json);

                    if (session?.ImagePath != null && File.Exists(session.ImagePath))
                    {
                        currentImagePath = session.ImagePath;
                        currentImage = SKBitmap.Decode(session.ImagePath);
                        CreateFogMask();
                        fogRevealed = session.FogRevealed;

                        // Restore transform
                        transform = SKMatrix.CreateTranslation(session.TranslateX, session.TranslateY);
                        transform = transform.PreConcat(SKMatrix.CreateScale(session.Scale, session.Scale));

                        // Load Markers
                        markers.Clear();
                        foreach (var md in session.Markers)
                        {
                            var color = SKColor.Parse(md.ColorHex);
                            markers.Add(new Marker(new SKPoint(md.X, md.Y), md.SizeMultiplier, md.Text, color));
                        }

                        // Load Shapes
                        shapes.Clear();
                        foreach (var sd in session.Shapes)
                        {
                            if (Enum.TryParse(sd.Type, out ShapeType type))
                            {
                                shapes.Add(new ShapeOverlay(type, new SKPoint(sd.CenterX, sd.CenterY), sd.Size, sd.Rotation));
                            }
                        }

                        InvalidateAll();
                        System.Windows.MessageBox.Show("Session loaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Failed to load session:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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