using SkiaSharp;

namespace rpgFogOfWar.Services;

public static class ColorFormat
{
    public static string ToHex(SKColor color) =>
        $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

    public static SKColor Parse(string? hex, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;

        hex = hex.Trim();
        if (hex[0] != '#')
            hex = "#" + hex;

        return SKColor.TryParse(hex, out var color) ? color : fallback;
    }
}
