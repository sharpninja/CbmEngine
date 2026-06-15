namespace CbmEngine.Tools.CbmVidStudio;

/// <summary>
/// Append-only startup/resize diagnostics, written to %TEMP%\cbmvid-studio.log. The GUI has no
/// console, so this is the only way to see real (post-DPI-awareness) display/window numbers
/// when debugging sizing issues on end-user machines.
/// </summary>
public static class StudioDiag
{
    public static readonly string LogPath = Path.Combine(Path.GetTempPath(), "cbmvid-studio.log");

    public static void Reset()
    {
        try { File.WriteAllText(LogPath, $"--- cbmvid studio {DateTime.Now:O} ---{Environment.NewLine}"); }
        catch { /* diagnostics must never crash the app */ }
    }

    public static void Log(string message)
    {
        try { File.AppendAllText(LogPath, message + Environment.NewLine); }
        catch { /* ditto */ }
    }
}
