using SkiaSharp;

namespace rpgFogOfWar;

public sealed class Marker
{
    public SKPoint Center { get; }
    public float Radius { get; }
    public string Text { get; }
    public SKColor Color { get; }
    public double SizeMultiplier { get; }

    public Marker(SKPoint center, double sizeMultiplier, string text, SKColor color)
    {
        Center = center;
        SizeMultiplier = sizeMultiplier;
        Radius = (float)(45 * sizeMultiplier);
        Text = text;
        Color = color;
    }

    public void Draw(SKCanvas canvas)
    {
        using var outer = new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            IsAntialias = true
        };
        canvas.DrawCircle(Center, Radius, outer);

        using var inner = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };
        canvas.DrawCircle(Center, Radius - 6, inner);

        using var font = new SKFont(SKTypeface.Default, 15f * (float)SizeMultiplier);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        float circ = Radius * 2f * (float)Math.PI;
        float textW = font.MeasureText(Text, paint);
        if (textW <= 0) return;

        float startAngle = -(textW / circ) * 180f;

        for (int i = 0; i < Text.Length; i++)
        {
            string ch = Text[i].ToString();
            float chW = font.MeasureText(ch, paint);
            float angle = startAngle + (chW / circ) * 180f;
            float rad = angle * (float)Math.PI / 180f;

            float x = Center.X + (Radius + 14) * (float)Math.Cos(rad);
            float y = Center.Y + (Radius + 14) * (float)Math.Sin(rad);

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(angle + 90);
            canvas.DrawText(ch, 0, 0, font, paint);
            canvas.Restore();

            startAngle += (chW / circ) * 360f;
        }
    }

    public bool HitTest(SKPoint p) => SKPoint.Distance(Center, p) < Radius + 20;
}
