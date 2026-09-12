using System.Reflection;
using System.Security.Cryptography;
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

        if (string.Equals(arguments.Command, "backup", StringComparison.OrdinalIgnoreCase))
        {
            return Backup(arguments);
        }

        if (string.Equals(arguments.Command, "status", StringComparison.OrdinalIgnoreCase))
        {
            return Status();
        }

        if (string.Equals(arguments.Command, "validate", StringComparison.OrdinalIgnoreCase))
        {
            return Validate();
        }

        if (string.Equals(arguments.Command, "list", StringComparison.OrdinalIgnoreCase))
        {
            return ListApplications();
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
        Console.WriteLine("      --verbose      Write copied files to stdout");
        Console.WriteLine("      --dryrun       Check files without copying");
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
            var deviceId = EnsureDeviceId();
            File.WriteAllText(configPath, $"version: 1{Environment.NewLine}backupPath: {QuoteYamlValue(backupPath)}{Environment.NewLine}");
            Console.WriteLine($"Configured backup path: {backupPath}");
            Console.WriteLine($"Device ID: {deviceId}");
            return Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            WriteError($"Could not write configuration: {exception.Message}");
            return UsageError;
        }
    }

    private static string GetStateDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StateKeep");

    private static string GetConfigPath() => Path.Combine(GetStateDirectory(), "config.yaml");

    private static string GetDeviceIdPath() => Path.Combine(GetStateDirectory(), "device-id");

    private static string EnsureDeviceId()
    {
        var deviceIdPath = GetDeviceIdPath();
        if (File.Exists(deviceIdPath))
        {
            var existingDeviceId = File.ReadAllText(deviceIdPath).Trim();
            if (existingDeviceId.Length > 0)
            {
                return existingDeviceId;
            }
        }

        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var deviceId = string.Create(10, alphabet, static (buffer, characters) =>
        {
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
        });

        Directory.CreateDirectory(GetStateDirectory());
        File.WriteAllText(deviceIdPath, deviceId + Environment.NewLine);
        return deviceId;
    }

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

    private static int Backup(Arguments arguments)
    {
        var configuration = ReadConfiguration(GetConfigPath());
        if (!configuration.Exists || string.IsNullOrWhiteSpace(configuration.BackupPath))
        {
            WriteError("A configured backup path is required. Run \"statekeep setup\" first.");
            return UsageError;
        }

        var appDirectory = Path.Combine(AppContext.BaseDirectory, "apps");
        if (!Directory.Exists(appDirectory))
        {
            WriteError($"Application definition directory was not found: {appDirectory}");
            return UsageError;
        }

        var requestedApp = arguments.Positionals.Skip(1).FirstOrDefault();
        var applications = Directory.EnumerateFiles(appDirectory, "*.yaml")
            .Concat(Directory.EnumerateFiles(appDirectory, "*.yml"))
            .Select(ReadBackupDefinition)
            .Where(app => requestedApp is null || string.Equals(app.Id, requestedApp, StringComparison.OrdinalIgnoreCase))
            .OrderBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var backupRoot = Path.Combine(Environment.ExpandEnvironmentVariables(configuration.BackupPath),
            $"{Environment.MachineName}_{EnsureDeviceId()}", "apps");
        var scanned = 0;
        var successful = 0;
        var copied = 0;

        foreach (var app in applications)
        {
            scanned++;
            var appCopied = 0;
            var failed = false;
            foreach (var root in app.Roots)
            {
                var spinner = new ProgressLine(
                    arguments.Silent,
                    $"Scanning {app.Name} ({root.Name})",
                    arguments.Verbose || requestedApp is not null);
                try
                {
                    spinner.Start();
                    var source = root.Paths
                        .Select(path => Environment.ExpandEnvironmentVariables(path))
                        .FirstOrDefault(path => Directory.Exists(path) && EvidenceMatches(path, root.Evidence));
                    if (source is null)
                    {
                        spinner.Complete(false, "not found");
                        continue;
                    }

                    var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                        .Where(file => MatchesFilters(Path.GetRelativePath(source, file), app.Include, app.Exclude))
                        .ToArray();
                    var destination = Path.Combine(backupRoot, app.Id, root.Name);
                    foreach (var file in files)
                    {
                        var relative = Path.GetRelativePath(source, file);
                        spinner.Detail(relative);
                        if (arguments.Verbose && arguments.DryRun)
                        {
                            spinner.WouldCopy(relative);
                        }

                        var target = Path.Combine(destination, relative);
                        if (!arguments.DryRun)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            File.Copy(file, target, true);
                        }
                        appCopied++;
                    }
                    spinner.Complete(true, $"{appCopied} file{(appCopied == 1 ? "" : "s")} found");
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    failed = true;
                    spinner.Complete(false, exception.Message);
                }
            }

            copied += appCopied;
            if (!arguments.Silent)
            {
                Console.WriteLine($"{(failed ? "✗" : "✓")} {app.Name}: {appCopied} changed files");
            }
            if (!failed) successful++;
        }

        if (!arguments.Silent)
        {
            Console.WriteLine($"Completed: {scanned} apps scanned, {successful} successful, {(arguments.DryRun ? $"{copied} files would have been copied" : $"{copied} files copied")}.");
        }
        return successful == scanned ? Success : 1;
    }

    private static BackupDefinition ReadBackupDefinition(string path)
    {
        var lines = File.ReadAllLines(path);
        var id = lines.Select(line => Regex.Match(line, @"^\s*id\s*:\s*(.+)$")).FirstOrDefault(m => m.Success)?.Groups[1].Value.Trim();
        var name = lines.Select(line => Regex.Match(line, @"^\s*name\s*:\s*(.+)$")).FirstOrDefault(m => m.Success)?.Groups[1].Value.Trim();
        var roots = new List<BackupRoot>();
        string? rootName = null;
        var paths = new List<string>();
        EvidenceGroup? evidence = null;
        EvidencePredicate? pendingFileContains = null;
        var pathList = false;

        void FlushEvidencePredicate()
        {
            if (evidence is null || pendingFileContains is null) return;
            evidence.Predicates.Add(pendingFileContains);
            pendingFileContains = null;
        }

        void FlushRoot()
        {
            FlushEvidencePredicate();
            if (rootName is not null && paths.Count > 0)
                roots.Add(new BackupRoot(rootName, paths.ToArray(), evidence));
            rootName = null;
            paths = new List<string>();
            evidence = null;
            pathList = false;
        }

        foreach (var line in lines.Append("- __end__"))
        {
            var root = Regex.Match(line, @"^\s*- name:\s*(.+)$");
            if (root.Success)
            {
                FlushRoot();
                rootName = Unquote(root.Groups[1].Value.Trim());
                continue;
            }

            if (rootName is null) continue;
            var scalarPath = Regex.Match(line, @"^\s{4}path:\s*(.+)$");
            if (scalarPath.Success)
            {
                paths.Add(Unquote(scalarPath.Groups[1].Value.Trim()));
                pathList = false;
                continue;
            }
            if (Regex.IsMatch(line, @"^\s{4}path:\s*$"))
            {
                pathList = true;
                continue;
            }
            if (pathList)
            {
                var pathItem = Regex.Match(line, @"^\s{6}-\s*(.+)$");
                if (pathItem.Success)
                {
                    paths.Add(Unquote(pathItem.Groups[1].Value.Trim()));
                    continue;
                }
                pathList = false;
            }

            var group = Regex.Match(line, @"^\s{6}(any|all):\s*$");
            if (group.Success)
            {
                FlushEvidencePredicate();
                evidence = new EvidenceGroup(group.Groups[1].Value, new List<EvidencePredicate>());
                continue;
            }
            if (evidence is null) continue;

            var fileContains = Regex.IsMatch(line, @"^\s{8}-\s*fileContains:\s*$");
            if (fileContains)
            {
                FlushEvidencePredicate();
                pendingFileContains = new EvidencePredicate("fileContains", null, null);
                continue;
            }
            var predicate = Regex.Match(line, @"^\s{8}-\s*(file|directory):\s*(.+)$");
            if (predicate.Success)
            {
                FlushEvidencePredicate();
                evidence.Predicates.Add(new EvidencePredicate(predicate.Groups[1].Value, Unquote(predicate.Groups[2].Value.Trim()), null));
                continue;
            }
            var property = Regex.Match(line, @"^\s{10}(file|text):\s*(.+)$");
            if (property.Success && pendingFileContains is not null)
            {
                pendingFileContains = property.Groups[1].Value == "file"
                    ? pendingFileContains with { Path = Unquote(property.Groups[2].Value.Trim()) }
                    : pendingFileContains with { Text = Unquote(property.Groups[2].Value.Trim()) };
            }
        }
        FlushRoot();

        return new BackupDefinition(Unquote(id ?? Path.GetFileNameWithoutExtension(path)), Unquote(name ?? id ?? "Application"), roots,
            ReadPatterns(lines, "include"), ReadPatterns(lines, "exclude"));
    }

    private static string[] ReadPatterns(string[] lines, string key)
    {
        var index = Array.FindIndex(lines, line => Regex.IsMatch(line, $@"^\s*{key}:\s*$"));
        if (index < 0) return key == "include" ? new[] { "**" } : Array.Empty<string>();
        return lines.Skip(index + 1).TakeWhile(line => line.StartsWith("  -") || line.StartsWith("    -")).Select(line => Unquote(line[(line.IndexOf('-') + 1)..].Trim())).ToArray();
    }

    private static bool EvidenceMatches(string root, EvidenceGroup? evidence)
    {
        if (evidence is null || evidence.Predicates.Count == 0) return true;
        var matches = evidence.Predicates.Select(predicate =>
        {
            try
            {
                return predicate.Kind switch
                {
                    "file" => predicate.Path is not null && File.Exists(Path.Combine(root, predicate.Path)),
                    "directory" => predicate.Path is not null && Directory.Exists(Path.Combine(root, predicate.Path)),
                    "fileContains" => predicate.Path is not null && predicate.Text is not null &&
                        File.Exists(Path.Combine(root, predicate.Path)) && File.ReadAllText(Path.Combine(root, predicate.Path)).Contains(predicate.Text, StringComparison.Ordinal),
                    _ => false
                };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        });
        return string.Equals(evidence.Operator, "all", StringComparison.OrdinalIgnoreCase) ? matches.All(match => match) : matches.Any();
    }

    private static bool MatchesFilters(string relative, IReadOnlyList<string> include, IReadOnlyList<string> exclude) =>
        include.Any(pattern => GlobMatches(relative, pattern)) && !exclude.Any(pattern => GlobMatches(relative, pattern));

    private static bool GlobMatches(string value, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*\\*", ".*").Replace("\\*", "[^/\\\\]*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value.Replace('\\', '/'), regex, RegexOptions.IgnoreCase);
    }

    private sealed record BackupDefinition(string Id, string Name, IReadOnlyList<BackupRoot> Roots, IReadOnlyList<string> Include, IReadOnlyList<string> Exclude);
    private sealed record BackupRoot(string Name, IReadOnlyList<string> Paths, EvidenceGroup? Evidence);
    private sealed record EvidenceGroup(string Operator, List<EvidencePredicate> Predicates);
    private sealed record EvidencePredicate(string Kind, string? Path, string? Text);

    private sealed class ProgressLine
    {
        private readonly bool silent;
        private readonly string label;
        private readonly bool persist;
        private bool hasDetail;
        private int wouldCopyCount;

        public ProgressLine(bool silent, string label, bool persist)
        {
            this.silent = silent;
            this.label = label;
            this.persist = persist;
        }

        public void Start()
        {
            if (!silent)
            {
                if (persist)
                {
                    Console.Write($"| {label}");
                }
                else
                {
                    Console.Write($"| {label}");
                }
            }
        }

        public void Detail(string text)
        {
            if (!silent)
            {
                if (hasDetail || wouldCopyCount > 0)
                {
                    Console.Write($"\r\u001b[2K  Scanning file {text}");
                }
                else
                {
                    hasDetail = true;
                    Console.WriteLine();
                    Console.Write($"  Scanning file {text}");
                }
            }
        }

        public void WouldCopy(string relativePath)
        {
            if (!silent)
            {
                Console.Write($"\r\u001b[2K  Would copy {relativePath}\n");
                hasDetail = false;
                wouldCopyCount++;
            }
        }

        public void Complete(bool ok, string text)
        {
            if (silent)
            {
                return;
            }

            if (!persist)
            {
                if (hasDetail)
                {
                    // Remove the temporary detail line and the root spinner line.
                    Console.Write("\r\u001b[2K\u001b[1A\r\u001b[2K\n");
                }
                else
                {
                    Console.Write("\r\u001b[2K");
                }

                return;
            }

            var result = $"{(ok ? "✓" : "✗")} {label}: {text}";
            if (hasDetail)
            {
                // Clear the temporary scan line before writing the final line below it.
                Console.Write("\r\u001b[2K");
            }

            if (wouldCopyCount > 0)
            {
                // Replace the spinner, then leave the cursor below persistent per-file
                // messages for the per-app result.
                var linesToSpinner = wouldCopyCount + 1;
                Console.Write($"\u001b[{linesToSpinner}A\r\u001b[2K{result}\u001b[{linesToSpinner}B\r");
            }
            else if (hasDetail)
            {
                // Replace the spinner and leave the cursor below it for the app result.
                Console.Write($"\u001b[1A\r\u001b[2K{result}\u001b[1B\r");
            }
            else
            {
                Console.Write($"\r\u001b[2K{result}\n");
            }
        }
    }

    private static int ListApplications()
    {
        var appsDirectory = Path.Combine(AppContext.BaseDirectory, "apps");
        if (!Directory.Exists(appsDirectory))
        {
            WriteError($"Application definition directory was not found: {appsDirectory}");
            return UsageError;
        }

        var applications = Directory.EnumerateFiles(appsDirectory, "*.yaml")
            .Concat(Directory.EnumerateFiles(appsDirectory, "*.yml"))
            .Select(ReadApplicationDefinition)
            .OrderBy(application => application.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (applications.Length == 0)
        {
            Console.WriteLine("No application definitions found.");
            return Success;
        }

        Console.WriteLine("Known applications:");
        foreach (var application in applications)
        {
            Console.WriteLine($"  {application.Id,-16} {application.Name}");
        }

        return Success;
    }

    private static ApplicationDefinition ReadApplicationDefinition(string path)
    {
        string? id = null;
        string? name = null;

        foreach (var line in File.ReadLines(path))
        {
            var idMatch = Regex.Match(line, @"^\s*id\s*:\s*(?<value>[^#]+?)\s*(?:#.*)?$");
            if (idMatch.Success)
            {
                id = Unquote(idMatch.Groups["value"].Value.Trim());
            }

            var nameMatch = Regex.Match(line, @"^\s*name\s*:\s*(?<value>[^#]+?)\s*(?:#.*)?$");
            if (nameMatch.Success)
            {
                name = Unquote(nameMatch.Groups["value"].Value.Trim());
            }
        }

        return new ApplicationDefinition(
            id ?? Path.GetFileNameWithoutExtension(path),
            name ?? "(unnamed)");
    }

    private sealed record ApplicationDefinition(string Id, string Name);

    private static int Status()
    {
        var configPath = GetConfigPath();
        var config = ReadConfiguration(configPath);

        Console.WriteLine("StateKeep status");
        Console.WriteLine($"Configuration: {configPath}");
        var deviceId = ReadDeviceId();
        Console.WriteLine($"Device ID: {deviceId ?? "not generated"}");

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

        var deviceId = ReadDeviceId();
        if (deviceId is null)
        {
            errors.Add($"Device ID was not found: {GetDeviceIdPath()}");
        }
        else if (!Regex.IsMatch(deviceId, "^[A-Za-z0-9]{8,12}$"))
        {
            errors.Add("Device ID must contain 8-12 alphanumeric characters");
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

    private static string? ReadDeviceId()
    {
        var deviceIdPath = GetDeviceIdPath();
        return File.Exists(deviceIdPath) ? File.ReadAllText(deviceIdPath).Trim() : null;
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
            Verbose = Has("--verbose");
            DryRun = Has("--dryrun", "--dry-run");
        }

        public string? Command { get; }
        public bool Silent { get; }
        public bool Verbose { get; }
        public bool DryRun { get; }
        public IReadOnlyList<string> Positionals => values.Where(value => !value.StartsWith('-')).ToArray();

        public bool Has(params string[] options) => values.Any(value => options.Contains(value, StringComparer.OrdinalIgnoreCase));
    }
}
