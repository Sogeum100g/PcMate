using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using PcMate.Localization;
using PcMate.Models;
using PcMate.Monitors;
using PcMate.Services;

namespace PcMate.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan ResourceRefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TopProcessInitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TopProcessRefreshInterval = TimeSpan.FromSeconds(15);
    private readonly IResourceMonitor _monitor;
    private readonly TopProcessMonitor _topProcessMonitor;
    private readonly StateClassifier _classifier;
    private readonly AnimationController _animationController;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Stopwatch _animationClock = new();
    private TimeSpan _lastFrameAt;
    private int _animationLoadVersion;
    private bool _isAnimationRendering;
    private bool _isRefreshRunning;
    private bool _refreshPending;
    private bool _isTopProcessRefreshRunning;
    private bool _hasStarted;
    private bool _isDisposed;
    private bool _isSpeechBubbleEnabled;
    private DateTime _nextTopProcessRefreshAt = DateTime.MaxValue;
    private int _resourceUsageValue;
    private ResourceType _selectedResourceType = ResourceType.Memory;
    private CharacterState _characterState;
    private ImageSource? _characterFrame;
    private string _baseSpeechBubbleText = BuildSpeechBubbleHeader(ResourceType.Memory);
    private string _speechBubbleText = BuildSpeechBubbleHeader(ResourceType.Memory);
    private bool _isInteractionMessageVisible;

    public MainViewModel(
        IResourceMonitor monitor,
        TopProcessMonitor topProcessMonitor,
        StateClassifier classifier,
        AnimationController animationController)
    {
        _monitor = monitor;
        _topProcessMonitor = topProcessMonitor;
        _classifier = classifier;
        _animationController = animationController;
        _refreshTimer = new DispatcherTimer
        {
            Interval = ResourceRefreshInterval
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ResourceUsageValue
    {
        get => _resourceUsageValue;
        private set
        {
            if (_resourceUsageValue == value)
            {
                return;
            }

            _resourceUsageValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResourceUsageText));
        }
    }

    public ResourceType SelectedResourceType
    {
        get => _selectedResourceType;
        private set
        {
            if (_selectedResourceType == value)
            {
                return;
            }

            _selectedResourceType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResourceUsageText));
            OnPropertyChanged(nameof(ResourceUsageMaximum));
        }
    }

    public int ResourceUsageMaximum => SelectedResourceType.GetThresholdMaximum();

    public string ResourceUsageText =>
        LocalizationManager.Instance.Format(
            "ResourceReadingFormat",
            SelectedResourceType.GetLocalizedName(),
            SelectedResourceType.FormatReading(ResourceUsageValue));

    public string SpeechBubbleText
    {
        get => _speechBubbleText;
        private set
        {
            if (_speechBubbleText == value)
            {
                return;
            }

            _speechBubbleText = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<CharacterOption> Characters => _animationController.Characters;

    public string CurrentCharacterId => _animationController.CurrentCharacterId;

    public double AnimationSpeedMultiplier => _animationController.SpeedMultiplier;

    public ImageSource? CharacterFrame
    {
        get => _characterFrame;
        private set
        {
            if (Equals(_characterFrame, value))
            {
                return;
            }

            _characterFrame = value;
            OnPropertyChanged();
        }
    }

    public CharacterState CharacterState
    {
        get => _characterState;
        private set
        {
            if (_characterState == value)
            {
                return;
            }

            _characterState = value;
            OnPropertyChanged();
        }
    }

    public void Start()
    {
        _hasStarted = true;
        _animationClock.Restart();
        _lastFrameAt = _animationClock.Elapsed;
        RequestAnimationLoad();
        _refreshTimer.Start();
        ScheduleTopProcessRefresh(TopProcessInitialDelay);
        _ = RefreshAsync();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _refreshTimer.Stop();
        StopAnimationRendering();
        _animationClock.Stop();
    }

    public bool SelectCharacter(string characterId)
    {
        if (!_animationController.SetCharacter(characterId))
        {
            return false;
        }

        _lastFrameAt = _animationClock.Elapsed;
        RequestAnimationLoad();
        OnPropertyChanged(nameof(CurrentCharacterId));
        return true;
    }

    public CharacterOption RegisterCustomCharacter(
        string displayName,
        string sleepingPath,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        CharacterOption character = _animationController.RegisterCustomCharacter(
            displayName,
            sleepingPath,
            standingPath,
            walkingPath,
            runningPath);
        OnPropertyChanged(nameof(Characters));
        return character;
    }

    public CharacterOption UpdateCustomCharacter(
        string characterId,
        string displayName,
        string sleepingPath,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        CharacterOption character = _animationController.UpdateCustomCharacter(
            characterId,
            displayName,
            sleepingPath,
            standingPath,
            walkingPath,
            runningPath);
        _lastFrameAt = _animationClock.Elapsed;
        RequestAnimationLoad();
        OnPropertyChanged(nameof(Characters));
        OnPropertyChanged(nameof(CurrentCharacterId));
        return character;
    }

    public bool DeleteCustomCharacter(string characterId)
    {
        bool deleted = _animationController.DeleteCustomCharacter(characterId);
        if (!deleted)
        {
            return false;
        }

        _lastFrameAt = _animationClock.Elapsed;
        RequestAnimationLoad();
        OnPropertyChanged(nameof(Characters));
        OnPropertyChanged(nameof(CurrentCharacterId));
        return true;
    }

    public bool IsCustomCharacter(string characterId)
    {
        return _animationController.IsCustomCharacter(characterId);
    }

    public CustomCharacterDefinition? GetCustomCharacterDefinition(string characterId)
    {
        return _animationController.GetCustomCharacterDefinition(characterId);
    }

    public bool SetAnimationSpeedMultiplier(double speedMultiplier)
    {
        if (!_animationController.SetSpeedMultiplier(speedMultiplier))
        {
            return false;
        }

        _lastFrameAt = _animationClock.Elapsed;
        UpdateAnimationRendering();
        OnPropertyChanged(nameof(AnimationSpeedMultiplier));
        return true;
    }

    public bool SelectResource(ResourceType resourceType)
    {
        if (SelectedResourceType == resourceType)
        {
            return false;
        }

        SelectedResourceType = resourceType;
        ResourceUsageValue = 0;
        InvalidateTopProcessCache();
        ScheduleTopProcessRefresh(TopProcessInitialDelay);
        if (!_hasStarted)
        {
            DelayInitialTopProcessRefresh();
        }

        return true;
    }

    public void SetSpeechBubbleEnabled(bool isEnabled)
    {
        if (_isSpeechBubbleEnabled == isEnabled)
        {
            return;
        }

        _isSpeechBubbleEnabled = isEnabled;
        if (_isSpeechBubbleEnabled)
        {
            if (_hasStarted)
            {
                InvalidateTopProcessCache();
                ScheduleTopProcessRefresh(TopProcessInitialDelay);
                _ = RefreshAsync();
            }
            else
            {
                DelayInitialTopProcessRefresh();
            }
        }
        else
        {
            _nextTopProcessRefreshAt = DateTime.MaxValue;
        }
    }

    public void ShowInteractionMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _isInteractionMessageVisible = true;
        SpeechBubbleText = message;
    }

    public void ClearInteractionMessage()
    {
        if (!_isInteractionMessageVisible)
        {
            return;
        }

        _isInteractionMessageVisible = false;
        SpeechBubbleText = _baseSpeechBubbleText;
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(ResourceUsageText));
        InvalidateTopProcessCache();
        if (_isSpeechBubbleEnabled)
        {
            ScheduleTopProcessRefresh(TimeSpan.Zero);
            _ = RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshRunning)
        {
            _refreshPending = true;
            return;
        }

        _isRefreshRunning = true;
        try
        {
            do
            {
                _refreshPending = false;
                ResourceType selectedResourceType = SelectedResourceType;
                ResourceRefreshResult? result = await GetRefreshResultAsync(selectedResourceType);
                if (_isDisposed)
                {
                    return;
                }

                if (result is null || result.ResourceType != SelectedResourceType)
                {
                    continue;
                }

                ResourceUsageValue = result.Reading;
                CharacterState nextState = _classifier.Classify(result.ResourceType, result.Reading);
                if (CharacterState != nextState)
                {
                    SetCharacterState(nextState);
                }

                if (ShouldRefreshTopProcesses(selectedResourceType))
                {
                    StartTopProcessRefresh(selectedResourceType);
                }
            } while (_refreshPending);
        }
        finally
        {
            _isRefreshRunning = false;
        }
    }

    private async Task<ResourceRefreshResult?> GetRefreshResultAsync(ResourceType resourceType)
    {
        try
        {
            return await Task.Run(() =>
            {
                int reading = _monitor.GetReading(resourceType);
                return new ResourceRefreshResult(resourceType, reading);
            });
        }
        catch
        {
            // Keep the last known UI state if a platform counter is temporarily unavailable.
            return null;
        }
    }

    private bool ShouldRefreshTopProcesses(ResourceType resourceType)
    {
        if (!_isSpeechBubbleEnabled || _isTopProcessRefreshRunning)
        {
            return false;
        }

        return DateTime.UtcNow >= _nextTopProcessRefreshAt;
    }

    private void StartTopProcessRefresh(ResourceType resourceType)
    {
        _isTopProcessRefreshRunning = true;
        _nextTopProcessRefreshAt = DateTime.UtcNow + TopProcessRefreshInterval;
        _ = RefreshTopProcessesAsync(resourceType);
    }

    private async Task RefreshTopProcessesAsync(ResourceType resourceType)
    {
        try
        {
            IReadOnlyList<ProcessResourceUsage> topProcesses = await Task.Run(() =>
                _topProcessMonitor.GetTopProcesses(resourceType, 2));

            if (_isDisposed
                || !_isSpeechBubbleEnabled
                || resourceType != SelectedResourceType)
            {
                return;
            }

            SetBaseSpeechBubbleText(BuildSpeechBubbleText(resourceType, topProcesses));
        }
        catch
        {
            return;
        }
        finally
        {
            _isTopProcessRefreshRunning = false;
        }
    }

    private void InvalidateTopProcessCache()
    {
        SetBaseSpeechBubbleText(BuildSpeechBubbleHeader(SelectedResourceType));
    }

    public ResourceThresholds GetThresholds(ResourceType resourceType)
    {
        return _classifier.GetThresholds(resourceType);
    }

    public void SetThresholds(ResourceType resourceType, ResourceThresholds thresholds)
    {
        _classifier.SetThresholds(resourceType, thresholds);
        if (SelectedResourceType == resourceType)
        {
            CharacterState nextState = _classifier.Classify(resourceType, ResourceUsageValue);
            if (CharacterState != nextState)
            {
                SetCharacterState(nextState);
            }
        }
    }

    private void DelayInitialTopProcessRefresh()
    {
        if (!_isSpeechBubbleEnabled)
        {
            return;
        }

        ScheduleTopProcessRefresh(TopProcessInitialDelay);
    }

    private void ScheduleTopProcessRefresh(TimeSpan delay)
    {
        if (!_isSpeechBubbleEnabled)
        {
            _nextTopProcessRefreshAt = DateTime.MaxValue;
            return;
        }

        _nextTopProcessRefreshAt = DateTime.UtcNow + delay;
    }

    private void SetBaseSpeechBubbleText(string text)
    {
        _baseSpeechBubbleText = text;
        if (!_isInteractionMessageVisible)
        {
            SpeechBubbleText = text;
        }
    }

    private static string BuildSpeechBubbleText(ResourceType resourceType, IReadOnlyList<ProcessResourceUsage> processes)
    {
        string header = BuildSpeechBubbleHeader(resourceType);
        if (processes.Count == 0)
        {
            return $"{header}\n{LocalizationManager.Instance.Get("SpeechNoProcessData")}";
        }

        IEnumerable<string> lines = processes.Select((process, index) =>
        {
            string processCount = process.ProcessCount > 1 ? $" ({process.ProcessCount})" : string.Empty;
            string usageText = resourceType == ResourceType.Memory
                ? FormatBytes(process.MemoryBytes)
                : $"{process.UsagePercent:0.#}%";

            return $"{index + 1}. {process.DisplayName}{processCount} {usageText}";
        });

        return $"{header}\n{string.Join('\n', lines)}";
    }

    private static string BuildSpeechBubbleHeader(ResourceType resourceType)
    {
        return LocalizationManager.Instance.Format(
            "SpeechVillainHeader",
            resourceType.GetLocalizedName());
    }

    private static string FormatBytes(long bytes)
    {
        const double oneMegabyte = 1024.0 * 1024.0;
        const double oneGigabyte = oneMegabyte * 1024.0;

        return bytes >= oneGigabyte
            ? $"{bytes / oneGigabyte:0.0} GB"
            : $"{bytes / oneMegabyte:0} MB";
    }

    private void SetCharacterState(CharacterState state)
    {
        CharacterState = state;
        _animationController.SetState(state);
        _lastFrameAt = _animationClock.Elapsed;
        RequestAnimationLoad();
    }

    private void RequestAnimationLoad()
    {
        StopAnimationRendering();
        int loadVersion = ++_animationLoadVersion;
        _ = LoadAnimationAsync(loadVersion);
    }

    private async Task LoadAnimationAsync(int loadVersion)
    {
        try
        {
            bool loaded = await _animationController.LoadCurrentAnimationAsync();
            if (_isDisposed || !loaded || loadVersion != _animationLoadVersion)
            {
                return;
            }

            CharacterFrame = _animationController.CurrentFrame;
            _lastFrameAt = _animationClock.Elapsed;
            UpdateAnimationRendering();
        }
        catch
        {
            if (loadVersion == _animationLoadVersion)
            {
                CharacterFrame = null;
            }
        }
    }

    private void UpdateAnimationRendering()
    {
        if (_animationController.IsAnimated)
        {
            StartAnimationRendering();
        }
        else
        {
            StopAnimationRendering();
        }
    }

    private void StartAnimationRendering()
    {
        if (_isAnimationRendering)
        {
            return;
        }

        CompositionTarget.Rendering += OnRendering;
        _isAnimationRendering = true;
    }

    private void StopAnimationRendering()
    {
        if (!_isAnimationRendering)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _isAnimationRendering = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan frameInterval = _animationController.FrameInterval;
        TimeSpan elapsed = _animationClock.Elapsed;
        TimeSpan delta = elapsed - _lastFrameAt;
        if (delta < frameInterval)
        {
            return;
        }

        int framesToAdvance = Math.Max(1, (int)(delta.Ticks / frameInterval.Ticks));
        ImageSource? nextFrame = null;
        for (int i = 0; i < framesToAdvance; i++)
        {
            nextFrame = _animationController.MoveNext();
        }

        _lastFrameAt += TimeSpan.FromTicks(frameInterval.Ticks * framesToAdvance);
        CharacterFrame = nextFrame;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record ResourceRefreshResult(ResourceType ResourceType, int Reading);
}
