using System.Runtime.InteropServices;

namespace CbmEngine.Tools.CbmVidStudio;

public static partial class DpiNormalizer
{
    private const float DefaultDpi = 96.0f;

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2. dotnet tool shims do not embed the project's
    // app.manifest, so the installed cbmvid.exe starts DPI-unaware and Windows virtualizes every
    // coordinate we read (window rects, display modes, GetDpiForWindow all lie). Calling this
    // before any window/SDL initialization opts the process in programmatically.
    private static readonly nint PerMonitorAwareV2 = -4;

    public static bool TryEnablePerMonitorV2()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { return SetProcessDpiAwarenessContext(PerMonitorAwareV2); }
        catch (EntryPointNotFoundException) { return false; }
        catch (DllNotFoundException) { return false; }
    }

    /// <summary>Monitor DPI factor for a window (1.0 = 96 DPI, 1.5 = 144, 2.0 = 192...).</summary>
    public static float WindowDpiFactor(nint windowHandle) => TryGetDpiForWindow(windowHandle) / DefaultDpi;

    /// <summary>System DPI factor (valid after TryEnablePerMonitorV2; 1.0 when unaware/virtualized).</summary>
    public static float SystemDpiFactor()
    {
        float f = TryGetDpiForSystem() / DefaultDpi;
        return f < 0.5f ? 1.0f : f;
    }

    private static float TryGetDpiForWindow(nint hwnd)
    {
        if (hwnd == 0) return DefaultDpi;
        if (!OperatingSystem.IsWindows()) return DefaultDpi;
        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            return dpi == 0 ? DefaultDpi : dpi;
        }
        catch (EntryPointNotFoundException) { return DefaultDpi; }
        catch (DllNotFoundException) { return DefaultDpi; }
    }

    private static float TryGetDpiForSystem()
    {
        if (!OperatingSystem.IsWindows()) return DefaultDpi;
        try
        {
            uint dpi = GetDpiForSystem();
            return dpi == 0 ? DefaultDpi : dpi;
        }
        catch (EntryPointNotFoundException) { return DefaultDpi; }
        catch (DllNotFoundException) { return DefaultDpi; }
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(nint value);
}
