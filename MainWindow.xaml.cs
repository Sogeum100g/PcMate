using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PcMate.Monitors;
using PcMate.Services;
using PcMate.ViewModels;

namespace PcMate;

public partial class MainWindow : Window
{
    private const double DefaultWidth = 220;
    private const double DefaultHeight = 190;
    private const double MinSpeechBubbleWidth = 110;
    private const double MinSpeechBubbleHeight = 48;
    private const double MaxSpeechBubbleWidth = 420;
    private const double MaxSpeechBubbleHeight = 240;
    private readonly WindowPlacementStore _windowPlacementStore;
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _windowPlacementStore = WindowPlacementStore.CreateDefault();
        _viewModel = new MainViewModel(
            new MemoryMonitor(),
            new TopProcessMonitor(),
            new StateClassifier(),
            new AnimationController(Path.Combine(AppContext.BaseDirectory, "assets", "characters")));

        ApplySavedWindowPlacement();
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Start();
        Closing += (_, _) => SaveWindowPlacement();
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnCharacterMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            e.Handled = true;
            DragMove();
        }
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        double nextWidth = Math.Max(MinWidth, Width - e.HorizontalChange);
        double appliedHorizontalChange = Width - nextWidth;
        Left += appliedHorizontalChange;
        Width = nextWidth;
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private void OnAlwaysOnTopClick(object sender, RoutedEventArgs e)
    {
        Topmost = AlwaysOnTopMenuItem.IsChecked;
    }

    private void OnResourceBarClick(object sender, RoutedEventArgs e)
    {
        ResourceBarPanel.Visibility = ResourceBarMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnSpeechBubbleClick(object sender, RoutedEventArgs e)
    {
        SpeechBubble.Visibility = SpeechBubbleMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnSpeechBubbleResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        SpeechBubbleBody.Width = Math.Clamp(
            SpeechBubbleBody.ActualWidth + e.HorizontalChange,
            MinSpeechBubbleWidth,
            MaxSpeechBubbleWidth);

        SpeechBubbleBody.Height = Math.Clamp(
            SpeechBubbleBody.ActualHeight + e.VerticalChange,
            MinSpeechBubbleHeight,
            MaxSpeechBubbleHeight);
    }

    private void OnCharacterMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string characterId)
        {
            return;
        }

        if (_viewModel.SelectCharacter(characterId))
        {
            UpdateCharacterMenuChecks(characterId);
        }
        else
        {
            UpdateCharacterMenuChecks(_viewModel.CurrentCharacterId);
        }
    }

    private void OnAnimationSpeedMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem
            || menuItem.Tag is not string speedText
            || !double.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double speedMultiplier))
        {
            return;
        }

        _viewModel.SetAnimationSpeedMultiplier(speedMultiplier);
        UpdateAnimationSpeedMenuChecks(_viewModel.AnimationSpeedMultiplier);
    }

    private void OnResetSizeClick(object sender, RoutedEventArgs e)
    {
        Width = DefaultWidth;
        Height = DefaultHeight;
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateCharacterMenuChecks(string characterId)
    {
        TailsCharacterMenuItem.IsChecked = characterId == "tails";
        RedParrotCharacterMenuItem.IsChecked = characterId == "red_parrot";
        KakaoRyanCharacterMenuItem.IsChecked = characterId == "kakao_ryan";
    }

    private void UpdateAnimationSpeedMenuChecks(double speedMultiplier)
    {
        SpeedHalfMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 0.5);
        SpeedThreeQuarterMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 0.75);
        SpeedNormalMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 1.0);
        SpeedOneAndHalfMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 1.5);
        SpeedDoubleMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 2.0);
    }

    private static bool IsSpeedSelected(double current, double target)
    {
        return Math.Abs(current - target) < 0.001;
    }

    private void ApplySavedWindowPlacement()
    {
        WindowPlacement? placement = _windowPlacementStore.Load();
        if (placement is null || !IsPlacementVisible(placement))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = Math.Max(MinWidth, placement.Width);
        Height = Math.Max(MinHeight, placement.Height);
    }

    private void SaveWindowPlacement()
    {
        _windowPlacementStore.Save(new WindowPlacement(Left, Top, Width, Height));
    }

    private static bool IsPlacementVisible(WindowPlacement placement)
    {
        double right = placement.Left + Math.Max(placement.Width, 1);
        double bottom = placement.Top + Math.Max(placement.Height, 1);
        double virtualRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        double virtualBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

        return right > SystemParameters.VirtualScreenLeft
            && bottom > SystemParameters.VirtualScreenTop
            && placement.Left < virtualRight
            && placement.Top < virtualBottom;
    }
}
