using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using PcMate.Models;

namespace PcMate.Services;

public sealed partial class AnimationController
{
    private const int DecodePixelWidth = 256;
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

    public BitmapImage? CurrentFrame => GetCurrentAnimation().Frames.Count == 0
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

    public BitmapImage? MoveNext()
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

        AnimationConfig ryanStanding = new(
            "kakao-ryan-standing",
            ["kakao-ryan", "kakao-ryan-standing"],
            "kakao-ryan-standing",
            ".png",
            TimeSpan.FromMilliseconds(80));

        return new Dictionary<string, CharacterProfile>
        {
            ["tails"] = new CharacterProfile(
                new Dictionary<CharacterState, AnimationConfig>
                {
                    [CharacterState.Lying] = new("tails-standing", ["tails", "tails-standing"], "tails-standing", ".png", TimeSpan.FromMilliseconds(140)),
                    [CharacterState.Sitting] = new("tails-standing", ["tails", "tails-standing"], "tails-standing", ".png", TimeSpan.FromMilliseconds(140)),
                    [CharacterState.Walking] = new("tails-walking", ["tails", "tails-walking"], "tails-walking", ".png", TimeSpan.FromMilliseconds(55)),
                    [CharacterState.Running] = new("tails-running", ["tails", "tails-running"], "tails-running", ".png", TimeSpan.FromMilliseconds(40))
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
                    [CharacterState.Lying] = ryanStanding,
                    [CharacterState.Sitting] = ryanStanding,
                    [CharacterState.Walking] = new("kakao-ryan-walking", ["kakao-ryan", "kakao-ryan-walking"], "kakao-ryan-walking", ".png", TimeSpan.FromMilliseconds(55)),
                    [CharacterState.Running] = new("kakao-ryan-running", ["kakao-ryan", "kakao-ryan-running"], "kakao-ryan-running", ".png", TimeSpan.FromMilliseconds(45))
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
        long ticks = Math.Max(1, (long)(frameInterval.Ticks / _speedMultiplier));
        return TimeSpan.FromTicks(ticks);
    }

    private static AnimationDefinition LoadAnimation(string assetRoot, AnimationConfig config)
    {
        string directory = Path.Combine([assetRoot, .. config.DirectoryParts]);
        if (!Directory.Exists(directory))
        {
            return new AnimationDefinition(config.Name, [], config.FrameInterval);
        }

        List<BitmapImage> frames = Directory
            .GetFiles(directory, $"{config.FilePrefix}-*{config.Extension}")
            .OrderBy(GetFrameNumber)
            .Select(LoadBitmap)
            .ToList();

        return new AnimationDefinition(config.Name, frames, config.FrameInterval);
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
        IReadOnlyList<BitmapImage> Frames,
        TimeSpan FrameInterval);
}

public sealed record CharacterOption(string Id, string DisplayName);
