using System.Text.RegularExpressions;

namespace SfWebAPIHeraldo.Models;

public static partial class TaxNumberNormalizer
{
    [GeneratedRegex(@"[.\s-]")]
    private static partial Regex Separators();

    public static string Normalize(string value) =>
        Separators().Replace(value.Trim(), string.Empty);
}
