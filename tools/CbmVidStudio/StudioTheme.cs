using System.Collections;
using System.Reflection;
using FontStashSharp;
using Myra.Graphics2D.UI.Styles;

namespace CbmEngine.Tools.CbmVidStudio;

/// <summary>
/// DPI strategy: instead of Desktop.Scale (whose popup/dialog placement math is broken in
/// Myra 1.6.1 - dialogs center against unscaled bounds and land off-screen), we render at
/// native pixels and scale the THEME: a TTF font sized 12pt x dpiFactor is pushed into every
/// style in Stylesheet.Current via reflection, so labels, menus, list rows, and the built-in
/// FileDialog all size correctly. Structural widget metrics are scaled by the caller.
/// </summary>
public static class StudioTheme
{
    private static FontSystem? _fontSystem;

    /// <summary>Pixel height for a 12pt-equivalent font at the given DPI factor (16px @ 96dpi).</summary>
    public static int FontPixels(float dpiFactor) => Math.Max(12, (int)MathF.Round(16f * MathF.Max(1f, dpiFactor)));

    /// <summary>
    /// Loads a system TTF and overrides every SpriteFontBase property reachable from
    /// Stylesheet.Current. Must run BEFORE widgets are constructed (widgets copy style values
    /// at creation). Returns false when no usable TTF is found (default bitmap font stays).
    /// </summary>
    public static bool Apply(float dpiFactor)
    {
        byte[]? ttf = LoadSystemFont();
        if (ttf is null) return false;

        _fontSystem = new FontSystem();
        _fontSystem.AddFont(ttf);
        SpriteFontBase font = _fontSystem.GetFont(FontPixels(dpiFactor));

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        OverrideFonts(Stylesheet.Current, font, visited);
        return true;
    }

    private static byte[]? LoadSystemFont()
    {
        if (!OperatingSystem.IsWindows()) return null;
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var candidate in new[] { "segoeui.ttf", "tahoma.ttf", "arial.ttf" })
        {
            var path = Path.Combine(fonts, candidate);
            if (File.Exists(path))
            {
                try { return File.ReadAllBytes(path); }
                catch { /* try next */ }
            }
        }
        return null;
    }

    /// <summary>
    /// Recursive reflection walk constrained to Myra style objects and their collections;
    /// sets every settable SpriteFontBase-typed property to <paramref name="font"/>.
    /// </summary>
    private static void OverrideFonts(object node, SpriteFontBase font, HashSet<object> visited)
    {
        if (node is null || !visited.Add(node)) return;

        var type = node.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;

            if (prop.PropertyType == typeof(SpriteFontBase))
            {
                if (prop.CanWrite) prop.SetValue(node, font);
                continue;
            }

            // Recurse into Myra style objects and dictionaries of them.
            bool isStyleish = prop.PropertyType.Namespace?.StartsWith("Myra.Graphics2D.UI.Styles", StringComparison.Ordinal) == true;
            bool isDictionary = typeof(IDictionary).IsAssignableFrom(prop.PropertyType);
            if (!isStyleish && !isDictionary) continue;

            object? value;
            try { value = prop.GetValue(node); }
            catch { continue; }
            if (value is null) continue;

            if (value is IDictionary dict)
            {
                foreach (var entry in dict.Values)
                {
                    if (entry is not null && entry.GetType().Namespace?.StartsWith("Myra.Graphics2D.UI.Styles", StringComparison.Ordinal) == true)
                        OverrideFonts(entry, font, visited);
                }
            }
            else
            {
                OverrideFonts(value, font, visited);
            }
        }
    }
}
