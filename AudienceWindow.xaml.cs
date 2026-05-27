using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Diagnostics;
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
            canvas.Clear(SKColors.Black);   // Force pure black

            if (Control?.currentImage == null)
                return;

            canvas.SetMatrix(Control.transform);

            // Draw the full image
            canvas.DrawBitmap(Control.currentImage, 0, 0);

            // If not fully revealed, apply the revealedMask as an alpha mask
            if (!Control.fogRevealed && Control.revealedMask != null)
            {
                Debug.WriteLine("Applying revealedMask on Audience");

                canvas.DrawBitmap(Control.revealedMask, 0, 0);
            }
            else
            {
                Debug.WriteLine("Full reveal mode on Audience");
            }

            // Draw overlays
            Control.DrawOverlays(canvas);

            // Cyan mouse pointer token
            var tokenPaint = new SKPaint
            {
                Color = SKColors.Cyan,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6,
                IsAntialias = true
            };
            canvas.DrawCircle(Control.mirrorMousePos, 22, tokenPaint);

            tokenPaint.StrokeWidth = 3;
            tokenPaint.Color = SKColors.White;
            canvas.DrawCircle(Control.mirrorMousePos, 12, tokenPaint);
        }
    }
}