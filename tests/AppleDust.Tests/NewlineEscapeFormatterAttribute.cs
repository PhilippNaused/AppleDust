using AppleDust.Tests;

[assembly: NewlineEscapeFormatter]

namespace AppleDust.Tests;

/// <summary>
/// Escapes Newline characters in test names.
/// </summary>
/// <remarks>
/// Needed since 'EnricoMi/publish-unit-test-result-action@v2' has trouble detecting added/removed tests when the name contains newline characters.
/// </remarks>
public sealed class NewlineEscapeFormatterAttribute : DisplayNameFormatterAttribute
{
    protected override string FormatDisplayName(DiscoveredTestContext context)
    {
        return context.GetDisplayName().Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
