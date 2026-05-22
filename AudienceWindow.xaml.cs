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
            canvas.Clear(SKColors.Black);        // Force black background

            if (Control?.currentImage == null || !Control.fogRevealed)
                return;   // Stay completely black until revealed

            // Only draw once revealed
            canvas.SetMatrix(Control.transform);
            canvas.DrawBitmap(Control.currentImage, 0, 0);
            Control.DrawOverlays(canvas);
        }
    }
}