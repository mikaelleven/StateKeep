using System.Text;

namespace StateKeep;

public static class Log
{
    private const long MaximumFileSizeBytes = 100 * 1024;
    private const int RetainedFileCount = 3;
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "statekeep.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Verbose(string message) => Write("VERBOSE", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (SyncRoot)
            {
                var entry = $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}";
                RotateIfNeeded(Encoding.UTF8.GetByteCount(entry));
                File.AppendAllText(LogPath, entry, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Logging must never prevent a backup or restore operation.
        }
    }

    private static void RotateIfNeeded(int incomingEntrySize)
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length + incomingEntrySize <= MaximumFileSizeBytes)
        {
            return;
        }

        var oldestPath = GetRotatedPath(RetainedFileCount - 1);
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }

        for (var index = RetainedFileCount - 2; index >= 1; index--)
        {
            var sourcePath = GetRotatedPath(index);
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, GetRotatedPath(index + 1), overwrite: true);
            }
        }

        File.Move(LogPath, GetRotatedPath(1), overwrite: true);
    }

    private static string GetRotatedPath(int index) =>
        Path.Combine(AppContext.BaseDirectory, $"statekeep.{index}.log");
}
