namespace CbmEngine.Tools.CbmVidStudio;

/// <summary>
/// Pure sizing math for the Studio window, extracted so it can be unit tested without a
/// GraphicsDevice. The UI is authored against a 1280x800 logical canvas; Desktop.Scale maps
/// that canvas onto whatever client area the OS actually gives us.
/// </summary>
public static class StudioLayoutMath
{
    public const int LogicalWidth = 1280;
    public const int LogicalHeight = 800;

    /// <summary>
    /// Initial client size request: the logical canvas multiplied by the monitor DPI factor
    /// (so text lands at its designed physical size), capped to a fraction of the display so the
    /// window always fits, floored to a usable minimum.
    /// </summary>
    public static (int Width, int Height) ComputeInitialClientSize(int displayWidth, int displayHeight, float dpiFactor)
    {
        float f = MathF.Max(1f, dpiFactor);
        int w = (int)MathF.Min(LogicalWidth * f, displayWidth * 0.92f);
        int h = (int)MathF.Min(LogicalHeight * f, displayHeight * 0.85f);
        return (Math.Max(w, 960), Math.Max(h, 600));
    }
}
