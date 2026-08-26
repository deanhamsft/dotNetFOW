using SkiaSharp;

namespace rpgFogOfWar;

public sealed class MapDocument : IDisposable
{
    public SKBitmap? Image { get; private set; }
    public bool ImageOwned { get; private set; }
    public string? ImagePath { get; private set; }
    public SKMatrix Transform { get; set; } = SKMatrix.CreateIdentity();
    public SKBitmap? FogMask { get; private set; }
    public SKBitmap? RevealedMask { get; private set; }
    public bool FogFullyRevealed { get; set; }
    public List<Marker> Markers { get; } = new();
    public List<ShapeOverlay> Shapes { get; } = new();
    public Stack<object> UndoStack { get; } = new();
    public SKPoint PointerWorld { get; set; }
    public bool PointerVisible { get; set; }

    public void SetImage(SKBitmap? bitmap, bool owned, string? path)
    {
        if (ImageOwned && Image != null && !ReferenceEquals(Image, bitmap))
            Image.Dispose();

        Image = bitmap;
        ImageOwned = owned;
        if (path != null)
            ImagePath = path;
    }

    public void SetAnimationFrame(SKBitmap frame)
    {
        Image = frame;
        ImageOwned = false;
    }

    public void ReplaceFog(SKBitmap? fog, SKBitmap? revealed)
    {
        if (!ReferenceEquals(FogMask, fog))
        {
            FogMask?.Dispose();
            FogMask = fog;
        }

        if (!ReferenceEquals(RevealedMask, revealed))
        {
            RevealedMask?.Dispose();
            RevealedMask = revealed;
        }
    }

    public void ClearOverlays()
    {
        Markers.Clear();
        Shapes.Clear();
        UndoStack.Clear();
        PointerVisible = false;
    }

    public void FitTo(float viewWidth, float viewHeight)
    {
        if (Image == null || viewWidth < 1 || viewHeight < 1)
            return;

        float sx = viewWidth / Image.Width;
        float sy = viewHeight / Image.Height;
        float s = Math.Min(sx, sy);
        float dx = (viewWidth - Image.Width * s) / 2f;
        float dy = (viewHeight - Image.Height * s) / 2f;
        Transform = new SKMatrix(
            s, 0, dx,
            0, s, dy,
            0, 0, 1);
    }

    public void RestoreView(float scale, float translateX, float translateY)
    {
        if (scale <= 0)
            scale = 1f;

        Transform = new SKMatrix(
            scale, 0, translateX,
            0, scale, translateY,
            0, 0, 1);
    }

    public float ImageRadiusFromScreen(float screenRadius)
    {
        float scale = Math.Abs(Transform.ScaleX);
        if (scale < 0.0001f)
            scale = 1f;
        return screenRadius / scale;
    }

    public bool TryScreenToWorld(SKPoint screen, out SKPoint world)
    {
        if (!Transform.TryInvert(out var inverse))
        {
            world = screen;
            return false;
        }

        world = inverse.MapPoint(screen);
        return true;
    }

    public void Dispose()
    {
        if (ImageOwned)
            Image?.Dispose();
        FogMask?.Dispose();
        RevealedMask?.Dispose();
        Image = null;
        FogMask = null;
        RevealedMask = null;
        ImageOwned = false;
    }
}
