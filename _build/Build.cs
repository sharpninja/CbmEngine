using System.Diagnostics;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

class Build : NukeBuild
{
    const string SolutionName = "CbmEngine.slnx";
    const string PackageId = "SharpNinja.CbmEngine";
    const string ChocolateyCc65Package = "cc65-compiler";
    const string ScoopCc65Package = "cc65";

    [Parameter("Configuration to build. Defaults to Debug for local builds and Release for server builds.")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("NuGet source for PublishNuGet.")]
    readonly string NuGetSource = "https://api.nuget.org/v3/index.json";

    [Parameter("NuGet API key for PublishNuGet. Defaults to the NUGET_API_KEY environment variable.")]
    readonly string? NuGetApiKey = null;

    [Parameter("Commit all current changes and create an annotated release tag before PublishNuGet packs from the last tag.")]
    readonly bool CommitAndTagBeforePublish = false;

    [Parameter("Commit message used when CommitAndTagBeforePublish is set.")]
    readonly string ReleaseCommitMessage = "chore(release): publish SharpNinja.CbmEngine";

    [Parameter("Prefix used for release tags created by CommitAndTagBeforePublish.")]
    readonly string ReleaseTagPrefix = "v";

    [Parameter("Pass --skip-duplicate to dotnet nuget push.")]
    readonly bool NuGetSkipDuplicate = true;

    AbsolutePath SolutionFile => RootDirectory / SolutionName;
    AbsolutePath PackageProject => RootDirectory / "src" / "SharpNinja.CbmEngine" / "SharpNinja.CbmEngine.csproj";
    AbsolutePath UnitTestProject => RootDirectory / "tests" / "CbmEngine.Tests.Unit" / "CbmEngine.Tests.Unit.csproj";
    AbsolutePath IntegrationTestProject => RootDirectory / "tests" / "CbmEngine.Tests.Integration" / "CbmEngine.Tests.Integration.csproj";
    AbsolutePath NukeArtifactsDirectory => RootDirectory / "artifacts" / "nuke";
    AbsolutePath PackagesDirectory => NukeArtifactsDirectory / "packages";
    AbsolutePath TaggedSourceDirectory => NukeArtifactsDirectory / "tagged-source";
    AbsolutePath TaggedPackagesDirectory => NukeArtifactsDirectory / "tagged-packages";

    public static int Main() => Execute<Build>(x => x.Compile);

    Target Clean => _ => _
        .Description("Cleans the solution and NUKE-owned artifact directory.")
        .Before(Restore)
        .Executes(() =>
        {
            RunDotNet("clean", SolutionFile, "--configuration", Configuration, "--verbosity", "minimal");
            DeleteDirectoryIfExists(NukeArtifactsDirectory);
        });

    Target Restore => _ => _
        .Description("Restores NuGet packages for the solution.")
        .Executes(() =>
        {
            RunDotNet("restore", SolutionFile, "--verbosity", "minimal", "/p:RestoreFallbackFolders=");
            RunProcessIn(RootDirectory, "dotnet", "tool", "restore");
        });

    Target Compile => _ => _
        .Description("Builds the solution without restoring.")
        .DependsOn(Restore)
        .Executes(() =>
        {
            RunDotNet("build", SolutionFile, "--no-restore", "--configuration", Configuration, "--verbosity", "minimal");
        });

    Target UnitTests => _ => _
        .Description("Runs the unit test project.")
        .DependsOn(Compile)
        .Executes(() =>
        {
            RunDotNet("test", UnitTestProject, "--no-restore", "--configuration", Configuration, "--verbosity", "minimal");
        });

    Target VerifyDeps => _ => _
        .Description("Verifies external command-line dependencies required by integration tests.")
        .Executes(EnsureCc65Available);

    Target IntegrationTests => _ => _
        .Description("Runs the integration test project. Requires CC65 on PATH.")
        .DependsOn(Compile, VerifyDeps)
        .Executes(() =>
        {
            RunDotNet("test", IntegrationTestProject, "--no-restore", "--configuration", Configuration, "--verbosity", "minimal");
        });

    Target Test => _ => _
        .Description("Runs unit and integration tests.")
        .DependsOn(UnitTests, IntegrationTests);

    Target Pack => _ => _
        .Description("Packs the SharpNinja.CbmEngine NuGet package from the current workspace.")
        .DependsOn(PackSharpNinjaCbmEngine);

    Target PackSharpNinjaCbmEngine => _ => _
        .Description("Builds SharpNinja.CbmEngine.nupkg from the current workspace.")
        .DependsOn(Compile)
        .Executes(() =>
        {
            PrepareDirectory(PackagesDirectory);
            PackSharpNinjaCbmEngineFromSource(RootDirectory, PackagesDirectory, noBuild: true);
        });

    Target PublishNuGet => _ => _
        .Description("Builds SharpNinja.CbmEngine from the last Git tag and publishes it to NuGet.")
        .DependsOn(UnitTests)
        .Executes(() =>
        {
            if (CommitAndTagBeforePublish)
                CommitAndTagRelease();

            var packageFile = PackSharpNinjaCbmEngineFromLastTag();
            PushNuGetPackage(packageFile);
        });

    Target InstallDeps => _ => _
        .Description("Locates or installs CC65, preferring Chocolatey then Scoop.")
        .Executes(() =>
        {
            if (TryLogCc65Tools())
                return;

            var choco = FindExecutable("choco", includePowerShellScripts: false);
            if (choco is not null)
            {
                Log.Information("Installing CC65 with Chocolatey package {Package}.", ChocolateyCc65Package);
                RunProcess(choco, "install", ChocolateyCc65Package, "-y", "--no-progress");
                EnsureCc65Available();
                return;
            }

            var scoop = FindExecutable("scoop", includePowerShellScripts: false);
            if (scoop is not null)
            {
                Log.Information("Installing CC65 with Scoop package {Package}.", ScoopCc65Package);
                RunProcess(scoop, "install", ScoopCc65Package);
                EnsureCc65Available();
                return;
            }

            throw new InvalidOperationException(
                "CC65 was not found, and neither Chocolatey (choco) nor Scoop (scoop) was found on PATH. Install one package manager, then rerun InstallDeps.");
        });

    void PackSharpNinjaCbmEngineFromSource(AbsolutePath sourceRoot, AbsolutePath outputDirectory, bool noBuild)
    {
        var packageProject = sourceRoot / "src" / "SharpNinja.CbmEngine" / "SharpNinja.CbmEngine.csproj";
        RunProcessIn(sourceRoot, "dotnet", "restore", packageProject, "--verbosity", "minimal", "/p:RestoreFallbackFolders=");

        var packArguments = new List<string>
        {
            "pack",
            packageProject,
            "--no-restore",
            "--configuration",
            Configuration,
            "--output",
            outputDirectory,
            "--verbosity",
            "minimal"
        };

        if (noBuild)
            packArguments.Insert(2, "--no-build");

        RunProcessIn(sourceRoot, "dotnet", packArguments.ToArray());
    }

    AbsolutePath PackSharpNinjaCbmEngineFromLastTag()
    {
        var tag = GetLastTag();
        var tagDirectoryName = SanitizePathSegment(tag);
        var worktreeDirectory = TaggedSourceDirectory / tagDirectoryName;
        var outputDirectory = TaggedPackagesDirectory / tagDirectoryName;

        DeleteDirectoryIfExists(worktreeDirectory);
        PrepareDirectory(outputDirectory);

        Log.Information("Packing {PackageId} from Git tag {Tag}.", PackageId, tag);
        RunGit("worktree", "add", "--detach", worktreeDirectory, tag);

        try
        {
            PackSharpNinjaCbmEngineFromSource(worktreeDirectory, outputDirectory, noBuild: false);
            var packages = Directory.GetFiles(outputDirectory, $"{PackageId}.*.nupkg")
                .Where(x => !x.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            if (packages.Length == 0)
                throw new InvalidOperationException($"No {PackageId} package was created in {outputDirectory}.");

            return packages[0];
        }
        finally
        {
            TryRunGit("worktree", "remove", "--force", worktreeDirectory);
            TryRunGit("worktree", "prune");
        }
    }

    void CommitAndTagRelease()
    {
        RunProcessIn(RootDirectory, "dotnet", "tool", "restore");
        RunGit("add", "--all");

        var status = CaptureGit("status", "--porcelain").Trim();
        if (status.Length > 0)
            RunGit("commit", "-m", ReleaseCommitMessage);
        else
            Log.Information("No working-tree changes to commit before tagging.");

        var version = GetGitVersionValue("MajorMinorPatch");
        var tag = ReleaseTagPrefix + version;
        if (GitTagExists(tag))
            throw new InvalidOperationException($"Release tag '{tag}' already exists.");

        RunGit("tag", "-a", tag, "-m", $"Release {tag}");
        Log.Information("Created release tag {Tag}.", tag);
    }

    string GetGitVersionValue(string propertyName)
    {
        var json = CaptureProcessIn(
            RootDirectory,
            "dotnet",
            "tool",
            "run",
            "dotnet-gitversion",
            "--",
            "/output",
            "json");

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(propertyName, out var property))
            throw new InvalidOperationException($"GitVersion output did not contain '{propertyName}'.");

        return property.GetString() ?? throw new InvalidOperationException($"GitVersion '{propertyName}' was null.");
    }

    string GetLastTag()
    {
        try
        {
            var tag = CaptureGit("describe", "--tags", "--abbrev=0").Trim();
            if (!string.IsNullOrWhiteSpace(tag))
                return tag;
        }
        catch
        {
        }

        throw new InvalidOperationException(
            "No Git tags exist. Create a release tag manually, or run PublishNuGet with --commit-and-tag-before-publish to create the initial tag.");
    }

    bool GitTagExists(string tag)
    {
        return CaptureGit("tag", "--list", tag)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(x => string.Equals(x.Trim(), tag, StringComparison.Ordinal));
    }

    void PushNuGetPackage(AbsolutePath packageFile)
    {
        var apiKey = NuGetApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("NuGet API key is required. Pass --nuget-api-key or set NUGET_API_KEY.");

        var arguments = new List<string>
        {
            "nuget",
            "push",
            packageFile,
            "--source",
            NuGetSource,
            "--api-key",
            apiKey
        };

        if (NuGetSkipDuplicate)
            arguments.Add("--skip-duplicate");

        RunProcessIn(RootDirectory, "dotnet", arguments.ToArray());
    }

    void RunDotNet(string command, AbsolutePath target, params string[] arguments)
    {
        var combined = new List<string> { command, target };
        combined.AddRange(arguments);
        RunProcessIn(RootDirectory, "dotnet", combined.ToArray());
    }

    void RunGit(params string[] arguments)
    {
        RunProcessIn(RootDirectory, "git", arguments);
    }

    bool TryRunGit(params string[] arguments)
    {
        try
        {
            RunGit(arguments);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Git command failed during cleanup: {Arguments}", string.Join(" ", arguments));
            return false;
        }
    }

    string CaptureGit(params string[] arguments)
    {
        return CaptureProcessIn(RootDirectory, "git", arguments);
    }

    static void RunProcess(string fileName, params string[] arguments)
    {
        RunProcessIn(Directory.GetCurrentDirectory(), fileName, arguments);
    }

    static void RunProcessIn(string workingDirectory, string fileName, params string[] arguments)
    {
        Log.Information("> {Command}", FormatCommand(fileName, arguments));

        using var process = CreateProcess(workingDirectory, fileName, arguments);
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Log.Information("{Line}", args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Log.Error("{Line}", args.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Command failed with exit code {process.ExitCode}: {FormatCommand(fileName, arguments)}");
    }

    static string CaptureProcessIn(string workingDirectory, string fileName, params string[] arguments)
    {
        Log.Information("> {Command}", FormatCommand(fileName, arguments));

        using var process = CreateProcess(workingDirectory, fileName, arguments);
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Command failed with exit code {process.ExitCode}: {FormatCommand(fileName, arguments)}{Environment.NewLine}{stderr}{stdout}");

        if (!string.IsNullOrWhiteSpace(stderr))
            Log.Debug("{ErrorOutput}", stderr.Trim());

        return stdout;
    }

    static Process CreateProcess(string workingDirectory, string fileName, IReadOnlyCollection<string> arguments)
    {
        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        return process;
    }

    static void EnsureCc65Available()
    {
        var ca65 = FindExecutable("ca65");
        var ld65 = FindExecutable("ld65");
        if (ca65 is not null && ld65 is not null)
        {
            Log.Information("CC65 tools found. ca65={Ca65}; ld65={Ld65}", ca65, ld65);
            return;
        }

        throw new InvalidOperationException(
            "CC65 tools were not found. Run the InstallDeps target, or install CC65 manually and ensure ca65 and ld65 are on PATH.");
    }

    static bool TryLogCc65Tools()
    {
        var ca65 = FindExecutable("ca65");
        var ld65 = FindExecutable("ld65");
        if (ca65 is null || ld65 is null)
            return false;

        Log.Information("CC65 already available. ca65={Ca65}; ld65={Ld65}", ca65, ld65);
        return true;
    }

    static string? FindExecutable(string name, bool includePowerShellScripts = true)
    {
        foreach (var directory in GetExecutableSearchDirectories())
        {
            foreach (var candidate in GetExecutableCandidates(name, includePowerShellScripts))
            {
                var fullPath = Path.Combine(directory, candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        return null;
    }

    static IEnumerable<string> GetExecutableCandidates(string name, bool includePowerShellScripts)
    {
        if (Path.HasExtension(name))
        {
            yield return name;
            yield break;
        }

        yield return name;

        if (!OperatingSystem.IsWindows())
            yield break;

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(includePowerShellScripts ? new[] { ".PS1" } : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in extensions)
            yield return name + extension.ToLowerInvariant();
    }

    static IEnumerable<string> GetExecutableSearchDirectories()
    {
        var directories = new List<string>();
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        directories.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (OperatingSystem.IsWindows())
        {
            var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddIfPresent(directories, Path.Combine(commonApplicationData, "chocolatey", "bin"));
            AddIfPresent(directories, Path.Combine(userProfile, "scoop", "shims"));
            AddIfPresent(directories, Path.Combine(Environment.GetEnvironmentVariable("SCOOP") ?? string.Empty, "shims"));
            AddIfPresent(directories, Path.Combine(Environment.GetEnvironmentVariable("SCOOP_GLOBAL") ?? string.Empty, "shims"));
        }

        return directories
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);
    }

    static void AddIfPresent(List<string> directories, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            directories.Add(path);
    }

    static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(x => invalid.Contains(x) ? '_' : x).ToArray());
    }

    static void PrepareDirectory(string path)
    {
        DeleteDirectoryIfExists(path);
        Directory.CreateDirectory(path);
    }

    static string FormatCommand(string fileName, IReadOnlyCollection<string> arguments)
    {
        return string.Join(" ", new[] { fileName }.Concat(arguments.Select(QuoteArgument)));
    }

    static string QuoteArgument(string argument)
    {
        return argument.Any(char.IsWhiteSpace) ? $"\"{argument}\"" : argument;
    }

    static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
