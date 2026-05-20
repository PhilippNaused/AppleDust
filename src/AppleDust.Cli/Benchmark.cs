using System.Collections.Immutable;
using AppleDust.Shared;
using DotNext.Threading;
using Perfolizer.Mathematics.OutlierDetection;

namespace AppleDust.Cli;

internal sealed class Benchmark(HostParameters hostConfig, string name, CancellationToken cancellationToken) : IDisposable
{
    private readonly List<double> _samplesRaw = new(50);
    private readonly List<double> _samplesGcRaw = new(50);
    private readonly AsyncLock _lock = AsyncLock.Exclusive();
    private ValueTask<AsyncLock.Scope> LockAsync() => _lock.AcquireAsync(cancellationToken);

    public string Name => name;
    public int Iterations { get; set; } = 1;
    public Stats Stats { get; private set; } = Stats.NaN;
    public Stats GcStats { get; private set; } = Stats.NaN;
    public bool IsBaseline => Baseline is null || Baseline == this;
    public bool IsOverhead => Name == Utils.OverheadBenchmarkName;
    public int Outliers { get; private set; }
    public Benchmark? Overhead { get; set; }
    public Benchmark? Baseline { get; set; }
    public AppleHost Host { get; } = AppleHost.Create(hostConfig, cancellationToken);

    public int SampleCount => Stats.Samples.Length;

    private int _state = 0; // TODO: use enum for state.

    public async Task WarmUp()
    {
        _state = 1;
        Iterations = await Host.WarmUp(Name, Utils2.TargetMs);
        _state = 0;
    }

    public string GetStatus()
    {
        if (Host.Restarting)
        {
            return "Restarting";
        }
        return _state switch
        {
            0 => "Idle",
            1 => $"Warming up",
            2 => "Sampling",
            _ => "Unknown"
        };
    }

    private ImmutableArray<double> Sanitize(List<double> samples, double? overhead)
    {
        var sanitized = samples.Where(double.IsFinite);
        if (overhead.HasValue && !double.IsNaN(overhead.Value) && !IsOverhead)
        {
            sanitized = sanitized.Select(s => s - overhead.Value);
        }
        return [.. sanitized];
    }

    public async Task GetSampleAsync(bool coldStart = false)
    {
        using var _ = await LockAsync();
        if (coldStart)
        {
            Iterations = 1;
            Host.Restart();
        }
        _state = 2;
        (long nanos, long bytes) = await Host.GetSample(name, Iterations);
        if (coldStart)
        {
            Host.Shutdown();
        }
        var timeSample = (double)nanos / Iterations;
        var memorySample = (double)bytes / Iterations;
        if (bytes < 0)
        {
            memorySample = double.NaN;
        }
        if (!coldStart)
        {
            Iterations = (int)(Utils2.TargetNs / timeSample);
            Iterations = Math.Max(Iterations, Utils.MinIterations);
        }
        _samplesRaw.Add(timeSample);
        _samplesGcRaw.Add(memorySample);
        RemoveOutliers();
        UpdateStats();
        _state = 0;
    }

    private void UpdateStats()
    {
        Stats = Utils2.Analyze(Sanitize(_samplesRaw, Overhead?.Stats.Center));
        GcStats = Utils2.Analyze(Sanitize(_samplesGcRaw, Overhead?.GcStats.Center));
    }

    private void RemoveOutliers()
    {
        Outliers += _samplesRaw.RemoveAll(s => !double.IsNormal(s));
        if (_samplesRaw.Count < 20)
        {
            return; // Not enough samples to detect outliers
        }
        // cspell:ignore Tukey
        // double MAD is better than Tukey since the outliers are likely skewed to the right.
        var detector = DoubleMadOutlierDetector.Create(_samplesRaw, k: 3.5);
        for (int i = 0; i < _samplesRaw.Count; i++)
        {
            int j = _samplesRaw.Count - 1 - i; // index from the end since we are removing items.
            // outliers are usually from CPU spikes.
            if (detector.IsOutlier(_samplesRaw[j]))
            {
                _samplesRaw.RemoveAt(j);
                _samplesGcRaw.RemoveAt(j);
                Outliers++;
            }
        }
    }

    public async Task ResetAsync()
    {
        using var _ = await LockAsync();
        _samplesRaw.Clear();
        _samplesGcRaw.Clear();
        Outliers = 0;
        Stats = Stats.NaN;
        GcStats = Stats.NaN;
    }

    public override string ToString()
    {
        return $"Benchmark: {Name}, Iterations: {Iterations}, Stats: {Stats}, GcStats: {GcStats}";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Host.Dispose();
    }
}
