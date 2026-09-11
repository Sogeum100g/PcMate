using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using PcMate.CustomCharacter;
using PcMate.Localization;
using PcMate.Models;
using PcMate.Monitors;
using PcMate.Services;
using PcMate.Settings;
using PcMate.ViewModels;

namespace PcMate;

public partial class MainWindow : Window
{
    private const double DefaultWidth = 220;
    private const double DefaultHeight = 190;
    private const double ResourceBarImageGap = 8;
    private static readonly TimeSpan InteractionBubbleDuration = TimeSpan.FromSeconds(3);
    private static readonly string[] CharacterClickMessageKeys = ["InteractionHello"];
    private static readonly Color DarkTextColor = Color.FromRgb(0x11, 0x18, 0x27);
    private static readonly Color LightTextColor = Color.FromRgb(0xF9, 0xFA, 0xFB);
    private readonly WindowPlacementStore _windowPlacementStore;
    private readonly AppSettingsStore _appSettingsStore;
    private readonly MainViewModel _viewModel;
    private CancellationTokenSource? _interactionBubbleCancellation;
    private Point? _characterMouseDownPosition;
    private bool _isCharacterDragStarted;
    private bool _hasSeenOnboarding;
    private string _language = LocalizationManager.SystemLanguage;
    private double? _heightWithoutSpeechBubble;
    private double _speechBubbleLayoutHeight;

    public MainWindow()
    {
        InitializeComponent();
        ApplyApplicationIcon();

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
        CharacterImage.SizeChanged += (_, _) => UpdateCharacterOverlayPlacement();
        SpeechBubble.SizeChanged += OnSpeechBubbleSizeChanged;
        CharacterStage.SizeChanged += (_, _) => UpdateCharacterOverlayPlacement();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CharacterFrame))
            {
                Dispatcher.BeginInvoke((Action)UpdateCharacterOverlayPlacement);
            }
        };

        ApplySavedAppSettings();
        RebuildCharacterMenu();
        Loaded += (_, _) =>
        {
            EnsureSpeechBubbleWindowExpansion();
            _viewModel.Start();

            if (!_hasSeenOnboarding)
            {
                Dispatcher.BeginInvoke((Action)ShowTutorial);
            }
        };
        Closing += (_, _) => SaveSettings();
        Closed += (_, _) =>
        {
            CancelInteractionBubble();
            _viewModel.Dispose();
        };
    }

    private void OnCharacterMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        _characterMouseDownPosition = e.GetPosition(this);
        _isCharacterDragStarted = false;
        CharacterImage.CaptureMouse();
        e.Handled = true;
    }

    private void OnCharacterMouseMove(object sender, MouseEventArgs e)
    {
        if (_characterMouseDownPosition is not Point mouseDownPosition
            || e.LeftButton != MouseButtonState.Pressed
            || _isCharacterDragStarted)
        {
            return;
        }

        Point currentPosition = e.GetPosition(this);
        double horizontalDistance = Math.Abs(currentPosition.X - mouseDownPosition.X);
        double verticalDistance = Math.Abs(currentPosition.Y - mouseDownPosition.Y);
        if (horizontalDistance < SystemParameters.MinimumHorizontalDragDistance
            && verticalDistance < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _isCharacterDragStarted = true;
        CharacterImage.ReleaseMouseCapture();
        e.Handled = true;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse button may have been released as the drag threshold was crossed.
        }
        finally
        {
            ResetCharacterPointerState();
        }
    }

    private void OnCharacterMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _characterMouseDownPosition is null)
        {
            return;
        }

        bool shouldInteract = !_isCharacterDragStarted;
        CharacterImage.ReleaseMouseCapture();
        ResetCharacterPointerState();
        e.Handled = true;

        if (shouldInteract)
        {
            ShowRandomCharacterMessage();
        }
    }

    private void ResetCharacterPointerState()
    {
        _characterMouseDownPosition = null;
        _isCharacterDragStarted = false;
    }

    private async void ShowRandomCharacterMessage()
    {
        CancelInteractionBubble();
        var cancellation = new CancellationTokenSource();
        _interactionBubbleCancellation = cancellation;

        string messageKey = CharacterClickMessageKeys[Random.Shared.Next(CharacterClickMessageKeys.Length)];
        string message = LocalizationManager.Instance.Get(messageKey);
        _viewModel.ShowInteractionMessage(message);
        ApplySpeechBubbleVisibility(isVisible: true);
        UpdateCharacterOverlayPlacement();

        try
        {
            await Task.Delay(InteractionBubbleDuration, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_interactionBubbleCancellation, cancellation))
        {
            return;
        }

        _interactionBubbleCancellation = null;
        cancellation.Dispose();
        _viewModel.ClearInteractionMessage();
        ApplySpeechBubbleVisibility(SpeechBubbleMenuItem.IsChecked);
        UpdateCharacterOverlayPlacement();
    }

    private void CancelInteractionBubble()
    {
        CancellationTokenSource? cancellation = _interactionBubbleCancellation;
        _interactionBubbleCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void ApplyApplicationIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "app.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        double nextWidth = Math.Max(MinWidth, Width - e.HorizontalChange);
        double appliedHorizontalChange = Width - nextWidth;
        Left += appliedHorizontalChange;
        Width = nextWidth;

        if (_heightWithoutSpeechBubble is double heightWithoutSpeechBubble)
        {
            double nextBaseHeight = Math.Max(MinHeight, heightWithoutSpeechBubble + e.VerticalChange);
            Height += nextBaseHeight - heightWithoutSpeechBubble;
            _heightWithoutSpeechBubble = nextBaseHeight;
        }
        else
        {
            Height = Math.Max(MinHeight, Height + e.VerticalChange);
        }
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

    private void OnDarkModeClick(object sender, RoutedEventArgs e)
    {
        ApplyResourceTextTheme(DarkModeMenuItem.IsChecked);
    }

    private void OnLanguageMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string language })
        {
            return;
        }

        _language = LocalizationManager.NormalizeLanguage(language);
        LocalizationManager.Instance.SetLanguage(_language);
        UpdateLanguageMenuChecks();
        _viewModel.RefreshLocalizedText();
        RebuildCharacterMenu();
        UpdateThresholdMenuHeaders();
        SaveSettings();
    }

    private void OnSpeechBubbleSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke((Action)(() =>
        {
            double previousLayoutHeight = _speechBubbleLayoutHeight;
            UpdateLayout();
            UpdateSpeechBubbleWindowExpansion(previousLayoutHeight);
            UpdateCharacterOverlayPlacement();
        }));
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

    private void OnThresholdMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem
            || menuItem.Tag is not string resourceText
            || !Enum.TryParse(resourceText, out ResourceType resourceType))
        {
            return;
        }

        var thresholdWindow = new ResourceThresholdSettingsWindow(
            resourceType,
            _viewModel.GetThresholds(resourceType))
        {
            Owner = this
        };

        if (thresholdWindow.ShowDialog() != true)
        {
            return;
        }

        _viewModel.SetThresholds(resourceType, thresholdWindow.Thresholds);
        UpdateThresholdMenuHeaders();
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
                registrationWindow.SleepingImagePath,
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
                registrationWindow.SleepingImagePath,
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
            LocalizationManager.Instance.Format(
                "CharacterDeleteConfirm",
                selectedCharacters.Count,
                selectedNames),
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
        if (_heightWithoutSpeechBubble is double heightWithoutSpeechBubble)
        {
            Height += DefaultHeight - heightWithoutSpeechBubble;
            _heightWithoutSpeechBubble = DefaultHeight;
        }
        else
        {
            Height = DefaultHeight;
        }
    }

    private void OnHelpClick(object sender, RoutedEventArgs e)
    {
        ShowTutorial();
    }

    private void ShowTutorial()
    {
        Onboarding.OnboardingWindow tutorialWindow = new()
        {
            Owner = this
        };

        tutorialWindow.ShowDialog();

        if (!_hasSeenOnboarding)
        {
            _hasSeenOnboarding = true;
            SaveSettings();
        }
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
            Header = LocalizationManager.Instance.Get("MenuRegisterCharacter")
        };
        registerMenuItem.Click += OnRegisterCustomCharacterClick;
        CharacterMenuItem.Items.Add(registerMenuItem);

        bool hasCustomCharacters = GetCustomCharacters().Count > 0;
        var editMenuItem = new MenuItem
        {
            Header = LocalizationManager.Instance.Get("MenuEditCharacter"),
            IsEnabled = hasCustomCharacters
        };
        editMenuItem.Click += OnEditCustomCharacterClick;
        CharacterMenuItem.Items.Add(editMenuItem);

        var deleteMenuItem = new MenuItem
        {
            Header = LocalizationManager.Instance.Get("MenuDeleteCharacters"),
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
            LocalizationManager.Instance.Get("CharacterNoneRegistered"),
            "PcMate",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateResourceMenuChecks(ResourceType resourceType)
    {
        MemoryResourceMenuItem.IsChecked = resourceType == ResourceType.Memory;
        CpuResourceMenuItem.IsChecked = resourceType == ResourceType.Cpu;
        GpuResourceMenuItem.IsChecked = resourceType == ResourceType.Gpu;
        NetworkResourceMenuItem.IsChecked = resourceType == ResourceType.Network;
    }

    private void UpdateThresholdMenuHeaders()
    {
        MemoryThresholdMenuItem.Header = BuildThresholdMenuHeader(ResourceType.Memory);
        CpuThresholdMenuItem.Header = BuildThresholdMenuHeader(ResourceType.Cpu);
        GpuThresholdMenuItem.Header = BuildThresholdMenuHeader(ResourceType.Gpu);
        NetworkThresholdMenuItem.Header = BuildThresholdMenuHeader(ResourceType.Network);
    }

    private string BuildThresholdMenuHeader(ResourceType resourceType)
    {
        ResourceThresholds thresholds = _viewModel.GetThresholds(resourceType);
        return LocalizationManager.Instance.Format(
            "ThresholdMenuFormat",
            resourceType.GetLocalizedName(),
            thresholds.SittingPercent,
            thresholds.WalkingPercent,
            thresholds.RunningPercent,
            resourceType.GetThresholdUnit());
    }

    private void UpdateLanguageMenuChecks()
    {
        SystemLanguageMenuItem.IsChecked = _language == LocalizationManager.SystemLanguage;
        EnglishLanguageMenuItem.IsChecked = _language == LocalizationManager.EnglishLanguage;
        KoreanLanguageMenuItem.IsChecked = _language == LocalizationManager.KoreanLanguage;
    }

    private void UpdateAnimationSpeedMenuChecks(double speedMultiplier)
    {
        SpeedHalfMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 0.5);
        SpeedThreeQuarterMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 0.75);
        SpeedNormalMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 1.0);
        SpeedOneAndHalfMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 1.5);
        SpeedDoubleMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 2.0);
        SpeedTripleMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 3.0);
        SpeedQuadrupleMenuItem.IsChecked = IsSpeedSelected(speedMultiplier, 4.0);
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
        if (isVisible)
        {
            if (SpeechBubble.Visibility != Visibility.Visible)
            {
                SpeechBubble.Visibility = Visibility.Visible;
            }

            EnsureSpeechBubbleWindowExpansion();
            return;
        }

        CollapseSpeechBubbleWindowExpansion();
        SpeechBubble.Visibility = Visibility.Collapsed;
    }

    private void EnsureSpeechBubbleWindowExpansion()
    {
        if (SpeechBubble.Visibility != Visibility.Visible || _heightWithoutSpeechBubble is not null)
        {
            return;
        }

        UpdateLayout();
        double layoutHeight = SpeechBubbleHostRow.ActualHeight;
        if (layoutHeight <= 0)
        {
            return;
        }

        _heightWithoutSpeechBubble = Height;
        _speechBubbleLayoutHeight = layoutHeight;
        Height += layoutHeight;
        if (double.IsFinite(Top))
        {
            Top -= layoutHeight;
        }
    }

    private void UpdateSpeechBubbleWindowExpansion(double previousLayoutHeight)
    {
        if (_heightWithoutSpeechBubble is null || SpeechBubble.Visibility != Visibility.Visible)
        {
            return;
        }

        double nextLayoutHeight = SpeechBubbleHostRow.ActualHeight;
        double layoutDelta = nextLayoutHeight - previousLayoutHeight;
        _speechBubbleLayoutHeight = nextLayoutHeight;
        if (Math.Abs(layoutDelta) < 0.01)
        {
            return;
        }

        Height += layoutDelta;
        if (double.IsFinite(Top))
        {
            Top -= layoutDelta;
        }
    }

    private void CollapseSpeechBubbleWindowExpansion()
    {
        if (_heightWithoutSpeechBubble is not double heightWithoutSpeechBubble)
        {
            return;
        }

        double layoutHeight = _speechBubbleLayoutHeight;
        Height = heightWithoutSpeechBubble;
        if (double.IsFinite(Top))
        {
            Top += layoutHeight;
        }

        _heightWithoutSpeechBubble = null;
        _speechBubbleLayoutHeight = 0;
    }

    private void ApplyImageBorderVisibility(bool isVisible)
    {
        CharacterImageBoundary.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyResourceTextTheme(bool useDarkMode)
    {
        ResourceUsageTextBlock.Foreground = new SolidColorBrush(
            useDarkMode ? LightTextColor : DarkTextColor);
        ResourceUsageTextBlock.Effect = new DropShadowEffect
        {
            BlurRadius = 2,
            Direction = 315,
            Opacity = 0.9,
            ShadowDepth = 1,
            Color = useDarkMode ? DarkTextColor : LightTextColor
        };
    }

    private void ApplySavedAppSettings()
    {
        AppSettings settings = _appSettingsStore.Load();
        _hasSeenOnboarding = settings.HasSeenOnboarding;
        _language = LocalizationManager.NormalizeLanguage(settings.Language);
        LocalizationManager.Instance.SetLanguage(_language);
        UpdateLanguageMenuChecks();
        _viewModel.RefreshLocalizedText();

        Topmost = settings.AlwaysOnTop;
        AlwaysOnTopMenuItem.IsChecked = settings.AlwaysOnTop;

        ResourceBarMenuItem.IsChecked = settings.ShowResourceBar;
        ApplyResourceBarVisibility(settings.ShowResourceBar);

        SpeechBubbleMenuItem.IsChecked = settings.ShowSpeechBubble;
        _viewModel.SetSpeechBubbleEnabled(settings.ShowSpeechBubble);
        ApplySpeechBubbleVisibility(settings.ShowSpeechBubble);

        ImageBorderMenuItem.IsChecked = settings.ShowImageBorder;
        ApplyImageBorderVisibility(settings.ShowImageBorder);

        DarkModeMenuItem.IsChecked = settings.UseDarkMode;
        ApplyResourceTextTheme(settings.UseDarkMode);

        _viewModel.SetThresholds(ResourceType.Memory, settings.MemoryThresholds);
        _viewModel.SetThresholds(ResourceType.Cpu, settings.CpuThresholds);
        _viewModel.SetThresholds(ResourceType.Gpu, settings.GpuThresholds);
        _viewModel.SetThresholds(ResourceType.Network, settings.NetworkThresholds);
        UpdateThresholdMenuHeaders();

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

        // CharacterImage sits centered in the image row, so any leftover height below the
        // displayed image (before the row/margin ends) is dead space that pushes the resource
        // bar away from the character as the window grows taller. Pull it up to compensate.
        double bottomSlack = Math.Max(0, CharacterImage.ActualHeight - imageTop - imageHeight)
            + CharacterStage.Margin.Bottom;
        double resourceBarShift = Math.Max(0, bottomSlack - ResourceBarImageGap);
        ResourceBarPanel.Margin = new Thickness(8, -resourceBarShift, 8, 4);
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
        double placementTop = Top;
        double placementHeight = Height;
        if (_heightWithoutSpeechBubble is double heightWithoutSpeechBubble)
        {
            placementTop += _speechBubbleLayoutHeight;
            placementHeight = heightWithoutSpeechBubble;
        }

        _windowPlacementStore.Save(new WindowPlacement(Left, placementTop, Width, placementHeight));
        _appSettingsStore.Save(new AppSettings
        {
            AlwaysOnTop = AlwaysOnTopMenuItem.IsChecked,
            ShowResourceBar = ResourceBarMenuItem.IsChecked,
            ShowSpeechBubble = SpeechBubbleMenuItem.IsChecked,
            ShowImageBorder = ImageBorderMenuItem.IsChecked,
            UseDarkMode = DarkModeMenuItem.IsChecked,
            HasSeenOnboarding = _hasSeenOnboarding,
            Language = _language,
            SelectedResourceType = _viewModel.SelectedResourceType,
            CharacterId = _viewModel.CurrentCharacterId,
            AnimationSpeedMultiplier = _viewModel.AnimationSpeedMultiplier,
            MemoryThresholds = _viewModel.GetThresholds(ResourceType.Memory),
            CpuThresholds = _viewModel.GetThresholds(ResourceType.Cpu),
            GpuThresholds = _viewModel.GetThresholds(ResourceType.Gpu),
            NetworkThresholds = _viewModel.GetThresholds(ResourceType.Network)
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
