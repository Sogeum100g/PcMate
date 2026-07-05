using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using PcMate.Models;
using PcMate.Monitors;
using PcMate.Services;

namespace PcMate.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan TopProcessRefreshInterval = TimeSpan.FromSeconds(3);
    private readonly IResourceMonitor _monitor;
    private readonly TopProcessMonitor _topProcessMonitor;
    private readonly StateClassifier _classifier;
    private readonly AnimationController _animationController;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Stopwatch _animationClock = new();
    private TimeSpan _lastFrameAt;
    private bool _isAnimationRendering;
    private bool _isRefreshRunning;
    private bool _refreshPending;
    private bool _hasStarted;
    private bool _isDisposed;
    private bool _isSpeechBubbleEnabled;
    private DateTime _lastTopProcessRefreshAt = DateTime.MinValue;
    private ResourceType? _lastTopProcessResourceType;
    private IReadOnlyList<ProcessResourceUsage> _lastTopProcesses = [];
    private int _resourceUsagePercent;
    private ResourceType _selectedResourceType = ResourceType.Memory;
    private CharacterState _characterState;
    private ImageSource? _characterFrame;
    private string _speechBubbleText = "Memory TOP";

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
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ResourceUsagePercent
    {
        get => _resourceUsagePercent;
        private set
        {
            if (_resourceUsagePercent == value)
            {
                return;
            }

            _resourceUsagePercent = value;
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
        }
    }

    public string ResourceUsageText => $"{GetResourceLabel(SelectedResourceType)} {ResourceUsagePercent}%";

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
        CharacterFrame = _animationController.CurrentFrame;
        _animationClock.Restart();
        _lastFrameAt = _animationClock.Elapsed;
        UpdateAnimationRendering();
        _refreshTimer.Start();
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

        CharacterFrame = _animationController.CurrentFrame;
        _lastFrameAt = _animationClock.Elapsed;
        UpdateAnimationRendering();
        OnPropertyChanged(nameof(CurrentCharacterId));
        return true;
    }

    public CharacterOption RegisterCustomCharacter(
        string displayName,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        CharacterOption character = _animationController.RegisterCustomCharacter(
            displayName,
            standingPath,
            walkingPath,
            runningPath);
        OnPropertyChanged(nameof(Characters));
        return character;
    }

    public CharacterOption UpdateCustomCharacter(
        string characterId,
        string displayName,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        CharacterOption character = _animationController.UpdateCustomCharacter(
            characterId,
            displayName,
            standingPath,
            walkingPath,
            runningPath);
        CharacterFrame = _animationController.CurrentFrame;
        _lastFrameAt = _animationClock.Elapsed;
        UpdateAnimationRendering();
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

        CharacterFrame = _animationController.CurrentFrame;
        _lastFrameAt = _animationClock.Elapsed;
        UpdateAnimationRendering();
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
        InvalidateTopProcessCache();
        if (_hasStarted)
        {
            _ = RefreshAsync();
        }
        else
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
                _ = RefreshAsync();
            }
            else
            {
                DelayInitialTopProcessRefresh();
            }
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
                bool includeTopProcesses = ShouldRefreshTopProcesses(selectedResourceType);
                ResourceRefreshResult? result = await GetRefreshResultAsync(selectedResourceType, includeTopProcesses);
                if (_isDisposed)
                {
                    return;
                }

                if (result is null || result.ResourceType != SelectedResourceType)
                {
                    continue;
                }

                ResourceUsagePercent = result.UsagePercent;
                UpdateSpeechBubbleText(result);
                CharacterState nextState = _classifier.Classify(result.UsagePercent);
                if (CharacterState != nextState)
                {
                    SetCharacterState(nextState);
                }
            } while (_refreshPending);
        }
        finally
        {
            _isRefreshRunning = false;
        }
    }

    private async Task<ResourceRefreshResult?> GetRefreshResultAsync(ResourceType resourceType, bool includeTopProcesses)
    {
        try
        {
            return await Task.Run(() =>
            {
                int usagePercent = _monitor.GetUsagePercent(resourceType);
                IReadOnlyList<ProcessResourceUsage>? topProcesses = includeTopProcesses
                    ? _topProcessMonitor.GetTopProcesses(resourceType, 2)
                    : null;
                return new ResourceRefreshResult(resourceType, usagePercent, topProcesses);
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
        if (!_isSpeechBubbleEnabled)
        {
            return false;
        }

        return _lastTopProcessResourceType != resourceType
            || DateTime.UtcNow - _lastTopProcessRefreshAt >= TopProcessRefreshInterval;
    }

    private void UpdateSpeechBubbleText(ResourceRefreshResult result)
    {
        if (!_isSpeechBubbleEnabled || result.TopProcesses is null)
        {
            return;
        }

        _lastTopProcessResourceType = result.ResourceType;
        _lastTopProcesses = result.TopProcesses;
        _lastTopProcessRefreshAt = DateTime.UtcNow;
        SpeechBubbleText = BuildSpeechBubbleText(result.ResourceType, _lastTopProcesses);
    }

    private void InvalidateTopProcessCache()
    {
        _lastTopProcessRefreshAt = DateTime.MinValue;
        _lastTopProcessResourceType = null;
        _lastTopProcesses = [];
    }

    private void DelayInitialTopProcessRefresh()
    {
        if (!_isSpeechBubbleEnabled)
        {
            return;
        }

        _lastTopProcessRefreshAt = DateTime.UtcNow;
        _lastTopProcessResourceType = SelectedResourceType;
        _lastTopProcesses = [];
    }

    private static string BuildSpeechBubbleText(ResourceType resourceType, IReadOnlyList<ProcessResourceUsage> processes)
    {
        string resourceLabel = GetResourceLabel(resourceType);
        if (processes.Count == 0)
        {
            return $"{resourceLabel} TOP\nNo process data";
        }

        IEnumerable<string> lines = processes.Select((process, index) =>
        {
            string processCount = process.ProcessCount > 1 ? $" ({process.ProcessCount})" : string.Empty;
            string usageText = resourceType == ResourceType.Memory
                ? FormatBytes(process.MemoryBytes)
                : $"{process.UsagePercent:0.#}%";

            return $"{index + 1}. {process.DisplayName}{processCount} {usageText}";
        });

        return $"{resourceLabel} TOP\n{string.Join('\n', lines)}";
    }

    private static string GetResourceLabel(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Memory => "Memory",
            ResourceType.Cpu => "CPU",
            ResourceType.Gpu => "GPU",
            _ => "Resource"
        };
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
        CharacterFrame = _animationController.CurrentFrame;
        _lastFrameAt = _animationClock.Elapsed;
        UpdateAnimationRendering();
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

    private sealed record ResourceRefreshResult(
        ResourceType ResourceType,
        int UsagePercent,
        IReadOnlyList<ProcessResourceUsage>? TopProcesses);
}
