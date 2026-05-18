using System.Diagnostics;
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

    public Dashboard(ResultTable resultTable, BenchmarkCollection collection)
    {
        _layout = new Layout("Root")
            .SplitRows(
                // new Layout("Header").Size(3),
                new Layout("Main"),
                new Layout("Footer").Size(3));

        _resultTable = resultTable;
        _collection = collection;

        Update();
    }

    public void Update()
    {
        var cpu = Utils2.GetTotalCpuUsage();
        CpuQuality = GetCpuQuality(cpu);
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
        _layout["Main"].Update(new Rows(_resultTable, new Markup(Markup.Escape(statusText))));
    }

    public int CpuQuality { get; private set; }

    Measurement IRenderable.Measure(RenderOptions options, int maxWidth) => ((IRenderable)_layout).Measure(options, maxWidth);

    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth) => ((IRenderable)_layout).Render(options, maxWidth);

    private TimeSpan GetUptime() => DateTime.Now - _startTime;

    private static string ToString(TimeSpan time)
    {
        return $"{time.Hours:N0}h {time.Minutes:00}m {time.Seconds:00}s";
    }

    private static int GetCpuQuality(double cpuUsage)
    {
        if (double.IsNaN(cpuUsage))
        {
            Debug.Assert(!OperatingSystem.IsWindows());
            return 1;
        }
        // 2 thread of work.
        var target = 2d / Environment.ProcessorCount;
        target = Math.Max(target, 0.1); // at least 10% CPU usage is expected.
        if (cpuUsage < target)
        {
            return 2; // great
        }
        if (cpuUsage < target * 1.5)
        {
            return 1; // good
        }
        if (cpuUsage < target * 2)
        {
            return 0; // meh
        }
        if (cpuUsage < target * 2.5)
        {
            return -1; // bad
        }
        return -2; // terrible
    }
}
