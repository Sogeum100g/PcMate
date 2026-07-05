using System.Globalization;
using System.Windows;
using PcMate.Models;

namespace PcMate;

public partial class ResourceThresholdSettingsWindow : Window
{
    public ResourceThresholdSettingsWindow(ResourceType resourceType, ResourceThresholds thresholds)
    {
        InitializeComponent();

        ResourceThresholds normalizedThresholds = thresholds.Normalize();
        Title = $"{GetResourceLabel(resourceType)} Thresholds";
        DescriptionTextBlock.Text = $"{GetResourceLabel(resourceType)} usage changes the character state at these percentages.";
        SittingTextBox.Text = normalizedThresholds.SittingPercent.ToString(CultureInfo.InvariantCulture);
        WalkingTextBox.Text = normalizedThresholds.WalkingPercent.ToString(CultureInfo.InvariantCulture);
        RunningTextBox.Text = normalizedThresholds.RunningPercent.ToString(CultureInfo.InvariantCulture);
    }

    public ResourceThresholds Thresholds { get; private set; } = ResourceThresholds.Default;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPercent(SittingTextBox.Text, out int sittingPercent)
            || !TryReadPercent(WalkingTextBox.Text, out int walkingPercent)
            || !TryReadPercent(RunningTextBox.Text, out int runningPercent)
            || sittingPercent >= walkingPercent
            || walkingPercent >= runningPercent)
        {
            MessageBox.Show(
                this,
                "Enter three increasing values between 1 and 100.",
                "PcMate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Thresholds = new ResourceThresholds
        {
            SittingPercent = sittingPercent,
            WalkingPercent = walkingPercent,
            RunningPercent = runningPercent
        };
        DialogResult = true;
    }

    private static bool TryReadPercent(string text, out int value)
    {
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value is >= 1 and <= 100;
    }

    private static string GetResourceLabel(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Memory => "Memory",
            ResourceType.Cpu => "CPU",
            ResourceType.Gpu => "GPU",
            _ => "Resource"
        };
    }
}
