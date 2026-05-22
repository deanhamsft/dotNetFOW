using SkiaSharp;

public enum ShapeType { Circle, Square, Cone, Rectangle }

public class ShapeOverlay
{
    public ShapeType Type { get; }
    public SKPoint Start { get; }
    public SKPoint End { get; }

    public ShapeOverlay(ShapeType type, SKPoint start, SKPoint end)
    {
        Type = type;
        Start = start;
        End = end;
    }

    public void Draw(SKCanvas canvas)
    {
        var paint = new SKPaint
        {
            Color = new SKColor(0, 255, 100, 120), // semi-transparent green
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        switch (Type)
        {
            case ShapeType.Circle:
                float radius = SKPoint.Distance(Start, End);
                canvas.DrawCircle(Start, radius, paint);
                break;

            case ShapeType.Square:
            case ShapeType.Rectangle:
                // Fixed: Properly create rectangle from two points
                float left = Math.Min(Start.X, End.X);
                float top = Math.Min(Start.Y, End.Y);
                float right = Math.Max(Start.X, End.X);
                float bottom = Math.Max(Start.Y, End.Y);

                var rect = new SKRect(left, top, right, bottom);
                canvas.DrawRect(rect, paint);
                break;

            case ShapeType.Cone:
                // Simple cone/triangle
                var path = new SKPath();
                path.MoveTo(Start);
                path.LineTo(new SKPoint(End.X - 60, End.Y));
                path.LineTo(new SKPoint(End.X + 60, End.Y));
                path.Close();
                canvas.DrawPath(path, paint);
                break;
        }
    }
}