using System.Collections;
using System.Diagnostics;

namespace AppleDust.Cli;

internal sealed class BenchmarkCollection : IReadOnlyCollection<Benchmark>, IDisposable
{
    private readonly List<Benchmark> _benchmarks;

    public IReadOnlyList<Benchmark> Benchmarks => _benchmarks;
    private readonly ParallelOptions _parallelOptions;
    private readonly Stopwatch _stopwatch = new();

    public double? WarmupProgress => LastWarmupTime.HasValue ? Math.Min(1.0, _stopwatch.Elapsed.TotalMilliseconds / LastWarmupTime.Value.TotalMilliseconds) : null;
    public TimeSpan? LastWarmupTime { get; set; }

    /// <inheritdoc />
    public int Count => _benchmarks.Count;

    private BenchmarkCollection(List<Benchmark> benchmarks)
    {
        _benchmarks = benchmarks;
        var maxParallel = Environment.ProcessorCount / 4;
        maxParallel = Math.Max(1, maxParallel);
        //var maxParallel = 1;
        _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };
    }

    public static async Task<BenchmarkCollection> CreateAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        int mode = paths.Count > 1 ? 1 : 0;
        var hosts = new List<List<Benchmark>>(paths.Count);
        var benchmarkList = new List<Benchmark>(paths.Count * 3); // every host should have at least 3 benchmarks (overhead, baseline, actual...)
        foreach (var path in paths)
        {
            var benchmarks = await AppleHost.GetBenchmarksAsync(new HostParameters(path), cancellationToken);
            hosts.Add(benchmarks);

            if (mode == 0)
            {
                // the first benchmark in each host is the baseline for that host
                var baseline = benchmarks.First(b => !b.IsOverhead);
                foreach (var benchmark in benchmarks)
                {
                    if (benchmark.IsOverhead)
                        continue;
                    benchmark.Baseline = baseline;
                }
            }
            benchmarkList.AddRange(benchmarks);
        }
        if (mode == 1)
        {
            // The first host is the baseline for the other hosts. Match benchmarks by name and set the baseline accordingly.
            var baseHost = hosts.First();
            foreach (var host in hosts.Skip(1))
            {
                foreach (var benchmark in host)
                {
                    var baseline = baseHost.Single(b => b.Name == benchmark.Name);
                    benchmark.Baseline = baseline;
                }
            }
            Debug.Assert(baseHost.All(b => b.Baseline == null));
        }
        return new BenchmarkCollection(benchmarkList);
    }

    public async Task WarmUp()
    {
        _stopwatch.Restart();
        await Parallel.ForEachAsync(_benchmarks.Shuffle(), _parallelOptions, async (bench, _) => await bench.WarmUp());
        _stopwatch.Stop();
        LastWarmupTime = _stopwatch.Elapsed;
    }

#pragma warning disable CA1822 // Mark members as static
    public Task CoolDown(CancellationToken cancellationToken)
    {
        return Task.Delay(2_000, cancellationToken);
    }
#pragma warning restore CA1822 // Mark members as static

    public async Task RestartAsync(bool warmup)
    {
        foreach (var benchmark in _benchmarks.Shuffle())
        {
            benchmark.Host.Restart();
        }
        if (!warmup)
        {
            return;
        }
        await WarmUp();
    }

    public void Dispose()
    {
        foreach (var item in _benchmarks)
        {
            item.Dispose();
        }
    }

    /// <inheritdoc />
    public IEnumerator<Benchmark> GetEnumerator() => _benchmarks.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _benchmarks.GetEnumerator();

}
