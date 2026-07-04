using System.IO;
using System.Text.Json;

namespace PcMate.Services;

public sealed record WindowPlacement(double Left, double Top, double Width, double Height);

public sealed class WindowPlacementStore
{
    private readonly string _settingsPath;

    private WindowPlacementStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public static WindowPlacementStore CreateDefault()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new WindowPlacementStore(Path.Combine(appData, "PcMate", "window-placement.json"));
    }

    public WindowPlacement? Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            string json = File.ReadAllText(_settingsPath);
            WindowPlacement? placement = JsonSerializer.Deserialize<WindowPlacement>(json);
            return IsValid(placement) ? placement : null;
        }
        catch
        {
            return null;
        }
    }

    public void Save(WindowPlacement placement)
    {
        if (!IsValid(placement))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        string json = JsonSerializer.Serialize(placement, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    private static bool IsValid(WindowPlacement? placement)
    {
        return placement is not null
            && double.IsFinite(placement.Left)
            && double.IsFinite(placement.Top)
            && double.IsFinite(placement.Width)
            && double.IsFinite(placement.Height)
            && placement.Width > 0
            && placement.Height > 0;
    }
}
