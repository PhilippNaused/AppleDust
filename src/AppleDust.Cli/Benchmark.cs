using System.Collections.Immutable;
using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class Benchmark(AppleServer server, string name, int iterations)
{
    private readonly List<double> _samplesRaw = new(50);
    private readonly List<double> _samplesGcRaw = new(50);

    public string Name => name;
    public int Iterations { get; private set; } = iterations;
    public Stats Stats { get; private set; } = Stats.NaN;
    public Stats GcStats { get; private set; } = Stats.NaN;
    public bool IsBaseline { get; set; }
    public bool IsOverhead => Name == Utils.OverheadBenchmarkName;
    public Benchmark? Overhead { get; set; }
    private ImmutableArray<double> Sanitize(List<double> samples, double? overhead)
    {
        var sanitized = samples.Where(double.IsFinite);
        if (overhead.HasValue && !double.IsNaN(overhead.Value) && !IsOverhead)
        {
            sanitized = sanitized.Select(s => s - overhead.Value);
        }
        return [.. sanitized];
    }

    public async Task GetSampleAsync()
    {
        (long nanos, long bytes) = await server.GetSample(name, Iterations);
        var timeSample = (double)nanos / Iterations;
        var memorySample = (double)bytes / Iterations;
        Iterations = (int)(Utils2.TargetNs / timeSample);
        Iterations = Math.Max(Iterations, Utils.MinIterations);
        _samplesRaw.Add(timeSample);
        _samplesGcRaw.Add(memorySample);
        Stats = Utils2.Analyze(Sanitize(_samplesRaw, Overhead?.Stats.Mean));
        GcStats = Utils2.Analyze(Sanitize(_samplesGcRaw, Overhead?.GcStats.Mean));
    }

    public void Reset()
    {
        _samplesRaw.Clear();
        _samplesGcRaw.Clear();
        Stats = Stats.NaN;
        GcStats = Stats.NaN;
    }
}
