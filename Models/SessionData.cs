namespace rpgFogOfWar;

public sealed class SessionData
{
    public string? ImagePath { get; set; }
    public string? ImageFile { get; set; }
    public string? FogMaskFile { get; set; }

    public float TranslateX { get; set; }
    public float TranslateY { get; set; }
    public float Scale { get; set; } = 1f;

    public List<MarkerData> Markers { get; set; } = new();
    public List<ShapeData> Shapes { get; set; } = new();

    public bool FogRevealed { get; set; }

    public string? RevealedMaskBase64 { get; set; }
}

public sealed class MarkerData
{
    public float X { get; set; }
    public float Y { get; set; }
    public double SizeMultiplier { get; set; }
    public string Text { get; set; } = "";
    public string ColorHex { get; set; } = "#FFFF0000";
}

public sealed class ShapeData
{
    public string Type { get; set; } = "Circle";
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float Size { get; set; }
    public float Rotation { get; set; }
}
