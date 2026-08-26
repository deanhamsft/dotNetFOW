using SkiaSharp;

namespace rpgFogOfWar;

public enum ShapeType { Circle, Square, Rectangle, Cone }

public sealed class ShapeOverlay
{
    public ShapeType Type { get; set; }
    public SKPoint Center { get; set; }
    public float Size { get; set; }
    public float Rotation { get; set; }

    public ShapeOverlay(ShapeType type, SKPoint center, float size, float rotation = 0f)
    {
        Type = type;
        Center = center;
        Size = size;
        Rotation = rotation;
    }

    public void Update(SKPoint center, float size, float rotation)
    {
        Center = center;
        Size = size;
        Rotation = rotation;
    }

    public ShapeOverlay Clone() => new(Type, Center, Size, Rotation);

    public void Draw(SKCanvas canvas)
    {
        using var outline = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round
        };
        using var fill = new SKPaint
        {
            Color = new SKColor(0, 220, 80, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round
        };

        DrawBody(canvas, outline);
        DrawBody(canvas, fill);
    }

    private void DrawBody(SKCanvas canvas, SKPaint paint)
    {
        canvas.Save();
        canvas.Translate(Center.X, Center.Y);
        canvas.RotateDegrees(Rotation);

        switch (Type)
        {
            case ShapeType.Circle:
                canvas.DrawCircle(0, 0, Size, paint);
                break;
            case ShapeType.Square:
                canvas.DrawRect(-Size, -Size, Size * 2, Size * 2, paint);
                break;
            case ShapeType.Rectangle:
                canvas.DrawRect(-Size * 1.5f, -Size * 0.75f, Size * 3f, Size * 1.5f, paint);
                break;
            case ShapeType.Cone:
                using (var path = new SKPath())
                {
                    path.MoveTo(0, 0);
                    path.LineTo(-Size * 0.8f, Size * 1.8f);
                    path.LineTo(Size * 0.8f, Size * 1.8f);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                break;
        }

        canvas.Restore();
    }
}
