using System.IO;
using System.Windows;
using Microsoft.Win32;
using PcMate.Services;

namespace PcMate;

public partial class CustomCharacterRegistrationWindow : Window
{
    public CustomCharacterRegistrationWindow()
    {
        InitializeComponent();
    }

    public CustomCharacterRegistrationWindow(CustomCharacterDefinition definition)
        : this()
    {
        Title = "Edit Character";
        RegisterButton.Content = "Save";
        CharacterNameTextBox.Text = definition.DisplayName;
        StandingPathTextBox.Text = CustomCharacterStore.GetStandingPath(definition);
        WalkingPathTextBox.Text = CustomCharacterStore.GetWalkingPath(definition);
        RunningPathTextBox.Text = CustomCharacterStore.GetRunningPath(definition);
    }

    public string CharacterName => CharacterNameTextBox.Text.Trim();

    public string StandingImagePath => StandingPathTextBox.Text;

    public string WalkingImagePath => WalkingPathTextBox.Text;

    public string RunningImagePath => RunningPathTextBox.Text;

    private void OnBrowseStandingClick(object sender, RoutedEventArgs e)
    {
        BrowseImage(StandingPathTextBox);
    }

    private void OnBrowseWalkingClick(object sender, RoutedEventArgs e)
    {
        BrowseImage(WalkingPathTextBox);
    }

    private void OnBrowseRunningClick(object sender, RoutedEventArgs e)
    {
        BrowseImage(RunningPathTextBox);
    }

    private void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CharacterName)
            || !IsSupportedImagePath(StandingImagePath)
            || !IsSupportedImagePath(WalkingImagePath)
            || !IsSupportedImagePath(RunningImagePath))
        {
            MessageBox.Show(
                this,
                "Enter a character name and choose standing, walking, and running image files.",
                "PcMate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private static void BrowseImage(System.Windows.Controls.TextBox target)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.gif;*.png;*.jpg;*.jpeg)|*.gif;*.png;*.jpg;*.jpeg",
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
