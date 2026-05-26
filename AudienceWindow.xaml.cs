using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Windows;

namespace rpgFogOfWar
{
    public partial class AudienceWindow : Window
    {
        public ControlWindow? Control { get; set; }

        public AudienceWindow()
        {
            InitializeComponent();
            Topmost = true;
            ShowActivated = false;
        }

        private void SkAudience_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            if (Control?.currentImage == null)
                return;

            canvas.SetMatrix(Control.transform);
            canvas.DrawBitmap(Control.currentImage, 0, 0);

            // Draw current fog state
            if (!Control.fogRevealed && Control.fogMask != null)
            {
                canvas.DrawBitmap(Control.fogMask, 0, 0);
            }

            // Draw all markers and shapes
            Control.DrawOverlays(canvas);

            // === NEW: Mouse Pointer Token on Audience ===
            if (Control != null)
            {
                var tokenPaint = new SKPaint
                {
                    Color = SKColors.Cyan,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 6,
                    IsAntialias = true
                };

                // Main circle
                canvas.DrawCircle(Control.mirrorMousePos, 22, tokenPaint);

                // Inner circle
                tokenPaint.StrokeWidth = 3;
                tokenPaint.Color = SKColors.White;
                canvas.DrawCircle(Control.mirrorMousePos, 12, tokenPaint);

                // Crosshair
                tokenPaint.StrokeWidth = 2;
                canvas.DrawLine(
                    Control.mirrorMousePos.X - 35, Control.mirrorMousePos.Y,
                    Control.mirrorMousePos.X + 35, Control.mirrorMousePos.Y, tokenPaint);

                canvas.DrawLine(
                    Control.mirrorMousePos.X, Control.mirrorMousePos.Y - 35,
                    Control.mirrorMousePos.X, Control.mirrorMousePos.Y + 35, tokenPaint);
            }
        }
    }
}