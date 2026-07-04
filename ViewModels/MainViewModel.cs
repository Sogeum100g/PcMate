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
    private readonly IResourceMonitor _monitor;
    private readonly TopProcessMonitor _topProcessMonitor;
    private readonly StateClassifier _classifier;
    private readonly AnimationController _animationController;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Stopwatch _animationClock = new();
    private TimeSpan _lastFrameAt;
    private bool _isAnimationRendering;
    private int _memoryUsagePercent;
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
        _refreshTimer.Tick += (_, _) => Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int MemoryUsagePercent
    {
        get => _memoryUsagePercent;
        private set
        {
            if (_memoryUsagePercent == value)
            {
                return;
            }

            _memoryUsagePercent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResourceUsageText));
        }
    }

    public string ResourceUsageText => $"Memory {MemoryUsagePercent}%";

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
        Refresh();
        CharacterFrame = _animationController.CurrentFrame;
        _animationClock.Restart();
        _lastFrameAt = _animationClock.Elapsed;
        StartAnimationRendering();
        _refreshTimer.Start();
    }

    public void Dispose()
    {
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
        OnPropertyChanged(nameof(CurrentCharacterId));
        return true;
    }

    public bool SetAnimationSpeedMultiplier(double speedMultiplier)
    {
        if (!_animationController.SetSpeedMultiplier(speedMultiplier))
        {
            return false;
        }

        _lastFrameAt = _animationClock.Elapsed;
        OnPropertyChanged(nameof(AnimationSpeedMultiplier));
        return true;
    }

    private void Refresh()
    {
        try
        {
            ResourceSnapshot snapshot = _monitor.GetSnapshot();
            MemoryUsagePercent = snapshot.MemoryUsagePercent;
            SpeechBubbleText = BuildSpeechBubbleText(_topProcessMonitor.GetTopMemoryProcesses(2));
            CharacterState nextState = _classifier.Classify(snapshot);
            if (CharacterState != nextState)
            {
                SetCharacterState(nextState);
            }
        }
        catch
        {
            // Keep the last known UI state if the OS memory query fails.
        }
    }

    private static string BuildSpeechBubbleText(IReadOnlyList<ProcessResourceUsage> processes)
    {
        if (processes.Count == 0)
        {
            return "Memory TOP\nNo process data";
        }

        IEnumerable<string> lines = processes.Select((process, index) =>
        {
            string processCount = process.ProcessCount > 1 ? $" ({process.ProcessCount})" : string.Empty;
            return $"{index + 1}. {process.DisplayName}{processCount} {FormatBytes(process.MemoryBytes)}";
        });

        return $"Memory TOP\n{string.Join('\n', lines)}";
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
}
