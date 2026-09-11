using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PcMate.Onboarding;

public partial class OnboardingWindow : Window
{
    private const string HelpImagePath =
        "assets/characters/blob/blob-running/blob-running.gif";

    public OnboardingWindow()
    {
        InitializeComponent();
        HelpImage.Source = LoadHelpImage();
    }

    private void OnDoneClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The pointer may be released while Windows begins the drag operation.
        }
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private static BitmapImage? LoadHelpImage()
    {
        try
        {
            string imagePath = Path.Combine(AppContext.BaseDirectory, HelpImagePath);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imagePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
