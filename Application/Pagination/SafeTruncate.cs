namespace ILSpy.Mcp.Application.Pagination;

/// <summary>
/// UTF-16-safe string truncation helpers used by bounded-output use cases.
///
/// Naive <c>s[..maxChars]</c> slices by UTF-16 code-unit index, which can split
/// a surrogate pair for non-BMP characters (e.g. emoji appearing in <c>ldstr</c>
/// operands during IL disassembly). The resulting string carries an unpaired
/// surrogate that is invalid UTF-16 and will round-trip through JSON as
/// <c>U+FFFD</c> or raise encoder errors on strict consumers.
/// </summary>
public static class SafeTruncate
{
    /// <summary>
    /// Truncates <paramref name="s"/> to at most <paramref name="maxChars"/> UTF-16
    /// code units, backing off by one char if the cut would land between a high
    /// and low surrogate.
    /// </summary>
    public static string Chars(string s, int maxChars)
    {
        if (s.Length <= maxChars) return s;
        var cut = maxChars;
        // Do not cut between a high and low surrogate.
        if (cut > 0 && char.IsHighSurrogate(s[cut - 1])) cut--;
        return s[..cut];
    }
}
