using System.IO;
using System.Text.Json;
using SkiaSharp;

namespace rpgFogOfWar.Services;

public sealed class SessionLoadResult
{
    public required string ImagePath { get; init; }
    public float Scale { get; init; }
    public float TranslateX { get; init; }
    public float TranslateY { get; init; }
    public bool FogFullyRevealed { get; init; }
    public required List<Marker> Markers { get; init; }
    public required List<ShapeOverlay> Shapes { get; init; }
    public SKBitmap? RevealedMask { get; set; }
    public string? MaskWarning { get; init; }
}

public static class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Save(string sessionPath, MapDocument doc)
    {
        if (doc.Image == null)
            throw new InvalidOperationException("No map loaded to save.");
        if (string.IsNullOrWhiteSpace(doc.ImagePath) || !File.Exists(doc.ImagePath))
            throw new FileNotFoundException("The current map file is missing, so the session cannot be saved.");

        var dir = Path.GetDirectoryName(sessionPath)
            ?? throw new InvalidOperationException("Invalid session path.");
        Directory.CreateDirectory(dir);

        var stem = Path.GetFileNameWithoutExtension(sessionPath);
        var imageCopy = Path.Combine(dir, stem + "_map" + Path.GetExtension(doc.ImagePath));
        CopyIfDifferent(doc.ImagePath, imageCopy);

        string? fogFileName = null;
        if (doc.RevealedMask != null)
        {
            var fogPath = Path.Combine(dir, stem + "_fog.png");
            using var encoded = doc.RevealedMask.Encode(SKEncodedImageFormat.Png, 90)
                ?? throw new InvalidOperationException("Could not encode the fog mask.");
            File.WriteAllBytes(fogPath, encoded.ToArray());
            fogFileName = Path.GetFileName(fogPath);
        }

        var session = new SessionData
        {
            ImagePath = Path.GetFullPath(imageCopy),
            ImageFile = Path.GetFileName(imageCopy),
            FogMaskFile = fogFileName,
            FogRevealed = doc.FogFullyRevealed,
            TranslateX = doc.Transform.TransX,
            TranslateY = doc.Transform.TransY,
            Scale = doc.Transform.ScaleX
        };

        foreach (var marker in doc.Markers)
        {
            session.Markers.Add(new MarkerData
            {
                X = marker.Center.X,
                Y = marker.Center.Y,
                SizeMultiplier = marker.SizeMultiplier,
                Text = marker.Text,
                ColorHex = ColorFormat.ToHex(marker.Color)
            });
        }

        foreach (var shape in doc.Shapes)
        {
            session.Shapes.Add(new ShapeData
            {
                Type = shape.Type.ToString(),
                CenterX = shape.Center.X,
                CenterY = shape.Center.Y,
                Size = shape.Size,
                Rotation = shape.Rotation
            });
        }

        File.WriteAllText(sessionPath, JsonSerializer.Serialize(session, JsonOptions));
    }

    public static SessionLoadResult Load(string sessionPath)
    {
        string json = File.ReadAllText(sessionPath);
        var session = JsonSerializer.Deserialize<SessionData>(json, JsonOptions)
            ?? throw new InvalidDataException("The session file is empty or invalid.");

        var dir = Path.GetDirectoryName(sessionPath) ?? "";
        string? imagePath = null;
        if (!string.IsNullOrWhiteSpace(session.ImageFile))
            imagePath = Path.Combine(dir, session.ImageFile);
        if (imagePath == null || !File.Exists(imagePath))
            imagePath = session.ImagePath;
        if (imagePath == null || !File.Exists(imagePath))
            throw new FileNotFoundException("The map image for this session was not found. It may have been moved.");

        var markers = new List<Marker>();
        foreach (var data in session.Markers)
        {
            var color = ColorFormat.Parse(data.ColorHex, SKColors.Red);
            markers.Add(new Marker(new SKPoint(data.X, data.Y), data.SizeMultiplier, data.Text, color));
        }

        var shapes = new List<ShapeOverlay>();
        foreach (var data in session.Shapes)
        {
            if (Enum.TryParse(data.Type, out ShapeType type))
                shapes.Add(new ShapeOverlay(type, new SKPoint(data.CenterX, data.CenterY), data.Size, data.Rotation));
        }

        SKBitmap? mask = null;
        string? warning = null;
        if (!string.IsNullOrWhiteSpace(session.FogMaskFile))
        {
            var fogPath = Path.Combine(dir, session.FogMaskFile);
            if (File.Exists(fogPath))
            {
                mask = SKBitmap.Decode(fogPath);
                if (mask == null)
                    warning = "The fog mask file could not be decoded.";
            }
            else
            {
                warning = "The fog mask file is missing.";
            }
        }
        else if (!string.IsNullOrEmpty(session.RevealedMaskBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(session.RevealedMaskBase64);
                mask = SKBitmap.Decode(bytes);
                if (mask == null)
                    warning = "The saved fog mask could not be decoded.";
            }
            catch (FormatException)
            {
                warning = "The saved fog mask data is not valid Base64.";
            }
        }

        return new SessionLoadResult
        {
            ImagePath = imagePath,
            Scale = session.Scale,
            TranslateX = session.TranslateX,
            TranslateY = session.TranslateY,
            FogFullyRevealed = session.FogRevealed,
            Markers = markers,
            Shapes = shapes,
            RevealedMask = mask,
            MaskWarning = warning
        };
    }

    private static void CopyIfDifferent(string source, string dest)
    {
        var srcFull = Path.GetFullPath(source);
        var destFull = Path.GetFullPath(dest);
        if (string.Equals(srcFull, destFull, StringComparison.OrdinalIgnoreCase))
            return;
        File.Copy(srcFull, destFull, overwrite: true);
    }
}
