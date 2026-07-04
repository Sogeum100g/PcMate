using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PcMate.Models;

namespace PcMate.Services;

public sealed partial class AnimationController
{
    private const int DecodePixelWidth = 256;
    private const double BaseSpeedScale = 0.5;
    private const double MinSpeedMultiplier = 0.25;
    private const double MaxSpeedMultiplier = 4.0;
    private readonly string _assetRoot;
    private readonly Dictionary<string, CharacterProfile> _profiles;
    private CharacterState _currentState = CharacterState.Lying;
    private string _currentCharacterId = "tails";
    private AnimationDefinition? _currentAnimation;
    private int _frameIndex;
    private double _speedMultiplier = 1.0;

    public AnimationController(string assetRoot)
    {
        _assetRoot = assetRoot;
        _profiles = CreateProfiles();
    }

    public IReadOnlyList<CharacterOption> Characters { get; } =
    [
        new CharacterOption("tails", "Tails"),
        new CharacterOption("red_parrot", "Red Parrot"),
        new CharacterOption("kakao_ryan", "Kakao Ryan")
    ];

    public string CurrentCharacterId => _currentCharacterId;

    public double SpeedMultiplier => _speedMultiplier;

    public TimeSpan FrameInterval => ApplySpeedMultiplier(GetCurrentAnimation().FrameInterval);

    public bool IsAnimated => GetCurrentAnimation().Frames.Count > 1;

    public ImageSource? CurrentFrame => GetCurrentAnimation().Frames.Count == 0
        ? null
        : GetCurrentAnimation().Frames[_frameIndex];

    public bool SetCharacter(string characterId)
    {
        if (!_profiles.ContainsKey(characterId) || _currentCharacterId == characterId)
        {
            return false;
        }

        _currentCharacterId = characterId;
        _currentAnimation = null;
        _frameIndex = 0;
        return true;
    }

    public bool SetSpeedMultiplier(double speedMultiplier)
    {
        double nextSpeedMultiplier = Math.Clamp(speedMultiplier, MinSpeedMultiplier, MaxSpeedMultiplier);
        if (Math.Abs(_speedMultiplier - nextSpeedMultiplier) < 0.001)
        {
            return false;
        }

        _speedMultiplier = nextSpeedMultiplier;
        return true;
    }

    public void SetState(CharacterState state)
    {
        if (_currentState == state)
        {
            return;
        }

        AnimationConfig nextConfig = GetConfig(state);
        if (_currentAnimation?.Name == nextConfig.Name)
        {
            _currentAnimation = _currentAnimation with { FrameInterval = nextConfig.FrameInterval };
        }
        else
        {
            _currentAnimation = LoadAnimation(_assetRoot, nextConfig);
        }

        _currentState = state;
        _frameIndex = 0;
    }

    public ImageSource? MoveNext()
    {
        AnimationDefinition animation = GetCurrentAnimation();
        if (animation.Frames.Count == 0)
        {
            return null;
        }

        _frameIndex = (_frameIndex + 1) % animation.Frames.Count;
        return CurrentFrame;
    }

    private static Dictionary<string, CharacterProfile> CreateProfiles()
    {
        AnimationConfig redParrot = new(
            "red-parrot",
            ["red-parrot"],
            "red-parrot",
            ".ico",
            TimeSpan.FromMilliseconds(80));

        return new Dictionary<string, CharacterProfile>
        {
            ["tails"] = new CharacterProfile(
                new Dictionary<CharacterState, AnimationConfig>
                {
                    [CharacterState.Lying] = new("tails-standing", ["tails", "tails-standing"], "tails-standing", ".gif", TimeSpan.FromMilliseconds(140)),
                    [CharacterState.Sitting] = new("tails-standing", ["tails", "tails-standing"], "tails-standing", ".gif", TimeSpan.FromMilliseconds(140)),
                    [CharacterState.Walking] = new("tails-walking", ["tails", "tails-walking"], "tails-walking", ".gif", TimeSpan.FromMilliseconds(55)),
                    [CharacterState.Running] = new("tails-running", ["tails", "tails-running"], "tails-running", ".gif", TimeSpan.FromMilliseconds(40))
                }),
            ["red_parrot"] = new CharacterProfile(
                new Dictionary<CharacterState, AnimationConfig>
                {
                    [CharacterState.Lying] = redParrot,
                    [CharacterState.Sitting] = redParrot,
                    [CharacterState.Walking] = redParrot,
                    [CharacterState.Running] = redParrot
                }),
            ["kakao_ryan"] = new CharacterProfile(
                new Dictionary<CharacterState, AnimationConfig>
                {
                    [CharacterState.Lying] = new("kakao-ryan-standing", ["kakao-ryan", "kakao-ryan-standing"], "kakao-ryan-standing", ".gif", TimeSpan.FromMilliseconds(80)),
                    [CharacterState.Sitting] = new("kakao-ryan-standing", ["kakao-ryan", "kakao-ryan-standing"], "kakao-ryan-standing", ".gif", TimeSpan.FromMilliseconds(80)),
                    [CharacterState.Walking] = new("kakao-ryan-walking", ["kakao-ryan", "kakao-ryan-walking"], "kakao-ryan-walking", ".gif", TimeSpan.FromMilliseconds(55)),
                    [CharacterState.Running] = new("kakao-ryan-running", ["kakao-ryan", "kakao-ryan-running"], "kakao-ryan-running", ".gif", TimeSpan.FromMilliseconds(45))
                })
        };
    }

    private AnimationConfig GetConfig(CharacterState state)
    {
        CharacterProfile profile = _profiles[_currentCharacterId];
        if (profile.Animations.TryGetValue(state, out AnimationConfig? config))
        {
            return config;
        }

        return profile.Animations[CharacterState.Lying];
    }

    private AnimationDefinition GetCurrentAnimation()
    {
        _currentAnimation ??= LoadAnimation(_assetRoot, GetConfig(_currentState));
        return _currentAnimation;
    }

    private TimeSpan ApplySpeedMultiplier(TimeSpan frameInterval)
    {
        long ticks = Math.Max(1, (long)(frameInterval.Ticks / (_speedMultiplier * BaseSpeedScale)));
        return TimeSpan.FromTicks(ticks);
    }

    private static AnimationDefinition LoadAnimation(string assetRoot, AnimationConfig config)
    {
        string directory = Path.Combine([assetRoot, .. config.DirectoryParts]);
        if (!Directory.Exists(directory))
        {
            return new AnimationDefinition(config.Name, [], config.FrameInterval);
        }

        if (config.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return LoadGifAnimation(directory, config);
        }

        List<ImageSource> frames = Directory
            .GetFiles(directory, $"{config.FilePrefix}-*{config.Extension}")
            .OrderBy(GetFrameNumber)
            .Select(path => (ImageSource)LoadBitmap(path))
            .ToList();

        return new AnimationDefinition(config.Name, frames, config.FrameInterval);
    }

    private static AnimationDefinition LoadGifAnimation(string directory, AnimationConfig config)
    {
        string gifPath = Path.Combine(directory, $"{config.FilePrefix}.gif");
        if (!File.Exists(gifPath))
        {
            return new AnimationDefinition(config.Name, [], config.FrameInterval);
        }

        using FileStream stream = File.OpenRead(gifPath);
        var decoder = new GifBitmapDecoder(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        List<ImageSource> frames = CreateCompositedGifFrames(decoder.Frames);

        return new AnimationDefinition(config.Name, frames, config.FrameInterval);
    }

    private static List<ImageSource> CreateCompositedGifFrames(IReadOnlyList<BitmapFrame> frames)
    {
        if (frames.Count == 0)
        {
            return [];
        }

        int canvasWidth = frames.Max(frame => GetFrameLeft(frame) + Math.Max(GetFrameWidth(frame), frame.PixelWidth));
        int canvasHeight = frames.Max(frame => GetFrameTop(frame) + Math.Max(GetFrameHeight(frame), frame.PixelHeight));
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return frames.Select(frame => CreateGifFrameSource(frame)).ToList();
        }

        List<GifFrameLayer> layers = [];
        List<ImageSource> compositedFrames = [];
        foreach (BitmapFrame frame in frames)
        {
            var layer = new GifFrameLayer(
                frame,
                new Rect(GetFrameLeft(frame), GetFrameTop(frame), frame.PixelWidth, frame.PixelHeight));
            layers.Add(layer);

            RenderTargetBitmap compositedFrame = RenderGifFrame(canvasWidth, canvasHeight, layers);
            compositedFrames.Add(CreateGifFrameSource(compositedFrame));

            if (GetFrameDisposal(frame) == GifDisposalRestoreToBackground)
            {
                layers.Remove(layer);
            }
        }

        return compositedFrames;
    }

    private static RenderTargetBitmap RenderGifFrame(int canvasWidth, int canvasHeight, IReadOnlyList<GifFrameLayer> layers)
    {
        var visual = new DrawingVisual();
        using (DrawingContext drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, canvasWidth, canvasHeight));
            foreach (GifFrameLayer layer in layers)
            {
                drawingContext.DrawImage(layer.Source, layer.Bounds);
            }
        }

        var bitmap = new RenderTargetBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static int GetFrameNumber(string path)
    {
        Match match = FrameNumberRegex().Match(Path.GetFileNameWithoutExtension(path));
        return match.Success && int.TryParse(match.Groups[1].Value, out int frameNumber)
            ? frameNumber
            : int.MaxValue;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        int decodePixelWidth = GetDecodePixelWidth(path);
        if (decodePixelWidth > 0)
        {
            image.DecodePixelWidth = decodePixelWidth;
        }

        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private const int GifDisposalRestoreToBackground = 2;

    private static ImageSource CreateGifFrameSource(BitmapSource frame)
    {
        BitmapSource source = frame;
        if (source.PixelWidth > DecodePixelWidth)
        {
            double scale = DecodePixelWidth / (double)source.PixelWidth;
            var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            transformed.Freeze();
            return transformed;
        }

        if (source.CanFreeze)
        {
            source.Freeze();
        }

        return source;
    }

    private static int GetFrameLeft(BitmapFrame frame)
    {
        return GetFrameMetadataInt(frame, "/imgdesc/Left", 0);
    }

    private static int GetFrameTop(BitmapFrame frame)
    {
        return GetFrameMetadataInt(frame, "/imgdesc/Top", 0);
    }

    private static int GetFrameWidth(BitmapFrame frame)
    {
        return GetFrameMetadataInt(frame, "/imgdesc/Width", frame.PixelWidth);
    }

    private static int GetFrameHeight(BitmapFrame frame)
    {
        return GetFrameMetadataInt(frame, "/imgdesc/Height", frame.PixelHeight);
    }

    private static int GetFrameDisposal(BitmapFrame frame)
    {
        return GetFrameMetadataInt(frame, "/grctlext/Disposal", 0);
    }

    private static int GetFrameMetadataInt(BitmapFrame frame, string query, int fallback)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata && metadata.GetQuery(query) is object value)
            {
                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch (NotSupportedException)
        {
        }
        catch (InvalidCastException)
        {
        }
        catch (FormatException)
        {
        }

        return fallback;
    }

    private static int GetDecodePixelWidth(string path)
    {
        using FileStream stream = File.OpenRead(path);
        BitmapDecoder decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.DelayCreation,
            BitmapCacheOption.OnLoad);

        return decoder.Frames[0].PixelWidth > DecodePixelWidth ? DecodePixelWidth : 0;
    }

    [GeneratedRegex(@"-(\d+)$")]
    private static partial Regex FrameNumberRegex();

    private sealed record CharacterProfile(IReadOnlyDictionary<CharacterState, AnimationConfig> Animations);

    private sealed record AnimationConfig(
        string Name,
        string[] DirectoryParts,
        string FilePrefix,
        string Extension,
        TimeSpan FrameInterval);

    private sealed record AnimationDefinition(
        string Name,
        IReadOnlyList<ImageSource> Frames,
        TimeSpan FrameInterval);

    private sealed record GifFrameLayer(BitmapSource Source, Rect Bounds);
}

public sealed record CharacterOption(string Id, string DisplayName);
