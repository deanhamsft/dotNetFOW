using SkiaSharp;
using System;

public class Marker
{
    public SKPoint Center { get; }
    public float Radius { get; }
    public string Text { get; }
    public double SizeMultiplier { get; }

    public Marker(SKPoint center, double sizeMultiplier, string text = "Condition")
    {
        Center = center;
        SizeMultiplier = sizeMultiplier;
        Radius = (float)(45 * sizeMultiplier);
        Text = text;
    }

    public void Draw(SKCanvas canvas)
    {
        // Outer Ring (Red)
        var ringPaint = new SKPaint
        {
            Color = SKColors.Red,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            IsAntialias = true
        };
        canvas.DrawCircle(Center, Radius, ringPaint);

        // Inner Ring (White)
        canvas.DrawCircle(Center, Radius - 6, new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        });

        // === Curved Text ===
        using var font = new SKFont(SKTypeface.Default, 16f * (float)SizeMultiplier);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        float circumference = Radius * 2f * (float)Math.PI;
        float textWidth = font.MeasureText(Text, textPaint);

        if (textWidth <= 0) return;

        float angleStep = (textWidth / circumference) * 360f;
        float startAngle = -angleStep / 2f;

        for (int i = 0; i < Text.Length; i++)
        {
            string ch = Text[i].ToString();
            float charWidth = font.MeasureText(ch, textPaint);

            float angle = startAngle + (charWidth / circumference) * 180f;
            float rad = angle * (float)Math.PI / 180f;

            // Fixed: explicit casts
            float x = Center.X + (Radius + 12) * (float)Math.Cos(rad);
            float y = Center.Y + (Radius + 12) * (float)Math.Sin(rad);

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(angle + 90);
            canvas.DrawText(ch, 0, 0, font, textPaint);
            canvas.Restore();

            startAngle += (charWidth / circumference) * 360f;
        }
    }

    public bool HitTest(SKPoint p) => SKPoint.Distance(Center, p) < Radius + 20;
}