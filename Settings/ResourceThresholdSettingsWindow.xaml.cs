using System.Globalization;
using System.Windows;
using PcMate.Localization;
using PcMate.Models;

namespace PcMate.Settings;

public partial class ResourceThresholdSettingsWindow : Window
{
    private readonly ResourceType _resourceType;
    private readonly int _maximumValue;

    public ResourceThresholdSettingsWindow(ResourceType resourceType, ResourceThresholds thresholds)
    {
        InitializeComponent();

        _resourceType = resourceType;
        _maximumValue = resourceType.GetThresholdMaximum();
        ResourceThresholds normalizedThresholds = thresholds.Normalize(_maximumValue);
        string resourceName = resourceType.GetLocalizedName();
        string unit = resourceType.GetThresholdUnit();

        Title = LocalizationManager.Instance.Format("ThresholdWindowTitle", resourceName);
        DescriptionTextBlock.Text = resourceType == ResourceType.Network
            ? LocalizationManager.Instance.Get("ThresholdNetworkDescription")
            : LocalizationManager.Instance.Format("ThresholdUsageDescription", resourceName);
        SittingTextBox.Text = normalizedThresholds.SittingPercent.ToString(CultureInfo.InvariantCulture);
        WalkingTextBox.Text = normalizedThresholds.WalkingPercent.ToString(CultureInfo.InvariantCulture);
        RunningTextBox.Text = normalizedThresholds.RunningPercent.ToString(CultureInfo.InvariantCulture);
        SittingUnitTextBlock.Text = $" {unit}";
        WalkingUnitTextBlock.Text = $" {unit}";
        RunningUnitTextBlock.Text = $" {unit}";
        Thresholds = normalizedThresholds;
    }

    public ResourceThresholds Thresholds { get; private set; } = ResourceThresholds.Default;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadValue(SittingTextBox.Text, out int sittingPercent)
            || !TryReadValue(WalkingTextBox.Text, out int walkingPercent)
            || !TryReadValue(RunningTextBox.Text, out int runningPercent)
            || sittingPercent >= walkingPercent
            || walkingPercent >= runningPercent)
        {
            MessageBox.Show(
                this,
                LocalizationManager.Instance.Format(
                    "ThresholdValidation",
                    _maximumValue,
                    _resourceType.GetThresholdUnit()),
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

    private bool TryReadValue(string text, out int value)
    {
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= 1
            && value <= _maximumValue;
    }
}
