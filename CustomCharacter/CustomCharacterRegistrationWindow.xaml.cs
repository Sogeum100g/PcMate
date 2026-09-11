using System.IO;
using System.Windows;
using Microsoft.Win32;
using PcMate.Localization;
using PcMate.Services;

namespace PcMate.CustomCharacter;

public partial class CustomCharacterRegistrationWindow : Window
{
    public CustomCharacterRegistrationWindow()
    {
        InitializeComponent();
    }

    public CustomCharacterRegistrationWindow(CustomCharacterDefinition definition)
        : this()
    {
        Title = LocalizationManager.Instance.Get("CharacterEditTitle");
        RegisterButton.Content = LocalizationManager.Instance.Get("ActionSave");
        CharacterNameTextBox.Text = definition.DisplayName;
        SleepingPathTextBox.Text = CustomCharacterStore.GetSleepingPath(definition);
        StandingPathTextBox.Text = CustomCharacterStore.GetStandingPath(definition);
        WalkingPathTextBox.Text = CustomCharacterStore.GetWalkingPath(definition);
        RunningPathTextBox.Text = CustomCharacterStore.GetRunningPath(definition);
    }

    public string CharacterName => CharacterNameTextBox.Text.Trim();

    public string StandingImagePath => StandingPathTextBox.Text.Trim();

    public string SleepingImagePath => SleepingPathTextBox.Text.Trim();

    public string WalkingImagePath => WalkingPathTextBox.Text.Trim();

    public string RunningImagePath => RunningPathTextBox.Text.Trim();

    private void OnBrowseStandingClick(object sender, RoutedEventArgs e)
    {
        BrowseImage(StandingPathTextBox);
    }

    private void OnBrowseSleepingClick(object sender, RoutedEventArgs e)
    {
        BrowseImage(SleepingPathTextBox);
    }

    private void OnBrowseWalkingClick(object sender, RoutedEventArgs e)
    {
        BrowseImage(WalkingPathTextBox);
    }

    private void OnBrowseRunningClick(object sender, RoutedEventArgs e)
    {
        BrowseImage(RunningPathTextBox);
    }

    private void OnClearSleepingClick(object sender, RoutedEventArgs e)
    {
        SleepingPathTextBox.Clear();
    }

    private void OnClearStandingClick(object sender, RoutedEventArgs e)
    {
        StandingPathTextBox.Clear();
    }

    private void OnClearWalkingClick(object sender, RoutedEventArgs e)
    {
        WalkingPathTextBox.Clear();
    }

    private void OnClearRunningClick(object sender, RoutedEventArgs e)
    {
        RunningPathTextBox.Clear();
    }

    private void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CharacterName)
            || !HasSelectedImage()
            || !IsOptionalSupportedImagePath(SleepingImagePath)
            || !IsOptionalSupportedImagePath(StandingImagePath)
            || !IsOptionalSupportedImagePath(WalkingImagePath)
            || !IsOptionalSupportedImagePath(RunningImagePath))
        {
            MessageBox.Show(
                this,
                LocalizationManager.Instance.Get("CharacterValidation"),
                "PcMate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private bool HasSelectedImage()
    {
        return !string.IsNullOrWhiteSpace(SleepingImagePath)
            || !string.IsNullOrWhiteSpace(StandingImagePath)
            || !string.IsNullOrWhiteSpace(WalkingImagePath)
            || !string.IsNullOrWhiteSpace(RunningImagePath);
    }

    private static bool IsOptionalSupportedImagePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) || IsSupportedImagePath(path);
    }

    private static void BrowseImage(System.Windows.Controls.TextBox target)
    {
        var dialog = new OpenFileDialog
        {
            Filter = LocalizationManager.Instance.Get("ImageFileFilter"),
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            target.Text = dialog.FileName;
        }
    }

    private static bool IsSupportedImagePath(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        return extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }
}
