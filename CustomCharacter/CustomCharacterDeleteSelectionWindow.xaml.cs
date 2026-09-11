using System.Windows;
using PcMate.Localization;
using PcMate.Services;

namespace PcMate.CustomCharacter;

public partial class CustomCharacterDeleteSelectionWindow : Window
{
    private readonly List<DeleteCharacterOption> _characters;

    public CustomCharacterDeleteSelectionWindow(IReadOnlyList<CharacterOption> characters)
    {
        InitializeComponent();
        _characters = characters
            .Select(character => new DeleteCharacterOption(character))
            .ToList();
        CharacterListBox.ItemsSource = _characters;
    }

    public IReadOnlyList<CharacterOption> SelectedCharacters =>
        _characters
            .Where(character => character.IsSelected)
            .Select(character => character.Character)
            .ToList();

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (SelectedCharacters.Count == 0)
        {
            MessageBox.Show(
                this,
                LocalizationManager.Instance.Get("CharacterDeleteSelectOne"),
                "PcMate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private sealed class DeleteCharacterOption(CharacterOption character)
    {
        public CharacterOption Character { get; } = character;

        public bool IsSelected { get; set; }
    }
}
