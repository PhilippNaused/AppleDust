using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AppleDust.Cli;

internal sealed class ResultTable : IRenderable
{
    private readonly Table _table;
    private readonly IReadOnlyList<Benchmark> _benchmarks;
    private readonly Lock _lock = new();

    public ResultTable(IReadOnlyList<Benchmark> benchmarks)
    {
        _benchmarks = benchmarks;
        var headers = new TableRow().GetColumns().Select(c => c.Name).ToArray();
        _table = new Table()
        .Title("Benchmark Results")
        .AddColumns(headers)
        .ShowRowSeparators();
    }

    public void SetBorderColor(Color color)
    {
        _ = _table.BorderColor(color);
    }

    private abstract class TableColumn(string name)
    {
        public string Name => name;
        public abstract Markup GetMarkup();
        public Style Style = Style.Plain;
        public Justify Justification = Justify.Right;
        protected static readonly Markup EmptyMarkup = new("");
        protected static readonly Markup NA = new("n/a", Styles.Dim);
    }
    private class NumberColumn(string name, Func<double, string> formatFunc) : TableColumn<double?>(name, s => s is not null ? formatFunc(s.Value) : "")
    {
        public NumberColumn(string name, [StringSyntax(StringSyntaxAttribute.NumericFormat)] string format, bool forceSign = false) : this(name, d =>
        {
            if (double.IsNaN(d))
            {
                return "NaN";
            }
            var text = d.ToString(format);
            return forceSign && d > 0 ? "+" + text : text;
        })
        { }
    }
    private class StringColumn(string name) : TableColumn<string>(name, static s => s);
    private class TableColumn<T>(string name, Func<T, string> func) : TableColumn(name)
    {
        public T? Value;
        public override Markup GetMarkup()
        {
            if (Value is null)
            {
                return EmptyMarkup;
            }
            if (Value is double.NaN)
            {
                return NA.Justify(Justification);
            }
            return new Markup(Markup.Escape(func(Value)), Style).Justify(Justification);
        }
    }

    private sealed record TableRow
    {
        public readonly StringColumn Name = new("Name") { Justification = Justify.Left };
        public readonly NumberColumn Center = new("Center", Utils2.AsTime);
        // public readonly NumberColumn Spread = new("Spread", Utils2.AsTime);
        public readonly NumberColumn SpreadRel = new("Spread", "P1");
        public readonly NumberColumn Ratio = new("Ratio", "P2");
        public readonly NumberColumn Shift = new("Shift", Utils2.AsTime);
        public readonly NumberColumn Disparity = new("Disparity", "P1");
        public readonly NumberColumn PValue = new("p-Value", "G2");

        public readonly NumberColumn Alloc = new("Alloc", FormatAlloc);
        public readonly NumberColumn AllocRatio = new("Alloc Ratio", "P2");
        public readonly NumberColumn Samples = new("Samples", "N0");
        // public readonly NumberColumn Iterations = new("Iterations", "N0");
        public readonly NumberColumn Outliers = new("Outliers", "N0");
        public readonly StringColumn Status = new("Status") { Justification = Justify.Left };

        private static string FormatAlloc(double bytes)
        {
            if (bytes < 1000)
                return bytes.ToString("G3"); // Only display decimals for small numbers
            return bytes.ToString("N0");
        }

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
    Measurement IRenderable.Measure(RenderOptions options, int maxWidth)
    {
        lock (_lock)
            return (_table as IRenderable).Measure(options, maxWidth);
    }

    /// <inheritdoc />
    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth)
    {
        lock (_lock)
            return (_table as IRenderable).Render(options, maxWidth);
    }

    public void Refresh()
    {
        using var l = _lock.EnterScope();
        _table.Rows.Clear();
        foreach (var bench in _benchmarks)
        {
            var row = new TableRow();

            var stat = bench.Stats;

            row.Name.Value = bench.Name;

            row.Center.Value = stat.Center;
            row.Center.Style = stat.Center < 0 ? Styles.Red : Styles.Plain;
            // row.Spread.Value = stat.Spread;
            row.SpreadRel.Value = stat.Spread / Math.Abs(stat.Center);
            row.SpreadRel.Style = Utils2.GetColor(Utils2.ScoreDev(stat.Center, stat.Spread));

            row.Samples.Value = stat.Samples.Length;
            // row.Iterations.Value = bench.Iterations;
            row.Outliers.Value = bench.Outliers;
            row.Status.Value = bench.GetStatus();

            var samples = stat.Samples;

            if (bench.IsBaseline)
            {
                row.Ratio.Value = 1;
                if (bench.IsOverhead)
                    row.Ratio.Value = double.NaN;
                row.Ratio.Style = Styles.Dim;
                row.Name.Style = Styles.Underline;
            }
            else
            {
                var baseline = bench.Baseline!;
                var baseSamples = baseline.Stats.Samples;
                var (ratio, shift, disparity, pValue) = Utils2.CompareToBaseline(samples, baseSamples);
                var ratioStyle = Utils2.GetRatioStyle(ratio, disparity, pValue);

                row.Ratio.Value = ratio;
                row.Ratio.Style = ratioStyle;
                row.Shift.Value = shift;
                row.Shift.Style = ratioStyle;
                row.PValue.Value = pValue;
                const double significanceLevel = 0.01;
                row.PValue.Style = Utils2.SignificanceColor(pValue <= significanceLevel);
                row.Disparity.Value = disparity;
                row.Disparity.Style = Utils2.SignificanceColor(Math.Abs(disparity) > 1);
            }

            if (bench.IsOverhead)
            {
                row.Name.Style = Styles.Dim;
            }

            // Memory measurements
            {
                row.Alloc.Value = bench.GcStats.Center;

                if (bench.IsBaseline)
                {
                    row.AllocRatio.Value = 1;
                    if (bench.IsOverhead)
                        row.AllocRatio.Value = double.NaN;
                    row.AllocRatio.Style = Styles.Dim;
                }
                else
                {
                    const double allocEps = 0.1; // only compare if both are above 0.1 bytes, to avoid noise in the ratio.
                    if (bench.GcStats.Center > allocEps && bench.Baseline!.GcStats.Center > allocEps)
                    {
                        var (ratio, shift, disparity, pValue) = Utils2.CompareToBaseline(bench.GcStats.Samples, bench.Baseline!.GcStats.Samples);
                        row.AllocRatio.Value = ratio;
                        row.AllocRatio.Style = Utils2.GetRatioStyle(ratio, disparity, pValue);
                    }
                    else
                    {
                        row.AllocRatio.Value = double.NaN;
                        row.AllocRatio.Style = Styles.Dim;
                    }
                }
            }
            _ = _table.AddRow(row.GetColumns().Select(c => c.GetMarkup()));
        }
    }
}
