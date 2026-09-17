using System.Text.Json;

namespace StateKeep;

internal sealed record BackupAttempt(
    DateTimeOffset Date,
    string Status,
    int NumAppsUpdated,
    string Type);

internal sealed record BackupState(
    BackupAttempt? LastSuccessfulAttempt,
    BackupAttempt LastAttempt);

internal static class BackupStateFile
{
    private const string FileName = "state.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Write(string backupRoot, BackupAttempt attempt)
    {
        var path = Path.Combine(backupRoot, FileName);
        var previousState = Read(path);
        var state = new BackupState(
            attempt.Status == "success" ? attempt : previousState?.LastSuccessfulAttempt,
            attempt);
        var temporaryPath = path + ".tmp";

        Directory.CreateDirectory(backupRoot);
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, SerializerOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static BackupState? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BackupState>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException)
        {
            Log.Error($"Could not read backup state file '{path}'; it will be replaced.");
            return null;
        }
    }
}
