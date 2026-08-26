using SkiaSharp;

namespace rpgFogOfWar.Services;

public static class ViewTransform
{
    public static SKMatrix ForSurface(
        SKMatrix controlTransform,
        float controlWidth,
        float controlHeight,
        float surfaceWidth,
        float surfaceHeight)
    {
        if (controlWidth < 1 || controlHeight < 1 || surfaceWidth < 1 || surfaceHeight < 1)
            return controlTransform;

        if (Math.Abs(controlWidth - surfaceWidth) < 0.5f && Math.Abs(controlHeight - surfaceHeight) < 0.5f)
            return controlTransform;

        if (!controlTransform.TryInvert(out var inverse))
            return controlTransform;

        var imgTL = inverse.MapPoint(0, 0);
        var imgTR = inverse.MapPoint(controlWidth, 0);
        var imgBL = inverse.MapPoint(0, controlHeight);
        var imgBR = inverse.MapPoint(controlWidth, controlHeight);

        float minX = Math.Min(Math.Min(imgTL.X, imgTR.X), Math.Min(imgBL.X, imgBR.X));
        float maxX = Math.Max(Math.Max(imgTL.X, imgTR.X), Math.Max(imgBL.X, imgBR.X));
        float minY = Math.Min(Math.Min(imgTL.Y, imgTR.Y), Math.Min(imgBL.Y, imgBR.Y));
        float maxY = Math.Max(Math.Max(imgTL.Y, imgTR.Y), Math.Max(imgBL.Y, imgBR.Y));

        float visW = Math.Max(maxX - minX, 1f);
        float visH = Math.Max(maxY - minY, 1f);
        float scale = Math.Min(surfaceWidth / visW, surfaceHeight / visH);
        float dx = (surfaceWidth - visW * scale) / 2f;
        float dy = (surfaceHeight - visH * scale) / 2f;

        return new SKMatrix(
            scale, 0, dx - scale * minX,
            0, scale, dy - scale * minY,
            0, 0, 1);
    }
}
