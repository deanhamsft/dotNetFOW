using SkiaSharp;

public enum ShapeType { Circle, Square, Rectangle, Cone }

public class ShapeOverlay
{
    public ShapeType Type { get; }
    public SKPoint Center { get; }
    public float Size { get; }           // Radius for circle/cone, width for rect
    public float Rotation { get; }       // Degrees

    public ShapeOverlay(ShapeType type, SKPoint center, float size, float rotation = 0f)
    {
        Type = type;
        Center = center;
        Size = size;
        Rotation = rotation;
    }

    public void Draw(SKCanvas canvas)
    {
        var paint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 255), // Semi-transparent green
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5,
            IsAntialias = true
        };

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
                var path = new SKPath();
                path.MoveTo(0, 0);
                path.LineTo(-Size * 0.8f, Size * 1.8f);
                path.LineTo(Size * 0.8f, Size * 1.8f);
                path.Close();
                canvas.DrawPath(path, paint);
                break;
        }

        canvas.Restore();
    }
}