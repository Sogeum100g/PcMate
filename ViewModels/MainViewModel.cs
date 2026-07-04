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
    private readonly StateClassifier _classifier;
    private readonly DispatcherTimer _refreshTimer;
    private int _memoryUsagePercent;
    private CharacterState _characterState;
    private string _lastUpdatedText = "Waiting";
    private string _errorText = string.Empty;
    private Brush _stateBrush = Brushes.SkyBlue;

    public MainViewModel(IResourceMonitor monitor, StateClassifier classifier)
    {
        _monitor = monitor;
        _classifier = classifier;
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
            OnPropertyChanged(nameof(MemoryText));
        }
    }

    public string MemoryText => $"{MemoryUsagePercent}%";

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
            StateBrush = GetStateBrush(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set
        {
            if (_lastUpdatedText == value)
            {
                return;
            }

            _lastUpdatedText = value;
            OnPropertyChanged();
        }
    }

    public string StatusText => string.IsNullOrWhiteSpace(_errorText)
        ? $"State: {CharacterState}"
        : _errorText;

    public Brush StateBrush
    {
        get => _stateBrush;
        private set
        {
            if (Equals(_stateBrush, value))
            {
                return;
            }

            _stateBrush = value;
            OnPropertyChanged();
        }
    }

    public void Start()
    {
        Refresh();
        _refreshTimer.Start();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
    }

    private void Refresh()
    {
        try
        {
            ResourceSnapshot snapshot = _monitor.GetSnapshot();
            MemoryUsagePercent = snapshot.MemoryUsagePercent;
            CharacterState = _classifier.Classify(snapshot);
            LastUpdatedText = snapshot.CollectedAt.ToString("HH:mm:ss");
            SetError(string.Empty);
        }
        catch (Exception ex)
        {
            SetError($"Memory read failed: {ex.Message}");
        }
    }

    private void SetError(string errorText)
    {
        if (_errorText == errorText)
        {
            return;
        }

        _errorText = errorText;
        OnPropertyChanged(nameof(StatusText));
    }

    private static Brush GetStateBrush(CharacterState state)
    {
        return state switch
        {
            CharacterState.Lying => Brushes.SkyBlue,
            CharacterState.Sitting => Brushes.MediumSeaGreen,
            CharacterState.Walking => Brushes.Goldenrod,
            CharacterState.Running => Brushes.IndianRed,
            _ => Brushes.SkyBlue
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
