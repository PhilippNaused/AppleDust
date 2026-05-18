using System.Diagnostics;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AppleDust.Cli;

internal sealed class ProgressBar : IRenderable
{
    public ProgressBar(double value = 0d)
    {
        Value = value;
    }

    public int? Width { get; set; }
    public double Value
    {
        get;
        set
        {
            if (field is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Value), value, "Value must be between 0 and 1.");
            }
            field = value;
        }
    }
    public Style Style { get; set; } = Style.Plain;

    public const string UnicodeBars = " ▏▎▍▌▋▊▉█"; // https://www.unicode.org/charts/nameslist/n_2580.html
    public const char AsciiBar = '-';
    public const char EmptyBar = ' ';

    /// <inheritdoc />
    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(Width ?? maxWidth, maxWidth);
        return new Measurement(4, width);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToString(AnsiConsole.Console.Profile.Capabilities.Unicode, Width ?? 10);
    }

    public string ToString(bool unicode, int width)
    {
        var barWidth = width * Value;
        var fullBars = (int)barWidth; // floor
        if (!unicode)
        {
            return new string(AsciiBar, fullBars) + new string(EmptyBar, width - fullBars);
        }
        var fullChar = UnicodeBars[^1];
        if (Value >= 1)
        {
            return new string(fullChar, width);
        }
        var partialBarPercent = barWidth - fullBars;
        Debug.Assert(partialBarPercent is >= 0 and < 1);
        var partialChar = UnicodeBars[(int)Math.Round(partialBarPercent * (UnicodeBars.Length - 1))];
        var emptyChar = UnicodeBars[0];
        var sb = new StringBuilder(width)
            .Append(fullChar, fullBars);
        if (fullBars < width)
        {
            _ = sb.Append(partialChar)
                .Append(emptyChar, width - fullBars - 1);
        }
        return sb.ToString();
    }

    /// <inheritdoc />
    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(Width ?? maxWidth, maxWidth);
        var text = ToString(options.Unicode, width);
        return [new Segment(text, Style)];
    }
}
