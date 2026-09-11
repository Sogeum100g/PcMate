using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PcMate.Models;
using PcMate.Localization;

namespace PcMate.Services;

public sealed partial class AnimationController
{
    private const int DecodePixelWidth = 192;
    private const long MaxCachedAnimationBytes = 32L * 1024 * 1024;
    private const string WildcardFilePrefix = "*";
    private const double BaseSpeedScale = 0.5;
    private const double MinSpeedMultiplier = 0.25;
    private const double MaxSpeedMultiplier = 4.0;
    private readonly string _builtInAssetRoot;
    private readonly CustomCharacterStore _customCharacterStore;
    private readonly Dictionary<string, CharacterProfile> _profiles;
    private readonly Dictionary<string, CachedAnimation> _animationCache = [];
    private readonly Dictionary<string, Task<AnimationDefinition>> _animationLoadTasks = [];
    private CharacterState _currentState = CharacterState.Lying;
    private string _currentCharacterId = "blob";
    private AnimationDefinition? _currentAnimation;
    private int _frameIndex;
    private double _speedMultiplier = 1.0;
    private long _animationCacheBytes;
    private int _cacheVersion;

    public AnimationController(string builtInAssetRoot, CustomCharacterStore customCharacterStore)
    {
        _builtInAssetRoot = builtInAssetRoot;
        _customCharacterStore = customCharacterStore;
        _profiles = CreateProfiles();

        if (!_profiles.ContainsKey(_currentCharacterId))
        {
            _currentCharacterId = _profiles.Keys.FirstOrDefault() ?? string.Empty;
        }
    }

    public IReadOnlyList<CharacterOption> Characters => _profiles
        .Select(profile => new CharacterOption(profile.Key, profile.Value.DisplayName, profile.Value.IsCustom))
        .ToList();

    public string CurrentCharacterId => _currentCharacterId;

    public double SpeedMultiplier => _speedMultiplier;

    public TimeSpan FrameInterval => _currentAnimation is null
        ? TimeSpan.FromMilliseconds(100)
        : ApplySpeedMultiplier(_currentAnimation.FrameInterval);

    public bool IsAnimated => _currentAnimation?.Frames.Count > 1;

    public ImageSource? CurrentFrame => _currentAnimation is null || _currentAnimation.Frames.Count == 0
        ? null
        : _currentAnimation.Frames[_frameIndex];

    public bool SetCharacter(string characterId)
    {
        if (!_profiles.ContainsKey(characterId) || _currentCharacterId == characterId)
        {
            return false;
        }

        _currentCharacterId = characterId;
        _currentAnimation = null;
        _frameIndex = 0;
        ClearAnimationCache();
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

    public CharacterOption RegisterCustomCharacter(
        string displayName,
        string sleepingPath,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        ThrowIfDuplicateCharacterName(displayName, exceptId: null);
        CustomCharacterDefinition definition = _customCharacterStore.Register(
            displayName,
            sleepingPath,
            standingPath,
            walkingPath,
            runningPath);

        _profiles[definition.Id] = CreateCustomProfile(definition);
        return new CharacterOption(definition.Id, definition.DisplayName, true);
    }

    public CharacterOption UpdateCustomCharacter(
        string characterId,
        string displayName,
        string sleepingPath,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        if (!IsCustomCharacter(characterId))
        {
            throw new InvalidOperationException(LocalizationManager.Instance.Get("ErrorOnlyCustomEditable"));
        }

        ThrowIfDuplicateCharacterName(displayName, exceptId: characterId);
        CustomCharacterDefinition definition = _customCharacterStore.Update(
            characterId,
            displayName,
            sleepingPath,
            standingPath,
            walkingPath,
            runningPath);

        _profiles[definition.Id] = CreateCustomProfile(definition);
        if (_currentCharacterId == definition.Id)
        {
            _currentAnimation = null;
            _frameIndex = 0;
            ClearAnimationCache();
        }

        return new CharacterOption(definition.Id, definition.DisplayName, true);
    }

    public bool DeleteCustomCharacter(string characterId)
    {
        if (!IsCustomCharacter(characterId))
        {
            return false;
        }

        _customCharacterStore.Delete(characterId);
        _profiles.Remove(characterId);
        if (_currentCharacterId == characterId)
        {
            _currentCharacterId = "blob";
            _currentAnimation = null;
            _frameIndex = 0;
            ClearAnimationCache();
        }

        return true;
    }

    public bool IsCustomCharacter(string characterId)
    {
        return _profiles.TryGetValue(characterId, out CharacterProfile? profile) && profile.IsCustom;
    }

    public CustomCharacterDefinition? GetCustomCharacterDefinition(string characterId)
    {
        return _customCharacterStore.Load()
            .FirstOrDefault(definition => definition.Id.Equals(characterId, StringComparison.OrdinalIgnoreCase));
    }

    public void SetState(CharacterState state)
    {
        if (_currentState == state)
        {
            return;
        }

        _currentState = state;
        _currentAnimation = null;
        _frameIndex = 0;
    }

    public async Task<bool> LoadCurrentAnimationAsync()
    {
        string characterId = _currentCharacterId;
        CharacterState state = _currentState;
        AnimationConfig config = GetConfig(state);
        AnimationDefinition animation = await GetOrLoadAnimationAsync(config);

        if (_currentCharacterId != characterId || _currentState != state)
        {
            return false;
        }

        _currentAnimation = animation;
        _frameIndex = 0;
        return true;
    }

    public ImageSource? MoveNext()
    {
        AnimationDefinition? animation = _currentAnimation;
        if (animation is null || animation.Frames.Count == 0)
        {
            return null;
        }

        _frameIndex = (_frameIndex + 1) % animation.Frames.Count;
        return CurrentFrame;
    }

    private Dictionary<string, CharacterProfile> CreateProfiles()
    {
        var profiles = new Dictionary<string, CharacterProfile>
        {
            ["blob"] = new CharacterProfile(
                "Blob",
                false,
                new Dictionary<CharacterState, AnimationConfig>
                {
                    [CharacterState.Lying] = new("blob-sleeping", _builtInAssetRoot, ["blob", "blob-sleeping"], "blob-sleeping", ".gif", TimeSpan.FromMilliseconds(80)),
                    [CharacterState.Sitting] = new("blob-standing", _builtInAssetRoot, ["blob", "blob-standing"], "blob-standing", ".gif", TimeSpan.FromMilliseconds(80)),
                    [CharacterState.Walking] = new("blob-walking", _builtInAssetRoot, ["blob", "blob-walking"], "blob-walking", ".gif", TimeSpan.FromMilliseconds(55)),
                    [CharacterState.Running] = new("blob-running", _builtInAssetRoot, ["blob", "blob-running"], "blob-running", ".gif", TimeSpan.FromMilliseconds(45))
                })
        };

        foreach (CustomCharacterDefinition definition in _customCharacterStore.Load())
        {
            profiles[definition.Id] = CreateCustomProfile(definition);
        }

        return profiles;
    }

    private static CharacterProfile CreateCustomProfile(CustomCharacterDefinition definition)
    {
        return new CharacterProfile(
            definition.DisplayName,
            true,
            new Dictionary<CharacterState, AnimationConfig>
            {
                [CharacterState.Lying] = CreateCustomConfig(definition, CharacterState.Lying, TimeSpan.FromMilliseconds(80)),
                [CharacterState.Sitting] = CreateCustomConfig(definition, CharacterState.Sitting, TimeSpan.FromMilliseconds(80)),
                [CharacterState.Walking] = CreateCustomConfig(definition, CharacterState.Walking, TimeSpan.FromMilliseconds(55)),
                [CharacterState.Running] = CreateCustomConfig(definition, CharacterState.Running, TimeSpan.FromMilliseconds(45))
            });
    }

    private static AnimationConfig CreateCustomConfig(
        CustomCharacterDefinition definition,
        CharacterState requestedState,
        TimeSpan frameInterval)
    {
        (CharacterState State, string StateName, string Path)[] candidates =
        [
            (CharacterState.Lying, CustomCharacterStore.SleepingState, CustomCharacterStore.GetSleepingPath(definition)),
            (CharacterState.Sitting, CustomCharacterStore.StandingState, CustomCharacterStore.GetStandingPath(definition)),
            (CharacterState.Walking, CustomCharacterStore.WalkingState, CustomCharacterStore.GetWalkingPath(definition)),
            (CharacterState.Running, CustomCharacterStore.RunningState, CustomCharacterStore.GetRunningPath(definition))
        ];
        (CharacterState State, string StateName, string Path) selected = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Path))
            .OrderBy(candidate => Math.Abs((int)candidate.State - (int)requestedState))
            .ThenBy(candidate => (int)candidate.State)
            .First();
        string fileName = Path.GetFileName(selected.Path);

        return new AnimationConfig(
            $"{definition.Id}-{requestedState}-{selected.StateName}",
            definition.DirectoryPath,
            [selected.StateName],
            Path.GetFileNameWithoutExtension(fileName),
            Path.GetExtension(fileName),
            frameInterval);
    }

    private void ThrowIfDuplicateCharacterName(string displayName, string? exceptId)
    {
        if (_profiles.Any(profile =>
                !profile.Key.Equals(exceptId, StringComparison.OrdinalIgnoreCase)
                && profile.Value.DisplayName.Equals(displayName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(LocalizationManager.Instance.Get("ErrorDuplicateCharacterName"));
        }
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

    private TimeSpan ApplySpeedMultiplier(TimeSpan frameInterval)
    {
        long ticks = Math.Max(1, (long)(frameInterval.Ticks / (_speedMultiplier * BaseSpeedScale)));
        return TimeSpan.FromTicks(ticks);
    }

    private Task<AnimationDefinition> GetOrLoadAnimationAsync(AnimationConfig config)
    {
        string cacheKey = GetAnimationCacheKey(config);
        lock (_animationCache)
        {
            if (_animationCache.TryGetValue(cacheKey, out CachedAnimation? cachedAnimation))
            {
                _animationCache[cacheKey] = cachedAnimation with { LastUsedAt = DateTime.UtcNow };
                return Task.FromResult(cachedAnimation.Animation);
            }

            if (_animationLoadTasks.TryGetValue(cacheKey, out Task<AnimationDefinition>? loadTask))
            {
                return loadTask;
            }

            Task<AnimationDefinition> nextLoadTask = LoadAnimationOnStaThreadAsync(config);
            _animationLoadTasks[cacheKey] = nextLoadTask;
            int cacheVersion = _cacheVersion;
            _ = nextLoadTask.ContinueWith(task =>
            {
                lock (_animationCache)
                {
                    _animationLoadTasks.Remove(cacheKey);
                    if (task.Status == TaskStatus.RanToCompletion && cacheVersion == _cacheVersion)
                    {
                        CacheAnimation(cacheKey, task.Result);
                    }
                }
            }, TaskScheduler.Default);
            return nextLoadTask;
        }
    }

    private void CacheAnimation(string cacheKey, AnimationDefinition animation)
    {
        long byteCost = EstimateByteCost(animation);
        if (byteCost <= 0)
        {
            return;
        }

        if (_animationCache.Remove(cacheKey, out CachedAnimation? existing))
        {
            _animationCacheBytes -= existing.ByteCost;
        }

        if (byteCost > MaxCachedAnimationBytes)
        {
            _animationCache.Clear();
            _animationCacheBytes = 0;
            return;
        }

        _animationCache[cacheKey] = new CachedAnimation(animation, byteCost, DateTime.UtcNow);
        _animationCacheBytes += byteCost;
        TrimAnimationCache();
    }

    private void TrimAnimationCache()
    {
        while (_animationCacheBytes > MaxCachedAnimationBytes && _animationCache.Count > 0)
        {
            string oldestKey = _animationCache
                .OrderBy(item => item.Value.LastUsedAt)
                .Select(item => item.Key)
                .First();

            _animationCacheBytes -= _animationCache[oldestKey].ByteCost;
            _animationCache.Remove(oldestKey);
        }
    }

    private void ClearAnimationCache()
    {
        lock (_animationCache)
        {
            _animationCache.Clear();
            _animationLoadTasks.Clear();
            _animationCacheBytes = 0;
            _cacheVersion++;
        }
    }

    private static long EstimateByteCost(AnimationDefinition animation)
    {
        long byteCost = 0;
        foreach (ImageSource frame in animation.Frames)
        {
            if (frame is BitmapSource bitmapSource)
            {
                int bytesPerPixel = Math.Max(1, bitmapSource.Format.BitsPerPixel / 8);
                byteCost += (long)bitmapSource.PixelWidth * bitmapSource.PixelHeight * bytesPerPixel;
            }
        }

        return byteCost;
    }

    private static string GetAnimationCacheKey(AnimationConfig config)
    {
        string directory = Path.Combine([config.RootPath, .. config.DirectoryParts]);
        string? path = Directory.Exists(directory)
            ? GetAnimationFilePath(directory, config)
            : null;

        if (path is null || !File.Exists(path))
        {
            return config.Name;
        }

        var fileInfo = new FileInfo(path);
        return $"{config.Name}|{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
    }

    private static Task<AnimationDefinition> LoadAnimationOnStaThreadAsync(AnimationConfig config)
    {
        var completion = new TaskCompletionSource<AnimationDefinition>();
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(LoadAnimation(config));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "PcMate animation loader",
            Priority = ThreadPriority.BelowNormal
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static AnimationDefinition LoadAnimation(AnimationConfig config)
    {
        string directory = Path.Combine([config.RootPath, .. config.DirectoryParts]);
        if (!Directory.Exists(directory))
        {
            return new AnimationDefinition(config.Name, [], config.FrameInterval);
        }

        string? exactPath = GetAnimationFilePath(directory, config);
        if (exactPath is not null && File.Exists(exactPath))
        {
            if (config.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                return LoadGifAnimation(exactPath, config);
            }

            return new AnimationDefinition(config.Name, [(ImageSource)LoadBitmap(exactPath)], config.FrameInterval);
        }

        List<ImageSource> frames = Directory
            .GetFiles(directory, $"{config.FilePrefix}-*{config.Extension}")
            .OrderBy(GetFrameNumber)
            .Select(path => (ImageSource)LoadBitmap(path))
            .ToList();

        return new AnimationDefinition(config.Name, frames, config.FrameInterval);
    }

    private static string? GetAnimationFilePath(string directory, AnimationConfig config)
    {
        if (config.FilePrefix == WildcardFilePrefix)
        {
            return Directory
                .GetFiles(directory, $"*{config.Extension}")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        return Path.Combine(directory, $"{config.FilePrefix}{config.Extension}");
    }

    private static AnimationDefinition LoadGifAnimation(string gifPath, AnimationConfig config)
    {
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

        int sourceCanvasWidth = frames.Max(frame => GetFrameLeft(frame) + Math.Max(GetFrameWidth(frame), frame.PixelWidth));
        int sourceCanvasHeight = frames.Max(frame => GetFrameTop(frame) + Math.Max(GetFrameHeight(frame), frame.PixelHeight));
        double renderScale = sourceCanvasWidth > DecodePixelWidth
            ? DecodePixelWidth / (double)sourceCanvasWidth
            : 1.0;
        int canvasWidth = Math.Max(1, (int)Math.Round(sourceCanvasWidth * renderScale));
        int canvasHeight = Math.Max(1, (int)Math.Round(sourceCanvasHeight * renderScale));
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
                new Rect(
                    GetFrameLeft(frame) * renderScale,
                    GetFrameTop(frame) * renderScale,
                    frame.PixelWidth * renderScale,
                    frame.PixelHeight * renderScale));
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

    private sealed record CharacterProfile(
        string DisplayName,
        bool IsCustom,
        IReadOnlyDictionary<CharacterState, AnimationConfig> Animations);

    private sealed record AnimationConfig(
        string Name,
        string RootPath,
        string[] DirectoryParts,
        string FilePrefix,
        string Extension,
        TimeSpan FrameInterval);

    private sealed record AnimationDefinition(
        string Name,
        IReadOnlyList<ImageSource> Frames,
        TimeSpan FrameInterval);

    private sealed record GifFrameLayer(BitmapSource Source, Rect Bounds);

    private sealed record CachedAnimation(
        AnimationDefinition Animation,
        long ByteCost,
        DateTime LastUsedAt);
}

public sealed record CharacterOption(string Id, string DisplayName, bool IsCustom = false);
