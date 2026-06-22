using System.Text.RegularExpressions;

namespace Quiz_Web.Helper;

public static class TextEncodingRepair
{
    private static readonly IReadOnlyDictionary<string, string> Replacements =
        new Dictionary<string, string>
        {
            ["Ch??ng"] = "Ch\u01b0\u01a1ng",
            ["Bai h?c"] = "Bai h\u1ecdc",
            ["B\u00e0i h?c"] = "B\u00e0i h\u1ecdc",
            ["b\u00e0i h?c"] = "b\u00e0i h\u1ecdc",
            ["kh\u00f3a h?c"] = "kh\u00f3a h\u1ecdc",
            ["Kh\u00f3a h?c"] = "Kh\u00f3a h\u1ecdc",
            ["h?c"] = "h\u1ecdc",
            ["m?i"] = "m\u1edbi",
            ["Ch\u00c6\u00b0\u00c6\u00a1ng"] = "Ch\u01b0\u01a1ng",
            ["B\u00c3\u00a0i h\u00c3\u00a1\u00c2\u00bb\u00c2\u008dc"] = "B\u00e0i h\u1ecdc",
            ["b\u00c3\u00a0i h\u00c3\u00a1\u00c2\u00bb\u00c2\u008dc"] = "b\u00e0i h\u1ecdc",
            ["kh\u00c3\u00b3a h\u00c3\u00a1\u00c2\u00bb\u00c2\u008dc"] = "kh\u00f3a h\u1ecdc",
            ["Kh\u00c3\u00b3a h\u00c3\u00a1\u00c2\u00bb\u00c2\u008dc"] = "Kh\u00f3a h\u1ecdc",
            ["h\u00c3\u00a1\u00c2\u00bb\u00c2\u008dc"] = "h\u1ecdc",
            ["m\u00c3\u00a1\u00c2\u00bb\u00e2\u20ac\u00bai"] = "m\u1edbi",
            ["T\u00c3\u00aan ch\u00c6\u00b0\u00c6\u00a1ng"] = "T\u00ean ch\u01b0\u01a1ng"
        };

    private static readonly IReadOnlyList<(Regex Pattern, string Replacement)> PatternReplacements =
        new List<(Regex Pattern, string Replacement)>
        {
            (new Regex(@"^(Ch\u01b0\u01a1ng\s+\d+)[\p{L}]+$", RegexOptions.Compiled), "$1"),
            (new Regex(@"^(B\u00e0i\s+h\u1ecdc\s+m\u1edbi)[\p{L}]+$", RegexOptions.Compiled), "$1")
        };

    public static string? Repair(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var repaired = value;
        foreach (var replacement in Replacements)
        {
            repaired = repaired.Replace(replacement.Key, replacement.Value);
        }

        foreach (var replacement in PatternReplacements)
        {
            repaired = replacement.Pattern.Replace(repaired, replacement.Replacement);
        }

        return repaired;
    }
}
