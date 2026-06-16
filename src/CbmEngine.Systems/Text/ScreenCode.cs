namespace CbmEngine.Systems.Text;

/// <summary>
/// Maps ASCII text to C64 screen codes for the uppercase character set: 'A'-'Z' =&gt; 1-26 (case
/// insensitive), and ASCII $20-$3F (space, punctuation, digits, ':;&lt;=&gt;?') maps 1:1 to screen
/// codes. Anything outside both ranges maps to space.
/// </summary>
public static class ScreenCode
{
    public const byte Space = 0x20;

    /// <summary>Convert one character to a C64 screen code (case-insensitive); unmapped =&gt; space.</summary>
    public static byte FromAscii(char c)
    {
        c = char.ToUpperInvariant(c);
        if (c is >= 'A' and <= 'Z') return (byte)(c - 'A' + 1);
        // CBMFR-010: ASCII $20-$3F (space, punctuation, digits, ':;<=>?') maps 1:1 to C64 screen
        // codes. Lets text bars carry the natural ASCII punctuation a game UI normally uses
        // (e.g. ">", "-") instead of collapsing those characters to space.
        if (c is >= ' ' and <= '?') return (byte)c;
        return Space;
    }

    /// <summary>Encode <paramref name="text"/> into <paramref name="dest"/> as screen codes.</summary>
    public static void Encode(ReadOnlySpan<char> text, Span<byte> dest)
    {
        if (dest.Length < text.Length)
            throw new ArgumentException($"dest must hold at least {text.Length} bytes.", nameof(dest));
        for (int i = 0; i < text.Length; i++) dest[i] = FromAscii(text[i]);
    }
}
