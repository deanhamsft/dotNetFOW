using SkiaSharp;

namespace rpgFogOfWar.Services;

public static class FogMaskService
{
    public static readonly SKColor FogColor = new(60, 60, 60, 200);

    public static void ResetFog(MapDocument doc)
    {
        if (doc.Image == null)
            return;

        var (fog, revealed) = Create(doc.Image.Width, doc.Image.Height);
        doc.ReplaceFog(fog, revealed);
        doc.FogFullyRevealed = false;
    }

    public static (SKBitmap fog, SKBitmap revealed) Create(int width, int height)
    {
        var fog = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(fog))
            canvas.Clear(FogColor);

        var revealed = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(revealed))
            canvas.Clear(SKColors.Black);

        return (fog, revealed);
    }

    public static void Punch(MapDocument doc, SKPoint imagePoint, float imageRadius, bool cover)
    {
        if (doc.FogMask == null || doc.RevealedMask == null)
            return;

        if (cover)
        {
            doc.FogFullyRevealed = false;
            using (var canvas = new SKCanvas(doc.FogMask))
            using (var paint = new SKPaint { Color = FogColor, IsAntialias = true, BlendMode = SKBlendMode.Src })
                canvas.DrawCircle(imagePoint, imageRadius, paint);

            using (var canvas = new SKCanvas(doc.RevealedMask))
            using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true, BlendMode = SKBlendMode.Src })
                canvas.DrawCircle(imagePoint, imageRadius, paint);
        }
        else
        {
            using (var canvas = new SKCanvas(doc.FogMask))
            using (var erase = new SKPaint { Color = SKColors.Transparent, BlendMode = SKBlendMode.Clear, IsAntialias = true })
                canvas.DrawCircle(imagePoint, imageRadius, erase);

            using (var canvas = new SKCanvas(doc.RevealedMask))
            using (var reveal = new SKPaint { Color = SKColors.Transparent, BlendMode = SKBlendMode.Clear, IsAntialias = true })
                canvas.DrawCircle(imagePoint, imageRadius, reveal);
        }
    }

    public static void ApplyRevealedMask(MapDocument doc, SKBitmap loaded)
    {
        if (doc.Image == null)
        {
            loaded.Dispose();
            return;
        }

        SKBitmap matched = loaded;
        if (loaded.Width != doc.Image.Width || loaded.Height != doc.Image.Height)
        {
            matched = ScaleTo(loaded, doc.Image.Width, doc.Image.Height);
            loaded.Dispose();
        }

        var fog = new SKBitmap(doc.Image.Width, doc.Image.Height);
        using (var canvas = new SKCanvas(fog))
        using (var paint = new SKPaint { BlendMode = SKBlendMode.DstIn })
        {
            canvas.Clear(FogColor);
            canvas.DrawBitmap(matched, 0, 0, paint);
        }

        doc.ReplaceFog(fog, matched);
    }

    public static bool IsRevealedAt(SKBitmap? revealedMask, SKPoint point, bool fullyRevealed)
    {
        if (fullyRevealed || revealedMask == null)
            return true;

        int x = (int)Math.Floor(point.X);
        int y = (int)Math.Floor(point.Y);
        if (x < 0 || y < 0 || x >= revealedMask.Width || y >= revealedMask.Height)
            return false;

        return revealedMask.GetPixel(x, y).Alpha < 128;
    }

    public static SKBitmap ScaleTo(SKBitmap source, int width, int height)
    {
        if (source.Width == width && source.Height == height)
            return source.Copy();

        var scaled = source.Resize(
            new SKImageInfo(width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));

        return scaled ?? throw new InvalidOperationException("Could not scale the fog mask to the map size.");
    }
}
