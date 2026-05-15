using System.Collections;
using System.Diagnostics;

namespace AppleDust.Cli;

internal sealed class HostCollection : IReadOnlyCollection<AppleHost>, IDisposable
{
    private readonly List<AppleHost> _hosts;
    private readonly List<Benchmark> _benchmarks;

    public IReadOnlyList<Benchmark> Benchmarks => _benchmarks;

    private HostCollection(List<AppleHost> hosts, List<Benchmark> benchmarks)
    {
        _hosts = hosts;
        _benchmarks = benchmarks;
    }

    public static async Task<HostCollection> CreateAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        int mode = paths.Count > 1 ? 1 : 0;
        var hosts = new List<AppleHost>(paths.Count);
        var benchmarkList = new List<Benchmark>(paths.Count * 3); // every host should have at least 3 benchmarks (overhead, baseline, actual...)
        foreach (var path in paths)
        {
            var host = await AppleHost.CreateAsync(path, cancellationToken);
            hosts.Add(host);

            if (mode == 0)
            {
                // the first benchmark in each host is the baseline for that host
                var baseline = host.Benchmarks.First(b => !b.IsOverhead);
                foreach (var benchmark in host.Benchmarks)
                {
                    if (benchmark.IsOverhead)
                        continue;
                    benchmark.Baseline = baseline;
                }
            }
            benchmarkList.AddRange(host.Benchmarks);
        }
        if (mode == 1)
        {
            // The first host is the baseline for the other hosts. Match benchmarks by name and set the baseline accordingly.
            var baseHost = hosts.First();
            foreach (var host in hosts.Skip(1))
            {
                foreach (var benchmark in host.Benchmarks)
                {
                    var baseline = baseHost.GetBenchmark(benchmark.Name);
                    benchmark.Baseline = baseline;
                }
            }
            Debug.Assert(baseHost.Benchmarks.All(b => b.Baseline == null));
        }
        return new HostCollection(hosts, benchmarkList);
    }

    public async Task WarmUp()
    {
        await Parallel.ForEachAsync(_hosts.Shuffle(), async (host, _) =>
        {
            var samples = await host.WarmUp(Utils2.TargetMs);
            foreach (var (name, iterations) in samples)
            {
                var bench = host.GetBenchmark(name);
                bench.Iterations = iterations;
            }
        });
        await Task.Delay(2_000);
    }

    public Task RestartAsync(bool warmup)
    {
        foreach (var host in _hosts.Shuffle())
        {
            host.RestartAsync();
        }
        if (warmup)
            return WarmUp();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var item in _hosts)
        {
            item.Dispose();
        }
    }

    /// <inheritdoc />
    public IEnumerator<AppleHost> GetEnumerator() => _hosts.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _hosts.GetEnumerator();

    /// <inheritdoc />
    public int Count => _hosts.Count;
}
