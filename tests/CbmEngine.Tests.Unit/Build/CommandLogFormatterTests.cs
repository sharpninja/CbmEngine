using System.Text.RegularExpressions;

namespace CbmEngine.Tests.Unit.Build;

public sealed class CommandLogFormatterTests
{
    const string Secret = "nuget-secret-value";

    [Fact]
    public void Format_RedactsSeparateApiKeyValue()
    {
        var command = CommandLogFormatter.Format(
            "dotnet",
            ["nuget", "push", "package.nupkg", "--api-key", Secret, "--skip-duplicate"]);

        Assert.DoesNotContain(Secret, command, StringComparison.Ordinal);
        Assert.Contains("--api-key ***REDACTED***", command, StringComparison.Ordinal);
        Assert.Contains("--skip-duplicate", command, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_RedactsInlineApiKeyValue()
    {
        var command = CommandLogFormatter.Format(
            "dotnet",
            ["nuget", "push", "package.nupkg", $"--api-key={Secret}"]);

        Assert.DoesNotContain(Secret, command, StringComparison.Ordinal);
        Assert.Contains("--api-key=***REDACTED***", command, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_PreservesAndQuotesNonSecretArguments()
    {
        var command = CommandLogFormatter.Format(
            "dotnet",
            ["nuget", "push", "package with spaces.nupkg", "--source", "https://api.nuget.org/v3/index.json"]);

        Assert.Equal(
            "dotnet nuget push \"package with spaces.nupkg\" --source https://api.nuget.org/v3/index.json",
            command);
    }

    [Fact]
    public void Format_DoesNotMutateProcessArguments()
    {
        string[] arguments = ["nuget", "push", "package.nupkg", "--api-key", Secret];
        var originalArguments = arguments.ToArray();

        _ = CommandLogFormatter.Format("dotnet", arguments);

        Assert.Equal(originalArguments, arguments);
    }

    [Fact]
    public void NuGetApiKeyParameter_IsMarkedSecret()
    {
        var buildSource = File.ReadAllText(FindRepositoryFile("_build", "Build.cs"));

        Assert.Matches(
            new Regex(@"\[Secret\]\s*readonly string\? NuGetApiKey", RegexOptions.CultureInvariant),
            buildSource);
    }

    static string FindRepositoryFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate repository file '{Path.Combine(relativeParts)}'.");
    }
}
