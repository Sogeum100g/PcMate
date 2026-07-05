using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace PcMate.Services;

public sealed class CustomCharacterDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public string StandingFileName { get; init; } = "standing.gif";

    public string WalkingFileName { get; init; } = "walking.gif";

    public string RunningFileName { get; init; } = "running.gif";
}

public sealed partial class CustomCharacterStore
{
    public const string StandingState = "standing";
    public const string WalkingState = "walking";
    public const string RunningState = "running";
    private static readonly string[] SupportedExtensions = [".gif", ".png", ".jpg", ".jpeg"];
    private readonly string _rootPath;
    private readonly string _indexPath;

    private CustomCharacterStore(string rootPath)
    {
        _rootPath = rootPath;
        _indexPath = Path.Combine(_rootPath, "characters.json");
    }

    public static CustomCharacterStore CreateDefault()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new CustomCharacterStore(Path.Combine(appData, "PcMate", "custom-characters"));
    }

    public IReadOnlyList<CustomCharacterDefinition> Load()
    {
        try
        {
            if (!File.Exists(_indexPath))
            {
                return [];
            }

            string json = File.ReadAllText(_indexPath);
            List<CustomCharacterDefinition>? definitions = JsonSerializer.Deserialize<List<CustomCharacterDefinition>>(json);
            return definitions?
                .Select(Normalize)
                .Where(IsValidDefinition)
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public CustomCharacterDefinition Register(
        string displayName,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        displayName = NormalizeDisplayName(displayName);
        ValidateImage(standingPath, StandingState);
        ValidateImage(walkingPath, WalkingState);
        ValidateImage(runningPath, RunningState);

        List<CustomCharacterDefinition> definitions = Load().ToList();
        ThrowIfDuplicateDisplayName(displayName, definitions, exceptId: null);

        string id = CreateUniqueId(displayName, definitions);
        string characterDirectory = Path.Combine(_rootPath, id);
        var definition = new CustomCharacterDefinition
        {
            Id = id,
            DisplayName = displayName,
            DirectoryPath = characterDirectory,
            StandingFileName = CopyStateImage(characterDirectory, StandingState, standingPath),
            WalkingFileName = CopyStateImage(characterDirectory, WalkingState, walkingPath),
            RunningFileName = CopyStateImage(characterDirectory, RunningState, runningPath)
        };

        definitions.Add(definition);
        Save(definitions);
        return definition;
    }

    public CustomCharacterDefinition Update(
        string id,
        string displayName,
        string standingPath,
        string walkingPath,
        string runningPath)
    {
        displayName = NormalizeDisplayName(displayName);
        ValidateImage(standingPath, StandingState);
        ValidateImage(walkingPath, WalkingState);
        ValidateImage(runningPath, RunningState);

        List<CustomCharacterDefinition> definitions = Load().ToList();
        int index = definitions.FindIndex(definition => definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("Custom character could not be found.");
        }

        ThrowIfDuplicateDisplayName(displayName, definitions, exceptId: id);

        CustomCharacterDefinition existing = definitions[index];
        var updated = new CustomCharacterDefinition
        {
            Id = existing.Id,
            DisplayName = displayName,
            DirectoryPath = existing.DirectoryPath,
            StandingFileName = CopyStateImage(existing.DirectoryPath, StandingState, standingPath),
            WalkingFileName = CopyStateImage(existing.DirectoryPath, WalkingState, walkingPath),
            RunningFileName = CopyStateImage(existing.DirectoryPath, RunningState, runningPath)
        };

        definitions[index] = updated;
        Save(definitions);
        return updated;
    }

    public void Delete(string id)
    {
        List<CustomCharacterDefinition> definitions = Load().ToList();
        CustomCharacterDefinition? definition = definitions.FirstOrDefault(
            item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            return;
        }

        definitions.Remove(definition);
        Save(definitions);

        string rootPath = Path.GetFullPath(_rootPath);
        string directoryPath = Path.GetFullPath(definition.DirectoryPath);
        if (directoryPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private void Save(IReadOnlyList<CustomCharacterDefinition> definitions)
    {
        Directory.CreateDirectory(_rootPath);
        string json = JsonSerializer.Serialize(definitions.Select(Normalize), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_indexPath, json);
    }

    private static void ValidateImage(string path, string stateName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException($"{stateName} image file is required.");
        }

        string extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{stateName} image must be a GIF, PNG, JPG, or JPEG file.");
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            BitmapDecoder decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                throw new InvalidOperationException($"{stateName} image has no frames.");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{stateName} image could not be read.", exception);
        }
    }

    private static string CopyStateImage(string characterDirectory, string stateName, string sourcePath)
    {
        string stateDirectory = Path.Combine(characterDirectory, stateName);
        Directory.CreateDirectory(stateDirectory);

        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        string fileName = $"{stateName}{extension}";
        string destinationPath = Path.Combine(stateDirectory, fileName);
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        foreach (string existingFile in Directory.GetFiles(stateDirectory, $"{stateName}.*"))
        {
            File.Delete(existingFile);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
        return fileName;
    }

    private static bool IsValidDefinition(CustomCharacterDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.Id)
            && !string.IsNullOrWhiteSpace(definition.DisplayName)
            && Directory.Exists(definition.DirectoryPath)
            && File.Exists(GetStatePath(definition, StandingState, definition.StandingFileName))
            && File.Exists(GetStatePath(definition, WalkingState, definition.WalkingFileName))
            && File.Exists(GetStatePath(definition, RunningState, definition.RunningFileName));
    }

    public static string GetStandingPath(CustomCharacterDefinition definition)
    {
        return GetStatePath(definition, StandingState, definition.StandingFileName);
    }

    public static string GetWalkingPath(CustomCharacterDefinition definition)
    {
        return GetStatePath(definition, WalkingState, definition.WalkingFileName);
    }

    public static string GetRunningPath(CustomCharacterDefinition definition)
    {
        return GetStatePath(definition, RunningState, definition.RunningFileName);
    }

    private static string GetStatePath(CustomCharacterDefinition definition, string stateName, string fileName)
    {
        return Path.Combine(definition.DirectoryPath, stateName, fileName);
    }

    private static CustomCharacterDefinition Normalize(CustomCharacterDefinition definition)
    {
        return new CustomCharacterDefinition
        {
            Id = definition.Id,
            DisplayName = definition.DisplayName,
            DirectoryPath = definition.DirectoryPath,
            StandingFileName = string.IsNullOrWhiteSpace(definition.StandingFileName) ? "standing.gif" : definition.StandingFileName,
            WalkingFileName = string.IsNullOrWhiteSpace(definition.WalkingFileName) ? "walking.gif" : definition.WalkingFileName,
            RunningFileName = string.IsNullOrWhiteSpace(definition.RunningFileName) ? "running.gif" : definition.RunningFileName
        };
    }

    private static string NormalizeDisplayName(string displayName)
    {
        displayName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Character name is required.");
        }

        return displayName;
    }

    private static void ThrowIfDuplicateDisplayName(
        string displayName,
        IEnumerable<CustomCharacterDefinition> definitions,
        string? exceptId)
    {
        if (definitions.Any(definition =>
                !definition.Id.Equals(exceptId, StringComparison.OrdinalIgnoreCase)
                && definition.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A custom character with the same name already exists.");
        }
    }

    private static string CreateUniqueId(string displayName, IReadOnlyCollection<CustomCharacterDefinition> definitions)
    {
        string slug = Slugify(displayName);
        string baseId = $"custom_{slug}";
        string id = baseId;
        int suffix = 2;
        while (definitions.Any(definition => definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            id = $"{baseId}_{suffix}";
            suffix++;
        }

        return id;
    }

    private static string Slugify(string value)
    {
        string slug = SlugRegex()
            .Replace(value.Trim().ToLowerInvariant(), "_")
            .Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "character" : slug;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
