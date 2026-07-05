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
    private readonly AppSettingsStore _appSettingsStore;
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _windowPlacementStore = WindowPlacementStore.CreateDefault();
        _appSettingsStore = AppSettingsStore.CreateDefault();
        CustomCharacterStore customCharacterStore = CustomCharacterStore.CreateDefault();
        _viewModel = new MainViewModel(
            new SystemResourceMonitor(),
            new TopProcessMonitor(),
            new StateClassifier(),
            new AnimationController(Path.Combine(AppContext.BaseDirectory, "assets", "characters"), customCharacterStore));

        ApplySavedWindowPlacement();
        DataContext = _viewModel;
        RebuildCharacterMenu();
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

        ApplySavedAppSettings();
        Loaded += (_, _) => _viewModel.Start();
        Closing += (_, _) => SaveSettings();
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
        ApplyResourceBarVisibility(ResourceBarMenuItem.IsChecked);
    }

    private void OnSpeechBubbleClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetSpeechBubbleEnabled(SpeechBubbleMenuItem.IsChecked);
        ApplySpeechBubbleVisibility(SpeechBubbleMenuItem.IsChecked);
        UpdateCharacterOverlayPlacement();
    }

    private void OnImageBorderClick(object sender, RoutedEventArgs e)
    {
        ApplyImageBorderVisibility(ImageBorderMenuItem.IsChecked);
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

        RebuildCharacterMenu();
    }

    private void OnRegisterCustomCharacterClick(object sender, RoutedEventArgs e)
    {
        var registrationWindow = new CustomCharacterRegistrationWindow
        {
            Owner = this
        };

        if (registrationWindow.ShowDialog() != true)
        {
            return;
        }

        try
        {
            CharacterOption character = _viewModel.RegisterCustomCharacter(
                registrationWindow.CharacterName,
                registrationWindow.StandingImagePath,
                registrationWindow.WalkingImagePath,
                registrationWindow.RunningImagePath);
            RebuildCharacterMenu();
            _viewModel.SelectCharacter(character.Id);
            UpdateCharacterMenuChecks(character.Id);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "PcMate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnEditCustomCharacterClick(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<CharacterOption> customCharacters = GetCustomCharacters();
        if (customCharacters.Count == 0)
        {
            ShowNoCustomCharactersMessage();
            return;
        }

        var selectionWindow = new CustomCharacterEditSelectionWindow(customCharacters)
        {
            Owner = this
        };
        if (selectionWindow.ShowDialog() != true || selectionWindow.SelectedCharacterId is null)
        {
            return;
        }

        string characterId = selectionWindow.SelectedCharacterId;
        CustomCharacterDefinition? definition = _viewModel.GetCustomCharacterDefinition(characterId);
        if (definition is null)
        {
            return;
        }

        var registrationWindow = new CustomCharacterRegistrationWindow(definition)
        {
            Owner = this
        };

        if (registrationWindow.ShowDialog() != true)
        {
            return;
        }

        try
        {
            CharacterOption character = _viewModel.UpdateCustomCharacter(
                characterId,
                registrationWindow.CharacterName,
                registrationWindow.StandingImagePath,
                registrationWindow.WalkingImagePath,
                registrationWindow.RunningImagePath);
            RebuildCharacterMenu();
            UpdateCharacterMenuChecks(character.Id);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "PcMate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnDeleteCustomCharacterClick(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<CharacterOption> customCharacters = GetCustomCharacters();
        if (customCharacters.Count == 0)
        {
            ShowNoCustomCharactersMessage();
            return;
        }

        var selectionWindow = new CustomCharacterDeleteSelectionWindow(customCharacters)
        {
            Owner = this
        };
        if (selectionWindow.ShowDialog() != true)
        {
            return;
        }

        IReadOnlyList<CharacterOption> selectedCharacters = selectionWindow.SelectedCharacters;
        string selectedNames = string.Join(", ", selectedCharacters.Select(character => character.DisplayName));
        MessageBoxResult result = MessageBox.Show(
            this,
            $"Delete {selectedCharacters.Count} custom character(s)?\n{selectedNames}",
            "PcMate",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        bool deletedAny = false;
        foreach (CharacterOption character in selectedCharacters)
        {
            deletedAny |= _viewModel.DeleteCustomCharacter(character.Id);
        }

        if (deletedAny)
        {
            RebuildCharacterMenu();
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
        foreach (MenuItem menuItem in CharacterMenuItem.Items.OfType<MenuItem>())
        {
            if (menuItem.Tag is string menuCharacterId)
            {
                menuItem.IsChecked = menuCharacterId == characterId;
            }
        }
    }

    private void RebuildCharacterMenu()
    {
        CharacterMenuItem.Items.Clear();
        foreach (CharacterOption character in _viewModel.Characters)
        {
            var menuItem = new MenuItem
            {
                Header = character.DisplayName,
                IsCheckable = true,
                Tag = character.Id
            };
            menuItem.Click += OnCharacterMenuItemClick;
            CharacterMenuItem.Items.Add(menuItem);
        }

        CharacterMenuItem.Items.Add(new Separator());
        var registerMenuItem = new MenuItem
        {
            Header = "Register custom character..."
        };
        registerMenuItem.Click += OnRegisterCustomCharacterClick;
        CharacterMenuItem.Items.Add(registerMenuItem);

        bool hasCustomCharacters = GetCustomCharacters().Count > 0;
        var editMenuItem = new MenuItem
        {
            Header = "Edit custom character...",
            IsEnabled = hasCustomCharacters
        };
        editMenuItem.Click += OnEditCustomCharacterClick;
        CharacterMenuItem.Items.Add(editMenuItem);

        var deleteMenuItem = new MenuItem
        {
            Header = "Delete custom characters...",
            IsEnabled = hasCustomCharacters
        };
        deleteMenuItem.Click += OnDeleteCustomCharacterClick;
        CharacterMenuItem.Items.Add(deleteMenuItem);

        UpdateCharacterMenuChecks(_viewModel.CurrentCharacterId);
    }

    private IReadOnlyList<CharacterOption> GetCustomCharacters()
    {
        return _viewModel.Characters
            .Where(character => character.IsCustom)
            .ToList();
    }

    private void ShowNoCustomCharactersMessage()
    {
        MessageBox.Show(
            this,
            "No custom characters are registered.",
            "PcMate",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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

    private void ApplyResourceBarVisibility(bool isVisible)
    {
        ResourceBarPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplySpeechBubbleVisibility(bool isVisible)
    {
        SpeechBubble.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyImageBorderVisibility(bool isVisible)
    {
        CharacterImageBoundary.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplySavedAppSettings()
    {
        AppSettings settings = _appSettingsStore.Load();

        Topmost = settings.AlwaysOnTop;
        AlwaysOnTopMenuItem.IsChecked = settings.AlwaysOnTop;

        ResourceBarMenuItem.IsChecked = settings.ShowResourceBar;
        ApplyResourceBarVisibility(settings.ShowResourceBar);

        SpeechBubbleMenuItem.IsChecked = settings.ShowSpeechBubble;
        _viewModel.SetSpeechBubbleEnabled(settings.ShowSpeechBubble);
        ApplySpeechBubbleVisibility(settings.ShowSpeechBubble);

        ImageBorderMenuItem.IsChecked = settings.ShowImageBorder;
        ApplyImageBorderVisibility(settings.ShowImageBorder);

        SpeechBubbleBody.Width = Math.Clamp(settings.SpeechBubbleWidth, MinSpeechBubbleWidth, MaxSpeechBubbleWidth);
        SpeechBubbleBody.Height = Math.Clamp(settings.SpeechBubbleHeight, MinSpeechBubbleHeight, MaxSpeechBubbleHeight);

        if (_viewModel.SelectCharacter(settings.CharacterId))
        {
            UpdateCharacterMenuChecks(settings.CharacterId);
        }
        else
        {
            UpdateCharacterMenuChecks(_viewModel.CurrentCharacterId);
        }

        _viewModel.SelectResource(settings.SelectedResourceType);
        UpdateResourceMenuChecks(_viewModel.SelectedResourceType);

        _viewModel.SetAnimationSpeedMultiplier(settings.AnimationSpeedMultiplier);
        UpdateAnimationSpeedMenuChecks(_viewModel.AnimationSpeedMultiplier);

        UpdateCharacterOverlayPlacement();
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

    private void SaveSettings()
    {
        _windowPlacementStore.Save(new WindowPlacement(Left, Top, Width, Height));
        _appSettingsStore.Save(new AppSettings
        {
            AlwaysOnTop = AlwaysOnTopMenuItem.IsChecked,
            ShowResourceBar = ResourceBarMenuItem.IsChecked,
            ShowSpeechBubble = SpeechBubbleMenuItem.IsChecked,
            ShowImageBorder = ImageBorderMenuItem.IsChecked,
            SelectedResourceType = _viewModel.SelectedResourceType,
            CharacterId = _viewModel.CurrentCharacterId,
            AnimationSpeedMultiplier = _viewModel.AnimationSpeedMultiplier,
            SpeechBubbleWidth = SpeechBubbleBody.ActualWidth > 0 ? SpeechBubbleBody.ActualWidth : SpeechBubbleBody.Width,
            SpeechBubbleHeight = SpeechBubbleBody.ActualHeight > 0 ? SpeechBubbleBody.ActualHeight : SpeechBubbleBody.Height
        });
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
