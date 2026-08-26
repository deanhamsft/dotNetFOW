using System.IO;
using System.Windows.Threading;
using SkiaSharp;

namespace rpgFogOfWar.Services;

public sealed class GifAnimator : IDisposable
{
    private readonly DispatcherTimer _timer;
    private List<SKBitmap>? _frames;
    private List<int>? _durations;
    private int _index;

    public SKBitmap? CurrentFrame { get; private set; }
    public event Action? FrameChanged;

    public GifAnimator()
    {
        _timer = new DispatcherTimer();
        _timer.Tick += OnTick;
    }

    public bool TryLoad(string path, out SKBitmap firstFrame, out string? error)
    {
        firstFrame = null!;
        if (!TryDecode(path, out var frames, out var durations, out error) || frames.Count == 0)
            return false;

        StopAndClear();
        _frames = frames;
        _durations = durations;
        _index = 0;
        CurrentFrame = _frames[0];
        firstFrame = _frames[0];
        StartIfNeeded();
        return true;
    }

    public void StopAndClear()
    {
        _timer.Stop();
        DisposeFrames();
        CurrentFrame = null;
        _index = 0;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        DisposeFrames();
        CurrentFrame = null;
    }

    private void StartIfNeeded()
    {
        if (_frames == null || _frames.Count <= 1 || _durations == null)
            return;

        _timer.Interval = TimeSpan.FromMilliseconds(_durations[_index]);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_frames == null || _frames.Count == 0 || _durations == null)
            return;

        _index = (_index + 1) % _frames.Count;
        CurrentFrame = _frames[_index];
        _timer.Interval = TimeSpan.FromMilliseconds(_durations[_index] > 0 ? _durations[_index] : 100);
        FrameChanged?.Invoke();
    }

    private void DisposeFrames()
    {
        if (_frames == null)
        {
            _durations = null;
            return;
        }

        foreach (var frame in _frames)
            frame.Dispose();
        _frames = null;
        _durations = null;
    }

    private static bool TryDecode(string path, out List<SKBitmap> frames, out List<int> durations, out string? error)
    {
        frames = new List<SKBitmap>();
        durations = new List<int>();
        error = null;

        try
        {
            using var stream = File.OpenRead(path);
            using var managed = new SKManagedStream(stream);
            using var codec = SKCodec.Create(managed);
            if (codec == null)
            {
                error = "Failed to load GIF.";
                return false;
            }

            var info = codec.Info;
            using var composition = new SKBitmap(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            composition.Erase(SKColors.Transparent);
            SKBitmap? saved = null;

            for (int i = 0; i < codec.FrameCount; i++)
            {
                var frameInfo = codec.FrameInfo[i];
                if (frameInfo.DisposalMethod == SKCodecAnimationDisposalMethod.RestorePrevious)
                {
                    saved?.Dispose();
                    saved = composition.Copy();
                }

                int required = frameInfo.RequiredFrame;
                SKCodecOptions options;
                if (required >= 0 && required < frames.Count)
                {
                    frames[required].CopyTo(composition);
                    options = new SKCodecOptions(i, required);
                }
                else
                {
                    composition.Erase(SKColors.Transparent);
                    options = new SKCodecOptions(i);
                }

                codec.GetPixels(composition.Info, composition.GetPixels(), options);

                var display = composition.Copy();
                if (display == null)
                {
                    error = "Failed to copy a GIF frame.";
                    saved?.Dispose();
                    DisposeList(frames);
                    frames.Clear();
                    return false;
                }

                frames.Add(display);
                durations.Add(frameInfo.Duration > 0 ? frameInfo.Duration : 100);

                switch (frameInfo.DisposalMethod)
                {
                    case SKCodecAnimationDisposalMethod.RestoreBackgroundColor:
                        using (var canvas = new SKCanvas(composition))
                        using (var clear = new SKPaint { BlendMode = SKBlendMode.Clear })
                            canvas.DrawRect(frameInfo.FrameRect, clear);
                        break;
                    case SKCodecAnimationDisposalMethod.RestorePrevious:
                        if (saved != null)
                        {
                            saved.CopyTo(composition);
                            saved.Dispose();
                            saved = null;
                        }
                        break;
                }
            }

            saved?.Dispose();
            if (frames.Count == 0)
            {
                error = "The GIF did not contain any frames.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            DisposeList(frames);
            frames.Clear();
            durations.Clear();
            error = ex.Message;
            return false;
        }
    }

    private static void DisposeList(List<SKBitmap> frames)
    {
        foreach (var frame in frames)
            frame.Dispose();
    }
}
