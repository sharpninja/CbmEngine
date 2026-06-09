using System.Diagnostics;

namespace CbmEngine.Systems.Cartridge;

public sealed class Ca65Assembler
{
    public string Ca65Path { get; }
    public string Ld65Path { get; }

    public Ca65Assembler(string? ca65Path = null, string? ld65Path = null)
    {
        Ca65Path = ca65Path ?? FindOnPath("ca65") ?? throw new FileNotFoundException("ca65 executable not found on PATH. Install the CC65 toolchain (https://cc65.github.io).");
        Ld65Path = ld65Path ?? FindOnPath("ld65") ?? throw new FileNotFoundException("ld65 executable not found on PATH. Install the CC65 toolchain (https://cc65.github.io).");
    }

    public static bool IsAvailable() => FindOnPath("ca65") is not null && FindOnPath("ld65") is not null;

    public byte[] Build(string asmSource, string linkerConfig, IReadOnlyDictionary<string, byte[]>? includeBinaries = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(asmSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(linkerConfig);

        string work = Path.Combine(Path.GetTempPath(), "cbm-ca65-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(work);
        try
        {
            string asmPath = Path.Combine(work, "input.s");
            string cfgPath = Path.Combine(work, "link.cfg");
            string objPath = Path.Combine(work, "input.o");
            string binPath = Path.Combine(work, "output.bin");

            File.WriteAllText(asmPath, asmSource);
            File.WriteAllText(cfgPath, linkerConfig);

            if (includeBinaries is not null)
                foreach (var (name, bytes) in includeBinaries)
                    File.WriteAllBytes(Path.Combine(work, name), bytes);

            RunTool(Ca65Path, $"-t none -o \"{objPath}\" \"{asmPath}\"", work);
            RunTool(Ld65Path, $"-C \"{cfgPath}\" -o \"{binPath}\" \"{objPath}\"", work);

            if (!File.Exists(binPath))
                throw new InvalidOperationException($"ld65 did not produce '{binPath}'.");
            return File.ReadAllBytes(binPath);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { }
        }
    }

    private static void RunTool(string exe, string args, string workingDir)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {exe}.");
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30_000);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(exe)} exited with {proc.ExitCode}.\nArgs: {args}\nStdout: {stdout}\nStderr: {stderr}");
    }

    private static string? FindOnPath(string toolName)
    {
        string exeName = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null) return null;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            string candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
