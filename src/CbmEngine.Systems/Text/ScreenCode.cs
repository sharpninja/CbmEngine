namespace CbmEngine.Systems.Text;

/// <summary>
/// Maps ASCII text to C64 screen codes for the uppercase character set: 'A'-'Z' =&gt; 1-26,
/// '0'-'9' =&gt; $30-$39, space =&gt; $20. Unknown characters map to space.
/// </summary>
public static class ScreenCode
{
    public const byte Space = 0x20;

    /// <summary>Convert one character to a C64 screen code (case-insensitive); unknown =&gt; space.</summary>
    public static byte FromAscii(char c)
    {
        c = char.ToUpperInvariant(c);
        if (c is >= 'A' and <= 'Z') return (byte)(c - 'A' + 1);
        if (c is >= '0' and <= '9') return (byte)c;        // '0'..'9' == 0x30..0x39
        if (c == ' ') return Space;
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
