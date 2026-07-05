using System.IO;
using System.Text.Json;
using PcMate.Models;

namespace PcMate.Services;

public sealed class AppSettings
{
    public bool AlwaysOnTop { get; init; } = true;

    public bool ShowResourceBar { get; init; } = true;

    public bool ShowSpeechBubble { get; init; }

    public bool ShowImageBorder { get; init; }

    public ResourceType SelectedResourceType { get; init; } = ResourceType.Memory;

    public string CharacterId { get; init; } = "tails";

    public double AnimationSpeedMultiplier { get; init; } = 1.0;

    public double SpeechBubbleWidth { get; init; } = 180;

    public double SpeechBubbleHeight { get; init; } = 64;

    public ResourceThresholds MemoryThresholds { get; init; } = ResourceThresholds.Default;

    public ResourceThresholds CpuThresholds { get; init; } = ResourceThresholds.Default;

    public ResourceThresholds GpuThresholds { get; init; } = ResourceThresholds.Default;
}

public sealed class AppSettingsStore
{
    private readonly string _settingsPath;

    private AppSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public static AppSettingsStore CreateDefault()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppSettingsStore(Path.Combine(appData, "PcMate", "app-settings.json"));
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_settingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings is null ? new AppSettings() : Normalize(settings);
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        string json = JsonSerializer.Serialize(Normalize(settings), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        return new AppSettings
        {
            AlwaysOnTop = settings.AlwaysOnTop,
            ShowResourceBar = settings.ShowResourceBar,
            ShowSpeechBubble = settings.ShowSpeechBubble,
            ShowImageBorder = settings.ShowImageBorder,
            SelectedResourceType = Enum.IsDefined(settings.SelectedResourceType)
                ? settings.SelectedResourceType
                : ResourceType.Memory,
            CharacterId = string.IsNullOrWhiteSpace(settings.CharacterId)
                ? "tails"
                : settings.CharacterId,
            AnimationSpeedMultiplier = double.IsFinite(settings.AnimationSpeedMultiplier)
                ? settings.AnimationSpeedMultiplier
                : 1.0,
            SpeechBubbleWidth = double.IsFinite(settings.SpeechBubbleWidth) && settings.SpeechBubbleWidth > 0
                ? settings.SpeechBubbleWidth
                : 180,
            SpeechBubbleHeight = double.IsFinite(settings.SpeechBubbleHeight) && settings.SpeechBubbleHeight > 0
                ? settings.SpeechBubbleHeight
                : 64,
            MemoryThresholds = NormalizeThresholds(settings.MemoryThresholds),
            CpuThresholds = NormalizeThresholds(settings.CpuThresholds),
            GpuThresholds = NormalizeThresholds(settings.GpuThresholds)
        };
    }

    private static ResourceThresholds NormalizeThresholds(ResourceThresholds? thresholds)
    {
        return (thresholds ?? ResourceThresholds.Default).Normalize();
    }
}
