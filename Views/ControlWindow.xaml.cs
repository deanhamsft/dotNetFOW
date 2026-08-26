using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using rpgFogOfWar.Services;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace rpgFogOfWar
{
    public partial class ControlWindow : Window
    {
        private enum GestureKind { None, Reveal, Cover, Pan, PlaceShape }

        private readonly MapDocument _doc = new();
        private readonly GifAnimator _gif = new();
        private readonly SKPaint _pointerPaint = new()
        {
            Color = SKColors.Yellow,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            IsAntialias = true
        };

        private AudienceWindow? _audience;
        private bool _shapeModeActive;
        private ShapeOverlay? _previewShape;
        private GestureKind _gesture;
        private float _revealRadius = 60f;
        private System.Windows.Point? _lastPanPoint;
        private bool _isFullyLoaded;
        private ShapeType _currentShapeType = ShapeType.Circle;
        private float _currentShapeSize = 120f;
        private float _currentShapeRotation;
        private bool _fitPending;
        private bool _suppressDisplayChange;
        private bool _closingFromOwner;

        public ControlWindow()
        {
            InitializeComponent();
            _gif.FrameChanged += OnGifFrameChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayPlacement.PlaceControl(this);

            _audience = new AudienceWindow
            {
                Document = _doc,
                GetControlCanvasSize = () => ((float)skControl.ActualWidth, (float)skControl.ActualHeight)
            };
            _audience.Closing += Audience_Closing;

            PopulateDisplays();
            Activate();
            _isFullyLoaded = true;
        }

        private void Audience_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closingFromOwner)
                return;
            e.Cancel = true;
            _audience?.Hide();
        }

        private void PopulateDisplays()
        {
            _suppressDisplayChange = true;
            DisplayCombo.Items.Clear();
            DisplayCombo.Items.Add(new AudienceTarget("Windowed (this PC)", null));

            var displays = DisplayPlacement.GetDisplays();
            int select = 0;
            foreach (var display in displays)
            {
                string label = display.IsPrimary ? $"{display.Name} (primary, fullscreen)" : $"{display.Name} (fullscreen)";
                DisplayCombo.Items.Add(new AudienceTarget(label, display));
            }

            if (displays.Count >= 2)
            {
                for (int i = 0; i < DisplayCombo.Items.Count; i++)
                {
                    if (DisplayCombo.Items[i] is AudienceTarget target &&
                        target.Display is { IsPrimary: false })
                    {
                        select = i;
                        break;
                    }
                }
            }

            DisplayCombo.SelectedIndex = select;
            _suppressDisplayChange = false;
            ApplyAudienceTarget();
        }

        private void DisplayCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressDisplayChange || !_isFullyLoaded)
                return;
            ApplyAudienceTarget();
        }

        private void ApplyAudienceTarget()
        {
            if (_audience == null)
                return;

            if (DisplayCombo.SelectedItem is AudienceTarget { Display: { } display })
                DisplayPlacement.PlaceFullscreen(_audience, display);
            else
                DisplayPlacement.PlaceWindowed(_audience);

            if (!_audience.IsVisible)
                _audience.Show();
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e) => LoadImage();

        private void LoadImage()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Images & Animated GIFs|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };

            if (dlg.ShowDialog() == true)
                TryLoadMap(dlg.FileName, resetView: true);
        }

        private bool TryLoadMap(string filePath, bool resetView)
        {
            try
            {
                bool isGif = string.Equals(Path.GetExtension(filePath), ".gif", StringComparison.OrdinalIgnoreCase);
                if (isGif)
                {
                    if (!_gif.TryLoad(filePath, out var firstFrame, out var error))
                    {
                        MessageBox.Show(error ?? "Failed to load GIF.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    _doc.SetImage(firstFrame, owned: false, filePath);
                }
                else
                {
                    var decoded = SKBitmap.Decode(filePath);
                    if (decoded == null)
                    {
                        MessageBox.Show("Could not decode the image.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    _gif.StopAndClear();
                    _doc.SetImage(decoded, owned: true, filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load image:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            FogMaskService.ResetFog(_doc);
            _doc.ClearOverlays();
            _previewShape = null;
            _gesture = GestureKind.None;

            if (resetView)
            {
                if (skControl.ActualWidth > 1 && skControl.ActualHeight > 1)
                {
                    _doc.FitTo((float)skControl.ActualWidth, (float)skControl.ActualHeight);
                    _fitPending = false;
                }
                else
                {
                    _doc.Transform = SKMatrix.CreateIdentity();
                    _fitPending = true;
                }
            }
            else
            {
                _fitPending = false;
            }

            InvalidateAll();
            return true;
        }

        private void OnGifFrameChanged()
        {
            if (_gif.CurrentFrame != null)
                _doc.SetAnimationFrame(_gif.CurrentFrame);
            InvalidateAll();
        }

        private void SkControl_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            if (_doc.Image == null)
                return;

            canvas.SetMatrix(_doc.Transform);
            canvas.DrawBitmap(_doc.Image, 0, 0);

            if (!_doc.FogFullyRevealed && _doc.FogMask != null)
                canvas.DrawBitmap(_doc.FogMask, 0, 0);

            foreach (var marker in _doc.Markers)
                marker.Draw(canvas);
            foreach (var shape in _doc.Shapes)
                shape.Draw(canvas);
            _previewShape?.Draw(canvas);

            if (_doc.PointerVisible)
                canvas.DrawCircle(_doc.PointerWorld, 15, _pointerPaint);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (e.Key == Key.F)
            {
                LoadImage();
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                ResetFog();
                e.Handled = true;
            }
            else if (e.Key == Key.S && ctrl)
            {
                SaveSession();
                e.Handled = true;
            }
            else if (e.Key == Key.O && ctrl)
            {
                LoadSession();
                e.Handled = true;
            }
            else if (e.Key == Key.Z && ctrl)
            {
                Undo();
                e.Handled = true;
            }
        }

        private void Undo()
        {
            if (_doc.UndoStack.Count == 0)
                return;

            var last = _doc.UndoStack.Pop();
            if (last is Marker marker)
                _doc.Markers.Remove(marker);
            if (last is ShapeOverlay shape)
                _doc.Shapes.Remove(shape);
            InvalidateAll();
        }

        private void SkControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            var screenPoint = new SKPoint((float)pos.X, (float)pos.Y);
            _doc.TryScreenToWorld(screenPoint, out var worldPoint);
            _doc.PointerWorld = worldPoint;
            _doc.PointerVisible = true;

            if (e.ChangedButton == MouseButton.Left)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    _gesture = GestureKind.Pan;
                    _lastPanPoint = pos;
                    skControl.CaptureMouse();
                    return;
                }

                if (_shapeModeActive)
                {
                    _gesture = GestureKind.PlaceShape;
                    UpdatePreview(worldPoint);
                    skControl.CaptureMouse();
                    InvalidateAll();
                    return;
                }

                bool cover = rbCover.IsChecked == true;
                _gesture = cover ? GestureKind.Cover : GestureKind.Reveal;
                PunchAtScreen(screenPoint, cover);
                skControl.CaptureMouse();
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    var toDelete = _doc.Markers.FirstOrDefault(m => m.HitTest(worldPoint));
                    if (toDelete != null)
                    {
                        _doc.Markers.Remove(toDelete);
                        InvalidateAll();
                    }
                    return;
                }

                var (text, color) = GetSelectedCondition();
                var marker = new Marker(worldPoint, GetSelectedMarkerSize(), text, color);
                _doc.Markers.Add(marker);
                _doc.UndoStack.Push(marker);
                InvalidateAll();
            }
        }

        private void SkControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            var screenPoint = new SKPoint((float)pos.X, (float)pos.Y);
            _doc.TryScreenToWorld(screenPoint, out var worldPoint);
            _doc.PointerWorld = worldPoint;
            _doc.PointerVisible = true;

            if (_gesture is GestureKind.Reveal or GestureKind.Cover && e.LeftButton == MouseButtonState.Pressed)
            {
                PunchAtScreen(screenPoint, _gesture == GestureKind.Cover);
                return;
            }

            if (_gesture == GestureKind.Pan && e.LeftButton == MouseButtonState.Pressed && _lastPanPoint.HasValue)
            {
                var deltaX = (float)(pos.X - _lastPanPoint.Value.X);
                var deltaY = (float)(pos.Y - _lastPanPoint.Value.Y);
                _doc.Transform = _doc.Transform.PreConcat(SKMatrix.CreateTranslation(deltaX, deltaY));
                _lastPanPoint = pos;
                InvalidateAll();
                return;
            }

            if (_shapeModeActive && _gesture != GestureKind.Pan)
            {
                UpdatePreview(worldPoint);
                InvalidateAll();
            }
            else
            {
                InvalidateAll();
            }
        }

        private void SkControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (_gesture == GestureKind.PlaceShape && _shapeModeActive)
            {
                var shape = (_previewShape ?? new ShapeOverlay(_currentShapeType, _doc.PointerWorld, _currentShapeSize, _currentShapeRotation)).Clone();
                _doc.Shapes.Add(shape);
                _doc.UndoStack.Push(shape);
            }

            _gesture = GestureKind.None;
            _lastPanPoint = null;
            if (skControl.IsMouseCaptured)
                skControl.ReleaseMouseCapture();
            InvalidateAll();
        }

        private void SkControl_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_gesture == GestureKind.None)
                return;
            _gesture = GestureKind.None;
            _lastPanPoint = null;
        }

        private void SkControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(skControl);
            var point = new SKPoint((float)pos.X, (float)pos.Y);
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (_shapeModeActive && !ctrl)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    _currentShapeRotation += e.Delta > 0 ? 15f : -15f;
                else
                    _currentShapeSize = Math.Clamp(_currentShapeSize * (e.Delta > 0 ? 1.12f : 0.88f), 30f, 800f);

                UpdatePreview(_doc.PointerWorld);
                InvalidateAll();
            }
            else
            {
                float zoom = e.Delta > 0 ? 1.1f : 0.9f;
                _doc.Transform = _doc.Transform.PreConcat(SKMatrix.CreateScale(zoom, zoom, point.X, point.Y));
                InvalidateAll();
            }

            e.Handled = true;
        }

        private void SkControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_fitPending || _doc.Image == null)
                return;
            if (skControl.ActualWidth < 1 || skControl.ActualHeight < 1)
                return;

            _doc.FitTo((float)skControl.ActualWidth, (float)skControl.ActualHeight);
            _fitPending = false;
            InvalidateAll();
        }

        private void PunchAtScreen(SKPoint screenPoint, bool cover)
        {
            if (_doc.Image == null)
                return;

            _doc.TryScreenToWorld(screenPoint, out var imagePoint);
            FogMaskService.Punch(_doc, imagePoint, _doc.ImageRadiusFromScreen(_revealRadius), cover);
            InvalidateAll();
        }

        private void UpdatePreview(SKPoint worldPoint)
        {
            if (_previewShape == null)
            {
                _previewShape = new ShapeOverlay(_currentShapeType, worldPoint, _currentShapeSize, _currentShapeRotation);
                return;
            }

            _previewShape.Type = _currentShapeType;
            _previewShape.Update(worldPoint, _currentShapeSize, _currentShapeRotation);
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

        private void ClearOverlays_Click(object sender, RoutedEventArgs e)
        {
            _doc.ClearOverlays();
            InvalidateAll();
        }

        private void RevealAll_Click(object sender, RoutedEventArgs e)
        {
            _doc.FogFullyRevealed = true;
            InvalidateAll();
        }

        private void ResetFog_Click(object sender, RoutedEventArgs e) => ResetFog();

        private void ResetFog()
        {
            FogMaskService.ResetFog(_doc);
            InvalidateAll();
        }

        private void ShapeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isFullyLoaded)
                return;

            _currentShapeType = ShapeCombo.SelectedIndex switch
            {
                2 => ShapeType.Square,
                3 => ShapeType.Rectangle,
                4 => ShapeType.Cone,
                _ => ShapeType.Circle
            };

            if (ShapeCombo.SelectedIndex == 0)
            {
                _shapeModeActive = false;
                _previewShape = null;
                InvalidateAll();
                return;
            }

            _shapeModeActive = true;
            _currentShapeSize = 120f;
            _currentShapeRotation = 0f;
            _previewShape = new ShapeOverlay(_currentShapeType, _doc.PointerWorld, _currentShapeSize, _currentShapeRotation);
            InvalidateAll();
        }

        private void SaveSession_Click(object sender, RoutedEventArgs e) => SaveSession();

        private void SaveSession()
        {
            if (_doc.Image == null)
            {
                MessageBox.Show("No map loaded to save.", "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "D&D Map Session|*.mapSes",
                DefaultExt = "mapSes"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                SessionStore.Save(dlg.FileName, _doc);
                InvalidateAll();
                MessageBox.Show("Session saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSession_Click(object sender, RoutedEventArgs e) => LoadSession();

        private void LoadSession()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Map Session|*.mapSes"
            };

            if (dlg.ShowDialog() != true)
                return;

            SessionLoadResult? session = null;
            try
            {
                session = SessionStore.Load(dlg.FileName);
                if (!TryLoadMap(session.ImagePath, resetView: false))
                    return;

                _doc.RestoreView(session.Scale, session.TranslateX, session.TranslateY);
                _doc.ClearOverlays();
                _doc.Markers.AddRange(session.Markers);
                _doc.Shapes.AddRange(session.Shapes);

                if (session.RevealedMask != null)
                {
                    FogMaskService.ApplyRevealedMask(_doc, session.RevealedMask);
                    session.RevealedMask = null;
                }

                _doc.FogFullyRevealed = session.FogFullyRevealed;
                InvalidateAll();

                if (!string.IsNullOrEmpty(session.MaskWarning))
                    MessageBox.Show("Session loaded, but fog could not be fully restored:\n" + session.MaskWarning, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load session:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                session?.RevealedMask?.Dispose();
            }
        }

        private void InvalidateAll()
        {
            skControl?.InvalidateVisual();
            _audience?.InvalidateSurface();
        }

        protected override void OnClosed(EventArgs e)
        {
            _closingFromOwner = true;
            _gif.FrameChanged -= OnGifFrameChanged;
            _gif.Dispose();
            if (_audience != null)
            {
                _audience.Closing -= Audience_Closing;
                _audience.Close();
                _audience = null;
            }
            _pointerPaint.Dispose();
            _doc.Dispose();
            base.OnClosed(e);
        }

        private sealed class AudienceTarget
        {
            public AudienceTarget(string label, DisplayInfo? display)
            {
                Label = label;
                Display = display;
            }

            public string Label { get; }
            public DisplayInfo? Display { get; }
            public override string ToString() => Label;
        }
    }
}
