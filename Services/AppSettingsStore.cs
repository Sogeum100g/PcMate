using System.IO;
using System.Text.Json;
using PcMate.Localization;
using PcMate.Models;

namespace PcMate.Services;

public sealed class AppSettings
{
    public bool AlwaysOnTop { get; init; } = true;

    public bool ShowResourceBar { get; init; } = true;

    public bool ShowSpeechBubble { get; init; }

    public bool ShowImageBorder { get; init; }

    public bool UseDarkMode { get; init; }

    public bool HasSeenOnboarding { get; init; }

    public string Language { get; init; } = LocalizationManager.SystemLanguage;

    public ResourceType SelectedResourceType { get; init; } = ResourceType.Memory;

    public string CharacterId { get; init; } = "blob";

    public double AnimationSpeedMultiplier { get; init; } = 1.0;

    public ResourceThresholds MemoryThresholds { get; init; } = ResourceThresholds.Default;

    public ResourceThresholds CpuThresholds { get; init; } = ResourceThresholds.Default;

    public ResourceThresholds GpuThresholds { get; init; } = ResourceThresholds.Default;

    public ResourceThresholds NetworkThresholds { get; init; } = ResourceThresholds.NetworkDefault;
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
            UseDarkMode = settings.UseDarkMode,
            HasSeenOnboarding = settings.HasSeenOnboarding,
            Language = LocalizationManager.NormalizeLanguage(settings.Language),
            SelectedResourceType = Enum.IsDefined(settings.SelectedResourceType)
                ? settings.SelectedResourceType
                : ResourceType.Memory,
            CharacterId = string.IsNullOrWhiteSpace(settings.CharacterId)
                ? "blob"
                : settings.CharacterId,
            AnimationSpeedMultiplier = double.IsFinite(settings.AnimationSpeedMultiplier)
                ? settings.AnimationSpeedMultiplier
                : 1.0,
            MemoryThresholds = NormalizeThresholds(settings.MemoryThresholds, ResourceType.Memory),
            CpuThresholds = NormalizeThresholds(settings.CpuThresholds, ResourceType.Cpu),
            GpuThresholds = NormalizeThresholds(settings.GpuThresholds, ResourceType.Gpu),
            NetworkThresholds = NormalizeThresholds(settings.NetworkThresholds, ResourceType.Network)
        };
    }

    private static ResourceThresholds NormalizeThresholds(ResourceThresholds? thresholds, ResourceType resourceType)
    {
        return (thresholds ?? resourceType.GetDefaultThresholds())
            .Normalize(resourceType.GetThresholdMaximum());
    }
}
