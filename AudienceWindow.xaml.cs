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

            // Draw the current fog mask state (revealed areas will show through)
            if (!Control.fogRevealed && Control.fogMask != null)
            {
                canvas.DrawBitmap(Control.fogMask, 0, 0);
            }

            // Always draw markers and shapes on top (they should be visible on both)
            Control.DrawOverlays(canvas);
        }
    }
}