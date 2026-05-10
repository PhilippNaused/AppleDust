using AppleDust.Tests;

[assembly: UnicodeEscapeFormatter]

namespace AppleDust.Tests;

/// <summary>
/// Escapes Newline characters in test names.
/// </summary>
/// <remarks>
/// Needed since 'EnricoMi/publish-unit-test-result-action@v2' has trouble detecting added/removed tests when the name contains certain characters.
/// </remarks>
public sealed class UnicodeEscapeFormatterAttribute : DisplayNameFormatterAttribute
{
    protected override string FormatDisplayName(DiscoveredTestContext context)
    {
        return EscapeUnicode(context.GetDisplayName());
    }

    private static string EscapeUnicode(string input)
    {
        return string.Concat(input.Select(selector));
        static string selector(char c) => c switch
        {
            '\n' => "\\n",
            '\r' => "\\r",
            _ => c.ToString()
        };
    }
}
