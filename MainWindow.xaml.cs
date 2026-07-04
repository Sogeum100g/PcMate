using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PcMate.Models;
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
            new SystemResourceMonitor(),
            new TopProcessMonitor(),
            new StateClassifier(),
            new AnimationController(Path.Combine(AppContext.BaseDirectory, "assets", "characters")));

        ApplySavedWindowPlacement();
        DataContext = _viewModel;
        CharacterImage.SizeChanged += (_, _) => UpdateCharacterOverlayPlacement();
        SpeechBubble.SizeChanged += (_, _) => UpdateCharacterOverlayPlacement();
        CharacterStage.SizeChanged += (_, _) => UpdateCharacterOverlayPlacement();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CharacterFrame))
            {
                Dispatcher.BeginInvoke((Action)UpdateCharacterOverlayPlacement);
            }
        };

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
        _viewModel.SetSpeechBubbleEnabled(SpeechBubbleMenuItem.IsChecked);
        SpeechBubble.Visibility = SpeechBubbleMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateCharacterOverlayPlacement();
    }

    private void OnImageBorderClick(object sender, RoutedEventArgs e)
    {
        CharacterImageBoundary.Visibility = ImageBorderMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateCharacterOverlayPlacement();
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
        UpdateCharacterOverlayPlacement();
    }

    private void OnResourceMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem
            || menuItem.Tag is not string resourceText
            || !Enum.TryParse(resourceText, out ResourceType resourceType))
        {
            return;
        }

        _viewModel.SelectResource(resourceType);
        UpdateResourceMenuChecks(_viewModel.SelectedResourceType);
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

    private void UpdateResourceMenuChecks(ResourceType resourceType)
    {
        MemoryResourceMenuItem.IsChecked = resourceType == ResourceType.Memory;
        CpuResourceMenuItem.IsChecked = resourceType == ResourceType.Cpu;
        GpuResourceMenuItem.IsChecked = resourceType == ResourceType.Gpu;
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

    private void UpdateCharacterOverlayPlacement()
    {
        if (!TryGetDisplayedImageBounds(out double imageWidth, out double imageHeight, out double imageTop))
        {
            return;
        }

        if (CharacterImageBoundary.Visibility == Visibility.Visible)
        {
            CharacterImageBoundary.Width = imageWidth;
            CharacterImageBoundary.Height = imageHeight;
        }

        if (SpeechBubble.Visibility == Visibility.Visible)
        {
            double speechBubbleTop = Math.Max(0, imageTop - SpeechBubble.ActualHeight + 2);
            SpeechBubble.Margin = new Thickness(4, speechBubbleTop, 4, 0);
        }
    }

    private bool TryGetDisplayedImageBounds(out double width, out double height, out double top)
    {
        width = 0;
        height = 0;
        top = 0;

        ImageSource? source = CharacterImage.Source;
        if (source is null
            || source.Width <= 0
            || source.Height <= 0
            || CharacterImage.ActualWidth <= 0
            || CharacterImage.ActualHeight <= 0)
        {
            return false;
        }

        double scale = Math.Min(CharacterImage.ActualWidth / source.Width, CharacterImage.ActualHeight / source.Height);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            return false;
        }

        width = source.Width * scale;
        height = source.Height * scale;
        top = (CharacterImage.ActualHeight - height) / 2;
        return true;
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
