using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AppleDust.Cli;

internal sealed class StatusDisplay : IRenderable
{
    private readonly Table _table;
    private readonly Rows _rows;
    private readonly IReadOnlyList<Benchmark> _benchmarks;

    public StatusDisplay(IReadOnlyList<Benchmark> benchmarks)
    {
        _benchmarks = benchmarks;
        var headers = new TableRow().GetColumns().Select(c => c.Name).ToArray();
        _table = new Table()
        .Title("Benchmark Results")
        .AddColumns(headers)
        .ShowRowSeparators();
        var legend = new Markup("""

        """);
        _rows = new Rows(_table, legend);
    }

    public void SetCaption(string caption)
    {
        _ = _table.Caption(caption);
    }

    private abstract record TableColumn(string Name)
    {
        public abstract Markup GetMarkup();
        public Style Style = Style.Plain;
        public Justify Justification = Justify.Right;
        protected static readonly Markup EmptyMarkup = new("");
        protected static readonly Markup NA = new("n/a", Styles.Dim);
    }
    private record NumberColumn(string Name, Func<double, string> FormatFunc) : TableColumn<double?>(Name, (s) => s is not null ? FormatFunc(s.Value) : "")
    {
        public NumberColumn(string name, [StringSyntax(StringSyntaxAttribute.NumericFormat)] string Format) : this(name, (d) =>
        {
            if (double.IsNaN(d))
            {
                return "NaN";
            }
            return d.ToString(Format);
        })
        { }
    }
    private record StringColumn(string Name) : TableColumn<string>(Name, static (s) => s);
    private record TableColumn<T>(string Name, Func<T, string> Func) : TableColumn(Name)
    {
        public T? Value;
        public override Markup GetMarkup()
        {
            if (Value is null)
            {
                return EmptyMarkup;
            }
            if (Value is double d && double.IsNaN(d))
            {
                return NA.Justify(Justification);
            }
            return new Markup(Func(Value), Style).Justify(Justification);
        }
    }

    private sealed record TableRow
    {
        public readonly StringColumn Name = new("Name") { Justification = Justify.Left };
        public readonly NumberColumn Mean = new("Mean", Utils2.AsTime);
        //public readonly NumberColumn StdDev = new("SD", Utils2.AsTime);
        public readonly NumberColumn StdDevRel = new("SD%", "P1");
        public readonly NumberColumn Ratio = new("Ratio", "P1");
        public readonly NumberColumn RatioMargin = new("Ratio Error", "P2");
        //public readonly NumberColumn TStat = new("t-stat", "F2");
        public readonly NumberColumn PValue = new("p-value", "G2");
        public readonly NumberColumn Score = new("Score", "F2");

        public readonly NumberColumn Memory = new("Alloc", "N0");
        public readonly NumberColumn MemorySD = new("Alloc SD", "F1");
        public readonly NumberColumn MemoryRatio = new("Alloc Ratio", "P1");
        public readonly NumberColumn Samples = new("Samples", "N0");
        // public readonly NumberColumn Iterations = new("Iterations", "N0");

        private static readonly FieldInfo[] columnFields = typeof(TableRow)
            .GetFields()
            .Where(f => f.FieldType.IsAssignableTo(typeof(TableColumn)))
            .ToArray();

        public TableColumn[] GetColumns()
        {
            return columnFields
                .Select(f => (TableColumn)f.GetValue(this)!)
                .ToArray();
        }
    };

    /// <inheritdoc />
    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        return (_rows as IRenderable).Measure(options, maxWidth);
    }

    /// <inheritdoc />
    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        return (_rows as IRenderable).Render(options, maxWidth);
    }

    public void Refresh()
    {
        _table.Rows.Clear();
        _ = _table.Title($"Benchmark Results");
        var rows = new List<TableRow>(_benchmarks.Count);
        var baseline = _benchmarks.Single(b => b.IsBaseline);
        foreach (var bench in _benchmarks)
        {
            var row = new TableRow();

            var name = bench.Name;
            var stat = bench.Stats;
            var mean = stat.Mean;

            row.Name.Value = name;
            row.Mean.Value = mean;
            row.Mean.Style = mean < 0 ? Styles.Red : Styles.Plain;
            //row.StdDev.Value = stat.StdDev;
            row.StdDevRel.Value = stat.StdDev / Math.Abs(mean);
            //row.StdError.Value = stat.StdErr;
            //row.StdErrorRel.Value = stat.StdErr / Math.Abs(mean);
            row.Samples.Value = stat.Samples.Length;
            // row.Iterations.Value = bench.Iterations;

            var samples = stat.Samples;
            var baseSamples = baseline.Stats.Samples;

            if (bench.IsBaseline)
            {
                row.Ratio.Value = 1;
                row.Name.Style = Styles.Underline;
            }
            else if (bench.IsOverhead)
            {
                row.Name.Style = Styles.Dim;
            }
            else
            {
                var (_, _, pValue) = Utils2.WelchTest(samples, baseSamples);

                var (ratio, score, ratioMargin) = GetRatioScore(samples, baseSamples, 0.1);
                var ratioStyle = GetRatioStyle(ratio, score, pValue);

                row.Ratio.Value = ratio;
                row.Ratio.Style = ratioStyle;
                row.RatioMargin.Value = ratioMargin;
                row.PValue.Value = pValue;
                const double significanceLevel = 0.01;
                row.PValue.Style = SignificanceColor(pValue <= significanceLevel);
                row.Score.Value = score;
                row.Score.Style = SignificanceColor(score > 1);
            }

            // Memory measurements
            {
                row.Memory.Value = bench.GcStats.Mean;
                row.MemorySD.Value = bench.GcStats.StdDev;

                if (bench.IsBaseline)
                {
                    row.MemoryRatio.Value = 1;
                }
                else if (!bench.IsOverhead)
                {
                    var (_, _, pValue) = Utils2.WelchTest(bench.GcStats.Samples, baseline.GcStats.Samples);

                    var (ratio, score, _) = GetRatioScore(bench.GcStats.Samples, baseline.GcStats.Samples, 0.1);
                    var ratioStyle = GetRatioStyle(ratio, score, pValue);
                    row.MemoryRatio.Value = ratio;
                    row.MemoryRatio.Style = ratioStyle;
                }
            }
            rows.Add(row);
            _ = _table.AddRow(row.GetColumns().Select(c => c.GetMarkup()));
        }
    }

    private static (double Ratio, double Score, double Margin) GetRatioScore(ImmutableArray<double> samples, ImmutableArray<double> baseSamples, double error)
    {
        const int minLength = 4;
        if (samples.Length < minLength || baseSamples.Length < minLength)
        {
            if (samples.Length > 0 && baseSamples.Length > 0)
            {
                var r = samples.Average() / baseSamples.Average();
                return (r, double.NaN, double.NaN);
            }
            return (double.NaN, double.NaN, double.NaN);
        }
        var (ratio, margin) = Utils2.GetRatio(samples, baseSamples, error);
        return (ratio, Math.Abs(ratio - 1) / margin, margin);
    }

    private static class Styles
    {
        public static readonly Style Plain = new();
        public static readonly Style Red = new(Color.Red);
        public static readonly Style Green = new(Color.Green);
        public static readonly Style GreenYellow = new(Color.GreenYellow);
        public static readonly Style Yellow = new(Color.Yellow);
        public static readonly Style Orange = new(Color.Orange1);
        public static readonly Style Underline = new(decoration: Decoration.Underline);
        public static readonly Style Dim = new(decoration: Decoration.Dim);
    }

    private static Style GetRatioStyle(double ratio, double score, double pValue)
    {
        const double significanceLevel = 0.01;
        if (double.IsNaN(ratio))
        {
            return Styles.Dim;
        }
        var points = 0;
        if (pValue <= significanceLevel)
            points++;
        if (score > 1)
            points++;

        return ColorCode(ratio < 1, points);
    }

    private static Style ColorCode(bool good, int strength)
    {
        return GetColor(good ? strength : -strength);
    }

    private static Style GetColor(int index)
    {
        return index switch
        {
            2 => Styles.Green,
            1 => Styles.GreenYellow,
            0 => Styles.Yellow,
            -1 => Styles.Orange,
            -2 => Styles.Red,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
        };
    }

    private static Style SignificanceColor(bool pass)
    {
        return pass ? Styles.Green : Styles.Yellow;
    }
}
