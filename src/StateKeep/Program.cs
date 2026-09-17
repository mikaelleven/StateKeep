using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        if (string.Equals(arguments.Command, "tmextract", StringComparison.OrdinalIgnoreCase))
        {
            return TampermonkeyExtract(arguments);
        }

        if (string.Equals(arguments.Command, "restore", StringComparison.OrdinalIgnoreCase))
        {
            return Restore(arguments);
        }

        if (string.Equals(arguments.Command, "status", StringComparison.OrdinalIgnoreCase))
        {
            return Status();
        }

        if (string.Equals(arguments.Command, "validate", StringComparison.OrdinalIgnoreCase))
        {
            return Validate();
        }

        if (string.Equals(arguments.Command, "apps", StringComparison.OrdinalIgnoreCase))
        {
            return Apps(arguments);
        }

        if (string.Equals(arguments.Command, "open", StringComparison.OrdinalIgnoreCase))
        {
            return OpenApplication(arguments);
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
        "tmextract",
        "restore",
        "apps",
        "status",
        "validate",
        "open",
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
        Console.WriteLine("  tmextract [profile|chrome|brave] [target]  Extract Tampermonkey scripts");
        Console.WriteLine("  restore <app> [--from <device>]  Restore one application");
        Console.WriteLine("  restore --all [--from <device>]  Restore all applications");
        Console.WriteLine("  apps list          List known applications");
        Console.WriteLine("  apps open          Open the installed apps folder");
        Console.WriteLine("  open <app>         Open the first detected application root");
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
        Console.WriteLine("      --force        Overwrite conflicting files after backing them up");
        Console.WriteLine("      --from <device> Restore from another device ID, computer name, or folder");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  statekeep restore zed");
        Console.WriteLine("  statekeep restore zed --from MYCOMPUTER");
        Console.WriteLine("  statekeep restore --all --from MYCOMPUTER");
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
            $"{Environment.MachineName}_{EnsureDeviceId()}");
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

                    if (string.Equals(app.Tool, "tmextract", StringComparison.OrdinalIgnoreCase))
                    {
                        var extraction = BackupTampermonkeyScripts(app, root, source, backupRoot, arguments, spinner);
                        appCopied += extraction.Updated;
                        spinner.Complete(true, $"{extraction.Found} scripts found, {extraction.Updated} {(arguments.DryRun ? "would be extracted" : "extracted")}");
                        continue;
                    }

                    var files = Directory.EnumerateFiles(source, "*", new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                            AttributesToSkip = FileAttributes.ReparsePoint
                        })
                        .Where(file => MatchesFilters(Path.GetRelativePath(source, file), app.Include, app.Exclude))
                        .ToArray();
                    var destination = app.Roots.Count == 1
                        ? Path.Combine(backupRoot, app.Id)
                        : Path.Combine(backupRoot, app.Id, root.Name);
                    var selectedRelativePaths = files
                        .Select(file => Path.GetRelativePath(source, file))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var file in files)
                    {
                        var relative = Path.GetRelativePath(source, file);
                        var target = Path.Combine(destination, relative);
                        if (!FileNeedsUpdate(file, target))
                        {
                            continue;
                        }

                        if (arguments.Verbose)
                        {
                            spinner.Detail(relative);
                        }

                        if (arguments.Verbose && arguments.DryRun)
                        {
                            spinner.WouldCopy(relative);
                        }

                        if (!arguments.DryRun)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            File.Copy(file, target, true);
                            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file));
                            if (arguments.Verbose)
                            {
                                spinner.FileCopied(relative);
                            }
                        }
                        appCopied++;
                    }

                    if (!arguments.DryRun)
                    {
                        RemoveObsoleteBackupFiles(destination, selectedRelativePaths);
                    }

                    spinner.Complete(true, $"{files.Length} file{(files.Length == 1 ? "" : "s")} found");
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

    private const string TampermonkeyExtensionId = "dhdgffkkebhmkfjojejmpbldmpobfkfo";

    private static int TampermonkeyExtract(Arguments arguments)
    {
        var positional = arguments.Positionals.Skip(1).ToArray();
        var source = positional.ElementAtOrDefault(0) ?? Directory.GetCurrentDirectory();
        var target = positional.ElementAtOrDefault(1) ?? Path.Combine(Directory.GetCurrentDirectory(), "tampermonkey_scripts");
        source = ResolveBrowserProfilePath(source);

        try
        {
            var scriptDirectories = FindTampermonkeyDirectories(source).ToArray();
            if (scriptDirectories.Length == 0)
            {
                WriteError($"No Tampermonkey .ldb files were found below: {source}");
                return UsageError;
            }

            var copied = 0;
            foreach (var directory in scriptDirectories)
            {
                copied += ExtractTampermonkeyScripts(directory, target, arguments.DryRun, arguments.Force, arguments.Silent, arguments.Verbose).Updated;
            }

            if (!arguments.Silent)
            {
                Console.WriteLine(arguments.DryRun
                    ? $"Completed: {copied} scripts would be extracted."
                    : $"Completed: {copied} scripts extracted.");
            }
            return Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            WriteError($"Could not extract Tampermonkey scripts: {exception.Message}");
            return 1;
        }
    }

    private static string ResolveBrowserProfilePath(string source) => source.ToLowerInvariant() switch
    {
        "brave" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "User Data"),
        "chrome" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"),
        _ => Environment.ExpandEnvironmentVariables(source)
    };

    private static IEnumerable<string> FindTampermonkeyDirectories(string source)
    {
        var direct = Path.GetFullPath(source);
        if (!Directory.Exists(direct))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(direct, "*.ldb", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            })
            .Select(Path.GetDirectoryName)
            .Where(path => path is not null)
            .Cast<string>()
            .Where(path => string.Equals(Path.GetFileName(path), TampermonkeyExtensionId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static ExtractionResult BackupTampermonkeyScripts(BackupDefinition app, BackupRoot root, string source, string backupRoot, Arguments arguments, ProgressLine spinner)
    {
        var directories = Directory.EnumerateFiles(source, "*.ldb", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            })
            .Select(Path.GetDirectoryName)
            .Where(path => path is not null)
            .Cast<string>()
            .Where(path => string.Equals(Path.GetFileName(path), TampermonkeyExtensionId, StringComparison.OrdinalIgnoreCase))
            .Where(path => app.Include.Any(pattern => GlobMatches(Path.GetRelativePath(source, path), pattern)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var found = 0;
        var updated = 0;
        foreach (var directory in directories)
        {
            var relative = Path.GetRelativePath(source, directory);
            var target = Path.Combine(backupRoot, app.Id, root.Name, relative);
            var extraction = ExtractTampermonkeyScripts(directory, target, arguments.DryRun, overwriteExisting: true, arguments.Silent, arguments.Verbose);
            found += extraction.Found;
            updated += extraction.Updated;
        }
        return new ExtractionResult(found, updated);
    }

    private static ExtractionResult ExtractTampermonkeyScripts(string databaseDirectory, string targetDirectory, bool dryRun, bool overwriteExisting, bool silent, bool verbose)
    {
        var values = Directory.EnumerateFiles(databaseDirectory, "*.ldb")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(ReadLevelDbValues)
            .ToArray();
        if (verbose && !silent)
        {
            var headers = values.Count(value => Encoding.UTF8.GetString(value).Contains("==UserScript==", StringComparison.Ordinal));
            Console.WriteLine($"  Read {values.Length} LevelDB values ({headers} containing a userscript header)");
        }
        var scripts = values
            .SelectMany(ExtractScripts)
            .GroupBy(script => script.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();

        var updated = 0;
        foreach (var script in scripts)
        {
            var target = Path.Combine(targetDirectory, script.FileName);
            if (File.Exists(target) && File.ReadAllText(target, Encoding.UTF8) == script.Source)
            {
                continue;
            }
            if (File.Exists(target) && !overwriteExisting)
            {
                if (!silent)
                {
                    Console.WriteLine($"Skipped existing script (use --force to overwrite): {target}");
                }
                continue;
            }

            if (!dryRun)
            {
                Directory.CreateDirectory(targetDirectory);
                var temporary = target + ".tmp";
                File.WriteAllText(temporary, script.Source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temporary, target, overwrite: true);
            }
            updated++;
        }
        return new ExtractionResult(scripts.Length, updated);
    }

    private static IEnumerable<ExtractedScript> ExtractScripts(byte[] value)
    {
        var text = Encoding.UTF8.GetString(value);
        if (!text.Contains("==UserScript==", StringComparison.Ordinal))
        {
            yield break;
        }

        JsonDocument? document = null;
        try { document = JsonDocument.Parse(text); }
        catch (JsonException) { }
        if (document is not null)
        {
            using (document)
            {
                foreach (var script in ExtractScripts(document.RootElement))
                {
                    yield return script;
                }
            }
            yield break;
        }

        yield return new ExtractedScript(CreateScriptFileName(text, null), text);
    }

    private static IEnumerable<ExtractedScript> ExtractScripts(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var name = element.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() : null;
            foreach (var property in element.EnumerateObject())
            {
                if ((property.NameEquals("code") || property.NameEquals("source")) && property.Value.ValueKind == JsonValueKind.String)
                {
                    var source = property.Value.GetString()!;
                    if (source.Contains("==UserScript==", StringComparison.Ordinal))
                    {
                        yield return new ExtractedScript(CreateScriptFileName(source, name), source);
                    }
                }
                foreach (var script in ExtractScripts(property.Value))
                {
                    yield return script;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var script in ExtractScripts(item))
                {
                    yield return script;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var source = element.GetString()!;
            if (source.Contains("==UserScript==", StringComparison.Ordinal))
            {
                yield return new ExtractedScript(CreateScriptFileName(source, null), source);
            }
        }
    }

    private static string CreateScriptFileName(string source, string? name)
    {
        var metadataName = Regex.Match(source, @"^\s*//\s*@name\s+(.+)$", RegexOptions.Multiline).Groups[1].Value.Trim();
        var candidate = string.IsNullOrWhiteSpace(name) ? metadataName : name;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..12];
        }
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(candidate.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return (string.IsNullOrWhiteSpace(safe) ? "tampermonkey-script" : safe) + ".js";
    }

    private static IEnumerable<byte[]> ReadLevelDbValues(string path)
    {
        var file = File.ReadAllBytes(path);
        if (file.Length < 48)
        {
            yield break;
        }
        var footer = file.AsSpan(file.Length - 48);
        const ulong TableMagic = 0xdb4775248b80fb57;
        if (BitConverter.ToUInt64(footer[40..]) != TableMagic)
        {
            yield break;
        }

        var position = 0;
        _ = ReadBlockHandle(footer[..40], ref position);
        var indexHandle = ReadBlockHandle(footer[..40], ref position);
        foreach (var index in ReadLevelDbBlock(file, indexHandle))
        {
            var handlePosition = 0;
            var dataHandle = ReadBlockHandle(index.Value, ref handlePosition);
            foreach (var entry in ReadLevelDbBlock(file, dataHandle))
            {
                yield return entry.Value;
            }
        }
    }

    private static BlockHandle ReadBlockHandle(ReadOnlySpan<byte> data, ref int position) =>
        new(ReadVarint(data, ref position), ReadVarint(data, ref position));

    private static IEnumerable<LevelDbEntry> ReadLevelDbBlock(byte[] file, BlockHandle handle)
    {
        if (handle.Offset > int.MaxValue || handle.Size > int.MaxValue || handle.Offset + handle.Size + 5 > (ulong)file.Length)
        {
            throw new InvalidDataException("LevelDB block points outside its table file.");
        }
        var offset = (int)handle.Offset;
        var size = (int)handle.Size;
        var data = file.AsSpan(offset, size).ToArray();
        var compression = file[offset + size];
        data = compression switch
        {
            0 => data,
            1 => DecompressSnappy(data),
            _ => throw new InvalidDataException($"Unsupported LevelDB compression type {compression}.")
        };

        if (data.Length < 4) yield break;
        var restartCount = BitConverter.ToInt32(data, data.Length - 4);
        var restartOffset = data.Length - 4 - (restartCount * sizeof(int));
        if (restartCount < 0 || restartOffset < 0) throw new InvalidDataException("Invalid LevelDB block restart table.");
        var position = 0;
        var previousKey = Array.Empty<byte>();
        while (position < restartOffset)
        {
            var shared = checked((int)ReadVarint(data, ref position));
            var unshared = checked((int)ReadVarint(data, ref position));
            var valueLength = checked((int)ReadVarint(data, ref position));
            if (shared > previousKey.Length || position + unshared + valueLength > restartOffset) throw new InvalidDataException("Invalid LevelDB entry.");
            var key = previousKey[..shared].Concat(data.AsSpan(position, unshared).ToArray()).ToArray();
            position += unshared;
            var value = data.AsSpan(position, valueLength).ToArray();
            position += valueLength;
            previousKey = key;
            yield return new LevelDbEntry(key, value);
        }
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int position)
    {
        ulong result = 0;
        for (var shift = 0; shift < 64 && position < data.Length; shift += 7)
        {
            var value = data[position++];
            result |= (ulong)(value & 0x7f) << shift;
            if ((value & 0x80) == 0) return result;
        }
        throw new InvalidDataException("Invalid LevelDB varint.");
    }

    private static byte[] DecompressSnappy(ReadOnlySpan<byte> compressed)
    {
        var position = 0;
        var length = checked((int)ReadVarint(compressed, ref position));
        var output = new byte[length];
        var outputPosition = 0;
        while (position < compressed.Length)
        {
            var tag = compressed[position++];
            switch (tag & 0x03)
            {
                case 0:
                    var literalLength = tag >> 2;
                    if (literalLength >= 60)
                    {
                        var byteCount = literalLength - 59;
                        literalLength = 0;
                        for (var index = 0; index < byteCount; index++) literalLength |= compressed[position++] << (index * 8);
                    }
                    literalLength++;
                    compressed.Slice(position, literalLength).CopyTo(output.AsSpan(outputPosition));
                    position += literalLength;
                    outputPosition += literalLength;
                    break;
                case 1:
                    CopySnappy(output, ref outputPosition, 4 + ((tag >> 2) & 7), ((tag & 0xe0) << 3) | compressed[position++]);
                    break;
                case 2:
                    CopySnappy(output, ref outputPosition, 1 + (tag >> 2), compressed[position] | (compressed[position + 1] << 8));
                    position += 2;
                    break;
                case 3:
                    CopySnappy(output, ref outputPosition, 1 + (tag >> 2), BitConverter.ToInt32(compressed.Slice(position, 4)));
                    position += 4;
                    break;
            }
        }
        if (outputPosition != output.Length) throw new InvalidDataException("Invalid Snappy output length.");
        return output;
    }

    private static void CopySnappy(byte[] output, ref int destination, int length, int offset)
    {
        if (offset <= 0 || offset > destination || destination + length > output.Length) throw new InvalidDataException("Invalid Snappy copy offset.");
        for (var index = 0; index < length; index++) output[destination + index] = output[destination - offset + index];
        destination += length;
    }

    private sealed record ExtractionResult(int Found, int Updated);
    private sealed record ExtractedScript(string FileName, string Source);
    private sealed record BlockHandle(ulong Offset, ulong Size);
    private sealed record LevelDbEntry(byte[] Key, byte[] Value);

    private static int Restore(Arguments arguments)
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
        var restoreAll = arguments.Has("--all");
        if (requestedApp is not null && restoreAll)
        {
            WriteError("Specify either an application or --all, not both.");
            return UsageError;
        }

        if (requestedApp is null && !restoreAll)
        {
            WriteError("Specify an application or --all.");
            return UsageError;
        }

        var applications = Directory.EnumerateFiles(appDirectory, "*.yaml")
            .Concat(Directory.EnumerateFiles(appDirectory, "*.yml"))
            .Select(ReadBackupDefinition)
            .Where(app => restoreAll || string.Equals(app.Id, requestedApp, StringComparison.OrdinalIgnoreCase))
            .OrderBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (applications.Length == 0)
        {
            WriteError($"No application definition found for '{requestedApp}'.");
            return UsageError;
        }

        var backupBasePath = Environment.ExpandEnvironmentVariables(configuration.BackupPath);
        var sourceDevice = FindRestoreDevice(backupBasePath, arguments.From);
        if (sourceDevice is null)
        {
            return UsageError;
        }

        var failures = 0;
        var restored = 0;
        foreach (var app in applications)
        {
            try
            {
                var appRestored = 0;
                var backupFilesFound = 0;
                foreach (var root in app.Roots)
                {
                    var source = app.Roots.Count == 1
                        ? Path.Combine(sourceDevice, app.Id)
                        : Path.Combine(sourceDevice, app.Id, root.Name);
                    if (!Directory.Exists(source))
                    {
                        continue;
                    }

                    var destination = ResolveRestoreTarget(root, arguments);
                    if (destination is null)
                    {
                        failures++;
                        continue;
                    }

                    foreach (var file in Directory.EnumerateFiles(source, "*", new EnumerationOptions
                             {
                                 RecurseSubdirectories = true,
                                 IgnoreInaccessible = true,
                                 AttributesToSkip = FileAttributes.ReparsePoint
                             }))
                    {
                        var relative = Path.GetRelativePath(source, file);
                        if (!MatchesFilters(relative, app.Include, app.Exclude))
                        {
                            continue;
                        }

                        backupFilesFound++;
                        var target = Path.Combine(destination, relative);
                        if (File.Exists(target) && FilesAreIdentical(file, target))
                        {
                            continue;
                        }

                        if (File.Exists(target) && !arguments.Force)
                        {
                            WriteRestoreConflict(relative, file, target, arguments.Silent);
                            failures++;
                            continue;
                        }

                        if (arguments.DryRun)
                        {
                            if (!arguments.Silent)
                            {
                                Console.WriteLine($"Would restore {app.Name}: {relative}");
                            }
                            appRestored++;
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        if (File.Exists(target))
                        {
                            var safetyCopy = GetBackupFilePath(target);
                            File.Copy(target, safetyCopy, false);
                            if (!arguments.Silent)
                            {
                                Console.WriteLine($"Backed up existing file: {safetyCopy}");
                            }
                        }

                        File.Copy(file, target, true);
                        File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file));
                        appRestored++;
                    }
                }

                restored += appRestored;
                if (!arguments.Silent)
                {
                    if (backupFilesFound == 0)
                    {
                        var deviceDescription = arguments.From is null ? "this device" : $"device '{arguments.From}'";
                        Console.WriteLine($"Warning: No backup files were found for {app.Name} on {deviceDescription}.");
                    }

                    Console.WriteLine($"{app.Name}: {(arguments.DryRun ? appRestored + " files would be restored" : appRestored + " files restored")}");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures++;
                WriteError($"Could not restore {app.Name}: {exception.Message}");
            }
        }

        if (!arguments.Silent)
        {
            Console.WriteLine(arguments.DryRun
                ? $"Completed: {restored} files would be restored."
                : $"Completed: {restored} files restored.");
        }
        return failures == 0 ? Success : 1;
    }

    private static string? FindRestoreDevice(string backupBasePath, string? requestedDevice)
    {
        if (!Directory.Exists(backupBasePath))
        {
            WriteError($"Backup path was not found: {backupBasePath}");
            return null;
        }

        var deviceDirectories = Directory.EnumerateDirectories(backupBasePath).ToArray();
        var device = requestedDevice is null
            ? $"{Environment.MachineName}_{EnsureDeviceId()}"
            : requestedDevice;
        var matches = deviceDirectories.Where(path =>
        {
            var name = Path.GetFileName(path);
            return string.Equals(name, device, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(device + "_", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_" + device, StringComparison.OrdinalIgnoreCase);
        }).ToArray();

        if (matches.Length == 1)
        {
            return matches[0];
        }

        WriteError(matches.Length == 0
            ? $"No backup device matched '{device}'."
            : $"Backup device '{device}' is ambiguous. Use its full computer-name_device-id folder name.");
        return null;
    }

    private static string? ResolveRestoreTarget(BackupRoot root, Arguments arguments)
    {
        var paths = root.Paths.Select(Environment.ExpandEnvironmentVariables).ToArray();
        if (paths.Length == 1)
        {
            return paths[0];
        }

        var matchingPath = paths.FirstOrDefault(path => Directory.Exists(path) && EvidenceMatches(path, root.Evidence));
        if (matchingPath is not null)
        {
            return matchingPath;
        }

        if (arguments.Silent)
        {
            WriteError($"Could not identify a restore target for root '{root.Name}' without prompting.");
            return null;
        }

        Console.WriteLine($"No known path matched the evidence for '{root.Name}'.");
        Console.WriteLine($"1. Use the first configured path: {paths[0]}");
        Console.WriteLine("2. Enter a custom path");
        Console.Write("Choose [1/2]: ");
        var choice = Console.ReadLine()?.Trim();
        if (choice == "1")
        {
            return paths[0];
        }

        if (choice == "2")
        {
            Console.Write("Custom restore path: ");
            var customPath = Console.ReadLine()?.Trim();
            return string.IsNullOrWhiteSpace(customPath) ? null : Environment.ExpandEnvironmentVariables(customPath);
        }

        WriteError("Restore target selection was cancelled.");
        return null;
    }

    private static bool FilesAreIdentical(string source, string target)
    {
        var sourceInfo = new FileInfo(source);
        var targetInfo = new FileInfo(target);
        if (sourceInfo.Length != targetInfo.Length)
        {
            return false;
        }

        using var sourceStream = File.OpenRead(source);
        using var targetStream = File.OpenRead(target);
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(sourceStream), SHA256.HashData(targetStream));
    }

    private static string GetBackupFilePath(string target)
    {
        var candidate = target + ".bak";
        return File.Exists(candidate)
            ? candidate + "." + DateTime.Now.ToString("yyyyMMdd-HHmmssfff")
            : candidate;
    }

    private static void WriteRestoreConflict(string relative, string source, string target, bool silent)
    {
        if (silent)
        {
            return;
        }

        static string Metadata(string path)
        {
            var info = new FileInfo(path);
            using var stream = File.OpenRead(path);
            return $"Size: {info.Length:N0} bytes, Modified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}, SHA-256: {Convert.ToHexString(SHA256.HashData(stream))}";
        }

        Console.WriteLine($"Conflict: {relative}");
        Console.WriteLine($"  Source:      {Metadata(source)}");
        Console.WriteLine($"  Destination: {Metadata(target)}");
        Console.WriteLine("Skipped. Use --force to overwrite after creating a .bak file.");
    }

    private static void RemoveObsoleteBackupFiles(string destination, ISet<string> selectedRelativePaths)
    {
        if (!Directory.Exists(destination))
        {
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        var obsoleteFiles = Directory.EnumerateFiles(destination, "*", options)
            .Where(file => !selectedRelativePaths.Contains(Path.GetRelativePath(destination, file)))
            .ToArray();
        foreach (var file in obsoleteFiles)
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(destination, "*", options)
                     .OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }

    private static bool FileNeedsUpdate(string source, string target)
    {
        if (!File.Exists(target))
        {
            return true;
        }

        var sourceInfo = new FileInfo(source);
        var targetInfo = new FileInfo(target);
        return sourceInfo.Length != targetInfo.Length
            || sourceInfo.LastWriteTimeUtc != targetInfo.LastWriteTimeUtc;
    }

    private static BackupDefinition ReadBackupDefinition(string path)
    {
        var lines = File.ReadAllLines(path);
        var id = lines.Select(line => Regex.Match(line, @"^\s*id\s*:\s*(.+)$")).FirstOrDefault(m => m.Success)?.Groups[1].Value.Trim();
        var name = lines.Select(line => Regex.Match(line, @"^\s*name\s*:\s*(.+)$")).FirstOrDefault(m => m.Success)?.Groups[1].Value.Trim();
        var tool = lines.Select(line => Regex.Match(line, @"^\s*tool\s*:\s*(.+)$")).FirstOrDefault(m => m.Success)?.Groups[1].Value.Trim();
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

        return new BackupDefinition(Unquote(id ?? Path.GetFileNameWithoutExtension(path)), Unquote(name ?? id ?? "Application"), Unquote(tool ?? ""), roots,
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

    private sealed record BackupDefinition(string Id, string Name, string Tool, IReadOnlyList<BackupRoot> Roots, IReadOnlyList<string> Include, IReadOnlyList<string> Exclude);
    private sealed record BackupRoot(string Name, IReadOnlyList<string> Paths, EvidenceGroup? Evidence);
    private sealed record EvidenceGroup(string Operator, List<EvidencePredicate> Predicates);
    private sealed record EvidencePredicate(string Kind, string? Path, string? Text);

    private sealed class ProgressLine
    {
        private static readonly string[] SpinnerFrames = ["|", "/", "-", "\\"];
        private readonly object consoleLock = new();
        private readonly bool silent;
        private readonly string label;
        private readonly bool persist;
        private CancellationTokenSource? refreshCancellation;
        private Thread? refreshThread;
        private bool hasDetail;
        private int outputFileCount;

        public ProgressLine(bool silent, string label, bool persist)
        {
            this.silent = silent;
            this.label = label;
            this.persist = persist;
        }

        public void Start()
        {
            if (silent)
            {
                return;
            }

            refreshCancellation = new CancellationTokenSource();
            refreshThread = new Thread(() => Refresh(refreshCancellation.Token))
            {
                IsBackground = true
            };
            refreshThread.Start();
        }

        private void Refresh(CancellationToken cancellationToken)
        {
            var frame = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (consoleLock)
                {
                    Console.Write($"\r\u001b[2K{SpinnerFrames[frame]} {label}");
                }

                frame = (frame + 1) % SpinnerFrames.Length;
                cancellationToken.WaitHandle.WaitOne(100);
            }
        }

        public void Detail(string text)
        {
            if (!silent)
            {
                lock (consoleLock)
                {
                    if (hasDetail || outputFileCount > 0)
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
        }

        public void WouldCopy(string relativePath)
        {
            if (!silent)
            {
                lock (consoleLock)
                {
                    Console.Write($"\r\u001b[2K  Would copy {relativePath}\n");
                    hasDetail = false;
                    outputFileCount++;
                }
            }
        }

        public void FileCopied(string relativePath)
        {
            if (!silent)
            {
                lock (consoleLock)
                {
                    Console.Write($"\r\u001b[2K  file {relativePath} copied\n");
                    hasDetail = false;
                    outputFileCount++;
                }
            }
        }

        public void Complete(bool ok, string text)
        {
            StopRefresh();
            if (silent)
            {
                return;
            }

            lock (consoleLock)
            {
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

                if (outputFileCount > 0)
                {
                    // Replace the spinner, then leave the cursor below persistent per-file
                    // messages for the per-app result.
                    var linesToSpinner = outputFileCount + 1;
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

        private void StopRefresh()
        {
            var cancellation = refreshCancellation;
            if (cancellation is null)
            {
                return;
            }

            cancellation.Cancel();
            refreshThread?.Join();
            cancellation.Dispose();
            refreshCancellation = null;
            refreshThread = null;
        }
    }

    private static int OpenApplication(Arguments arguments)
    {
        var requestedApp = arguments.Positionals.Skip(1).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestedApp))
        {
            WriteError("An application is required. Usage: statekeep open <app>");
            return UsageError;
        }

        var appsDirectory = Path.Combine(AppContext.BaseDirectory, "apps");
        if (!Directory.Exists(appsDirectory))
        {
            WriteError($"Application definition directory was not found: {appsDirectory}");
            return UsageError;
        }

        var application = Directory.EnumerateFiles(appsDirectory, "*.yaml")
            .Concat(Directory.EnumerateFiles(appsDirectory, "*.yml"))
            .Select(ReadBackupDefinition)
            .FirstOrDefault(app => string.Equals(app.Id, requestedApp, StringComparison.OrdinalIgnoreCase));
        if (application is null)
        {
            WriteError($"No application definition found for '{requestedApp}'. Use 'statekeep apps list' to see available applications.");
            return UsageError;
        }

        string? matchingPath = null;
        foreach (var root in application.Roots)
        {
            matchingPath = root.Paths
                .Select(Environment.ExpandEnvironmentVariables)
                .FirstOrDefault(path => Directory.Exists(path) && EvidenceMatches(path, root.Evidence));
            if (matchingPath is not null)
            {
                break;
            }
        }

        if (matchingPath is null)
        {
            Console.WriteLine($"Warning: no root folder for {application.Name} was found on this machine.");
            return Success;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
                Arguments = $"\"{matchingPath}\""
            });
            return Success;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            WriteError($"Could not open the {application.Name} folder: {exception.Message}");
            return UsageError;
        }
    }

    private static int Apps(Arguments arguments)
    {
        var subcommand = arguments.Positionals.Skip(1).FirstOrDefault();
        if (string.Equals(subcommand, "list", StringComparison.OrdinalIgnoreCase))
        {
            return ListApplications();
        }

        if (string.Equals(subcommand, "open", StringComparison.OrdinalIgnoreCase))
        {
            return OpenApplicationsDirectory();
        }

        WriteError("Usage: statekeep apps <list|open>");
        return UsageError;
    }

    private static int OpenApplicationsDirectory()
    {
        var appsDirectory = Path.Combine(AppContext.BaseDirectory, "apps");
        if (!Directory.Exists(appsDirectory))
        {
            WriteError($"Installed apps directory was not found: {appsDirectory}");
            return UsageError;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
                Arguments = $"\"{appsDirectory}\""
            });
            return Success;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            WriteError($"Could not open the installed apps directory: {exception.Message}");
            return UsageError;
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
        var computerName = Environment.MachineName;
        var deviceId = ReadDeviceId() ?? EnsureDeviceId();
        Console.WriteLine($"Computer name: {computerName}");
        Console.WriteLine($"Device ID: {deviceId}");

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
        var resolvedPath = Path.Combine(expandedPath, $"{computerName}_{deviceId}");
        Console.WriteLine($"Backup path: {config.BackupPath}");
        Console.WriteLine($"Resolved path: {resolvedPath}");
        Console.WriteLine($"Path status: {(Directory.Exists(resolvedPath) ? "exists" : "does not exist")}");
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
            Force = Has("--force");
            From = GetValue("--from");
        }

        public string? Command { get; }
        public bool Silent { get; }
        public bool Verbose { get; }
        public bool DryRun { get; }
        public bool Force { get; }
        public string? From { get; }
        public IReadOnlyList<string> Positionals => values.Where((value, index) =>
            !value.StartsWith('-') && (index == 0 || !string.Equals(values[index - 1], "--from", StringComparison.OrdinalIgnoreCase))).ToArray();

        public bool Has(params string[] options) => values.Any(value => options.Contains(value, StringComparer.OrdinalIgnoreCase));

        private string? GetValue(string option)
        {
            var index = Array.FindIndex(values, value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index == values.Length - 1 || values[index + 1].StartsWith('-'))
            {
                return null;
            }

            return values[index + 1];
        }
    }
}
