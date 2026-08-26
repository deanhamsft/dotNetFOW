using System.Windows;
using rpgFogOfWar.Services;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace rpgFogOfWar
{
    public partial class AudienceWindow : Window
    {
        public MapDocument? Document { get; set; }
        public Func<(float Width, float Height)>? GetControlCanvasSize { get; set; }

        private readonly SKPaint _pointerOuter = new()
        {
            Color = SKColors.Cyan,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            IsAntialias = true
        };

        private readonly SKPaint _pointerInner = new()
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };

        public AudienceWindow()
        {
            InitializeComponent();
            ShowActivated = false;
        }

        private bool _disposed;

        public void InvalidateSurface() => skAudience?.InvalidateVisual();

        private void SkAudience_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (_disposed)
                return;

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            var doc = Document;
            if (doc?.Image == null)
                return;

            float controlW = 0;
            float controlH = 0;
            if (GetControlCanvasSize != null)
                (controlW, controlH) = GetControlCanvasSize();

            var matrix = ViewTransform.ForSurface(
                doc.Transform,
                controlW,
                controlH,
                e.Info.Width,
                e.Info.Height);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(doc.Image, 0, 0);

            if (!doc.FogFullyRevealed && doc.RevealedMask != null)
                canvas.DrawBitmap(doc.RevealedMask, 0, 0);

            foreach (var marker in doc.Markers)
            {
                if (FogMaskService.IsRevealedAt(doc.RevealedMask, marker.Center, doc.FogFullyRevealed))
                    marker.Draw(canvas);
            }

            foreach (var shape in doc.Shapes)
            {
                if (FogMaskService.IsRevealedAt(doc.RevealedMask, shape.Center, doc.FogFullyRevealed))
                    shape.Draw(canvas);
            }

            if (doc.PointerVisible &&
                FogMaskService.IsRevealedAt(doc.RevealedMask, doc.PointerWorld, doc.FogFullyRevealed))
            {
                canvas.DrawCircle(doc.PointerWorld, 22, _pointerOuter);
                canvas.DrawCircle(doc.PointerWorld, 12, _pointerInner);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _disposed = true;
            _pointerOuter.Dispose();
            _pointerInner.Dispose();
            base.OnClosed(e);
        }
    }
}
