using SkiaSharp;
using System;

public class Marker
{
    public SKPoint Center { get; }
    public float Radius { get; }
    public string Text { get; }
    public SKColor Color { get; }

    public Marker(SKPoint center, double sizeMultiplier, string text, SKColor color)
    {
        Center = center;
        Radius = (float)(45 * sizeMultiplier);
        Text = text;
        Color = color;
    }

    public void Draw(SKCanvas canvas)
    {
        // Outer ring
        canvas.DrawCircle(Center, Radius, new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            IsAntialias = true
        });

        // Inner ring
        canvas.DrawCircle(Center, Radius - 6, new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        });

        // Curved Text
        using var font = new SKFont(SKTypeface.Default, 15f * (Radius / 45f));
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