using Spectre.Console;
using Spectre.Console.Rendering;

namespace AppleDust.Cli;

#pragma warning disable IDE0058 // Expression value is never used

internal class Dashboard : IRenderable
{
    private readonly Layout _layout;
    private readonly DateTime _startTime = DateTime.Now;
    private readonly ResultTable _resultTable;
    private readonly BenchmarkCollection _collection;
    private readonly CpuMonitor _cpuMonitor;

    public Dashboard(ResultTable resultTable, BenchmarkCollection collection, CpuMonitor cpuMonitor)
    {
        _layout = new Layout("Root")
            .SplitRows(
                // new Layout("Header").Size(3),
                new Layout("Main"),
                new Layout("Footer").Size(3));

        _resultTable = resultTable;
        _collection = collection;
        _cpuMonitor = cpuMonitor;

        Update();
    }

    public void Update()
    {
        _resultTable.Refresh();
        var cpu = _cpuMonitor.CpuUsage;
        CpuQuality = _cpuMonitor.CpuQuality;
        _layout["Footer"].Update(
            new Panel(new Columns(
                new Markup($"[dim]Runtime: {ToString(GetUptime())}[/]"),
                new Markup($"CPU: {cpu:P1}", Utils2.GetColor(CpuQuality))
                ))
                .BorderColor(Color.Grey));

        string statusText = _collection.State switch
        {
            BenchmarkCollection.StateEnum.Warmup => $"Warmup: [{new ProgressBar(_collection.WarmupProgress ?? 0) { Width = 25 }}]",
            BenchmarkCollection.StateEnum.Cooldown => "Cooldown...",
            BenchmarkCollection.StateEnum.Sampling => "Sampling...",
            BenchmarkCollection.StateEnum.Idle => "Idle...",
            _ => ""
        };
        const string helpText = """
        Press [bold]Delete[/] to reset benchmarks.
        """;
        _layout["Main"].Update(new Rows(
            _resultTable,
            Markup.FromInterpolated($"{statusText}"),
            new Markup(helpText)
            ));
    }

    public int CpuQuality { get; private set; }

    Measurement IRenderable.Measure(RenderOptions options, int maxWidth) => ((IRenderable)_layout).Measure(options, maxWidth);

    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth) => ((IRenderable)_layout).Render(options, maxWidth);

    private TimeSpan GetUptime() => DateTime.Now - _startTime;

    private static string ToString(TimeSpan time)
    {
        return $"{time.Hours:N0}h {time.Minutes:00}m {time.Seconds:00}s";
    }
}
