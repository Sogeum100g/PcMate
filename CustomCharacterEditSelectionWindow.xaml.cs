using System.Windows;
using PcMate.Services;

namespace PcMate;

public partial class CustomCharacterEditSelectionWindow : Window
{
    public CustomCharacterEditSelectionWindow(IReadOnlyList<CharacterOption> characters)
    {
        InitializeComponent();
        CharacterListBox.ItemsSource = characters;
        if (characters.Count > 0)
        {
            CharacterListBox.SelectedIndex = 0;
        }
    }

    public string? SelectedCharacterId =>
        CharacterListBox.SelectedItem is CharacterOption character ? character.Id : null;

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCharacterId is null)
        {
            MessageBox.Show(
                this,
                "Choose a custom character to edit.",
                "PcMate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
