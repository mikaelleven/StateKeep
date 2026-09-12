using System.Reflection;
using System.Text.RegularExpressions;

namespace StateKeep;

internal static class Program
{
    private const int Success = 0;
    private const int UsageError = 2;
    private const string Version = "0.1.0";

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            WriteHelp(includeConfigurationWarning: true);
            return Success;
        }

        var arguments = new Arguments(args);

        if (arguments.Has("--help", "-h", "/?"))
        {
            WriteHelp(arguments.Command);
            return Success;
        }

        if (arguments.Has("--version", "-v"))
        {
            Console.WriteLine(GetVersion());
            return Success;
        }

        if (arguments.Command is null)
        {
            WriteError("A command is required.");
            return UsageError;
        }

        if (!Commands.Contains(arguments.Command))
        {
            WriteError($"Unknown command '{arguments.Command}'. Use 'statekeep --help' for usage.");
            return UsageError;
        }

        if (string.Equals(arguments.Command, "setup", StringComparison.OrdinalIgnoreCase))
        {
            return Setup(arguments.Positionals.Skip(1).FirstOrDefault());
        }

        if (string.Equals(arguments.Command, "status", StringComparison.OrdinalIgnoreCase))
        {
            return Status();
        }

        if (string.Equals(arguments.Command, "validate", StringComparison.OrdinalIgnoreCase))
        {
            return Validate();
        }

        WarnIfBackupPathIsNotConfigured();

        if (!arguments.Silent)
        {
            Console.WriteLine($"The '{arguments.Command}' command is not implemented yet.");
        }

        return Success;
    }

    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        "backup",
        "restore",
        "list",
        "status",
        "validate",
        "install",
        "setup"
    };

    private static string GetVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? $"statekeep {Version}"
            : $"statekeep {informationalVersion}";
    }

    private static void WriteHelp(string? command = null, bool includeConfigurationWarning = false)
    {
        if (string.Equals(command, "install", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Usage: statekeep install task");
            Console.WriteLine();
            Console.WriteLine("Install or update the scheduled backup task.");
            return;
        }

        Console.WriteLine("StateKeep - preserve application settings in a local backup folder.");

        if (includeConfigurationWarning)
        {
            WriteConfigurationWarning();
        }

        Console.WriteLine();
        Console.WriteLine("Usage: statekeep <command> [arguments] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  backup [app]       Back up all applications or one application");
        Console.WriteLine("  restore [app]      Restore one application");
        Console.WriteLine("  restore --all      Restore all applications");
        Console.WriteLine("  list               List known applications");
        Console.WriteLine("  status             Show current status and configuration");
        Console.WriteLine("  validate           Validate configuration");
        Console.WriteLine("  setup [path]       Configure the backup path");
        Console.WriteLine("  install task       Install or update the scheduled backup task");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help         Show help");
        Console.WriteLine("  -v, --version      Show version");
        Console.WriteLine("      --silent       Suppress normal console output");
    }

    private static int Setup(string? requestedPath)
    {
        var configPath = GetConfigPath();
        var recommendedPath = requestedPath is null ? FindRecommendedCloudPath() : null;
        var backupPath = requestedPath;

        if (string.IsNullOrWhiteSpace(backupPath))
        {
            if (recommendedPath is not null)
            {
                Console.WriteLine($"Recommended backup path: {recommendedPath}");
                Console.Write("Backup path [press Enter to use the recommendation]: ");
                var enteredPath = Console.ReadLine();
                backupPath = string.IsNullOrWhiteSpace(enteredPath) ? recommendedPath : enteredPath.Trim();
            }
            else
            {
                Console.Write("Backup path: ");
                backupPath = Console.ReadLine()?.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(backupPath))
        {
            WriteError("A backup path is required.");
            return UsageError;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, $"version: 1{Environment.NewLine}backupPath: {QuoteYamlValue(backupPath)}{Environment.NewLine}");
            Console.WriteLine($"Configured backup path: {backupPath}");
            return Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            WriteError($"Could not write configuration: {exception.Message}");
            return UsageError;
        }
    }

    private static string GetConfigPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StateKeep",
        "config.yaml");

    private static string? FindRecommendedCloudPath()
    {
        var candidates = new (string Token, string? Value)[]
        {
            ("%ONEDRIVE%", Environment.GetEnvironmentVariable("ONEDRIVE")),
            ("%DROPBOX%", Environment.GetEnvironmentVariable("DROPBOX")),
            ("%GOOGLEDRIVE%", Environment.GetEnvironmentVariable("GOOGLEDRIVE"))
        };

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates = candidates
            .Select(candidate => (candidate.Token, Value: ExistingDirectory(candidate.Value)))
            .Concat(new[]
            {
                ("%ONEDRIVE%", ExistingDirectory(Path.Combine(userProfile, "OneDrive"))),
                ("%DROPBOX%", ExistingDirectory(Path.Combine(userProfile, "Dropbox"))),
                ("%GOOGLEDRIVE%", ExistingDirectory(Path.Combine(userProfile, "Google Drive")))
            })
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (candidate.Value is not null)
            {
                return $"{candidate.Token}\\.StateKeep";
            }
        }

        return null;
    }

    private static string? ExistingDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? path : null;

    private static string QuoteYamlValue(string value) =>
        $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"") }\"";

    private static int Status()
    {
        var configPath = GetConfigPath();
        var config = ReadConfiguration(configPath);

        Console.WriteLine("StateKeep status");
        Console.WriteLine($"Configuration: {configPath}");

        if (!config.Exists)
        {
            Console.WriteLine("Configuration status: not found");
            Console.WriteLine("Backup path: not configured");
            return Success;
        }

        Console.WriteLine("Configuration status: found");
        if (config.BackupPath is null)
        {
            Console.WriteLine("Backup path: not configured");
            return Success;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(config.BackupPath);
        Console.WriteLine($"Backup path: {config.BackupPath}");
        Console.WriteLine($"Resolved path: {expandedPath}");
        Console.WriteLine($"Path status: {(Directory.Exists(expandedPath) ? "exists" : "does not exist")}");
        return Success;
    }

    private static int Validate()
    {
        var configPath = GetConfigPath();
        var config = ReadConfiguration(configPath);
        var errors = new List<string>();

        if (!config.Exists)
        {
            errors.Add($"Configuration file was not found: {configPath}");
        }
        else
        {
            if (config.Version is null)
            {
                errors.Add("Missing required field: version");
            }
            else if (config.Version != "1")
            {
                errors.Add($"Unsupported configuration version: {config.Version}");
            }

            if (string.IsNullOrWhiteSpace(config.BackupPath))
            {
                errors.Add("Missing required field: backupPath");
            }
            else
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(config.BackupPath);
                if (expandedPath.Contains('%'))
                {
                    errors.Add($"Backup path contains an undefined environment variable: {config.BackupPath}");
                }
            }
        }

        if (errors.Count == 0)
        {
            Console.WriteLine("Configuration is valid.");
            return Success;
        }

        Console.Error.WriteLine("Configuration is invalid:");
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"- {error}");
        }

        return UsageError;
    }

    private static Configuration ReadConfiguration(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new Configuration(false, null, null);
        }

        string? version = null;
        string? backupPath = null;
        foreach (var line in File.ReadLines(configPath))
        {
            var versionMatch = Regex.Match(line, @"^\s*version\s*:\s*(?<value>[^#]+?)\s*(?:#.*)?$");
            if (versionMatch.Success)
            {
                version = Unquote(versionMatch.Groups["value"].Value.Trim());
            }

            var backupPathMatch = Regex.Match(line, @"^\s*(?:backupPath|backup_path)\s*:\s*(?<value>.+?)\s*(?:#.*)?$");
            if (backupPathMatch.Success)
            {
                backupPath = Unquote(backupPathMatch.Groups["value"].Value.Trim());
            }
        }

        return new Configuration(true, version, backupPath);
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2 || (value[0] != '"' && value[0] != '\'') || value[^1] != value[0])
        {
            return value;
        }

        var content = value[1..^1];
        return value[0] == '"'
            ? content.Replace("\\\\", "\\").Replace("\\\"", "\"")
            : content.Replace("''", "'");
    }

    private sealed record Configuration(bool Exists, string? Version, string? BackupPath);

    private static void WarnIfBackupPathIsNotConfigured()
    {
        var configPath = GetConfigPath();

        if (!File.Exists(configPath))
        {
            WriteConfigurationWarning();
            return;
        }

        var hasBackupPath = File.ReadLines(configPath)
            .Select(line => Regex.Match(line, @"^\s*(?:backupPath|backup_path)\s*:\s*(?<value>.+?)\s*(?:#.*)?$"))
            .Where(match => match.Success)
            .Select(match => match.Groups["value"].Value.Trim().Trim('"', '\''))
            .Any(value => value.Length > 0);

        if (!hasBackupPath)
        {
            WriteConfigurationWarning();
        }
    }

    private static void WriteConfigurationWarning()
    {
        const string yellow = "\u001b[33m";
        const string reset = "\u001b[0m";

        Console.WriteLine($"{yellow}Warning: No valid app configuration with a backup path was found.{reset}");
        Console.WriteLine($"{yellow}Run \"statekeep setup\" to configure StateKeep before using backup or restore commands.{reset}");
    }

    private static void WriteError(string message) => Console.Error.WriteLine($"Error: {message}");

    private sealed class Arguments
    {
        private readonly string[] values;

        public Arguments(string[] args)
        {
            values = args;
            Command = args.FirstOrDefault(value => !value.StartsWith('-'))?.ToLowerInvariant();
            Silent = Has("--silent");
        }

        public string? Command { get; }
        public bool Silent { get; }
        public IReadOnlyList<string> Positionals => values.Where(value => !value.StartsWith('-')).ToArray();

        public bool Has(params string[] options) => values.Any(value => options.Contains(value, StringComparer.OrdinalIgnoreCase));
    }
}
