using System.Collections.Immutable;
using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class Benchmark(AppleServer server, string name, int iterations)
{
    private readonly List<double> _samplesRaw = new(50);

    public string Name => name;
    public int Iterations { get; private set; } = iterations;
    public Stats Stats { get; private set; } = Stats.NaN;
    public bool IsBaseline { get; set; }
    public bool IsOverhead => Name == Utils.OverheadBenchmarkName;
    public Benchmark? Overhead { get; set; }
    private ImmutableArray<double> GetSamples()
    {
        var samples = _samplesRaw.Where(double.IsFinite);
        if (!IsOverhead && Overhead is not null && !double.IsNaN(Overhead.Stats.Mean))
        {
            var overheadValue = Overhead.Stats.Mean;
            samples = samples.Select(s => s - overheadValue);
        }
        return [.. samples];
    }

    public async Task GetSampleAsync()
    {
        var sampleNs = await server.GetSample(name, Iterations);
        var sample = (double)sampleNs / Iterations;
        Iterations = (int)(Utils2.TargetNs / sample);
        Iterations = Math.Max(Iterations, Utils.MinIterations);
        _samplesRaw.Add(sample);
        Stats = Utils2.Analyze(GetSamples());
    }

    public void Reset()
    {
        _samplesRaw.Clear();
        Stats = Stats.NaN;
    }
}
