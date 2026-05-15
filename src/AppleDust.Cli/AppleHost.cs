using System.Diagnostics.CodeAnalysis;

namespace AppleDust.Cli;

internal sealed class AppleHost : IDisposable
{
    private readonly string _path;
    private readonly List<Benchmark> _benchmarks = [];
    private readonly CancellationToken _cancellationToken;
    private RpcProcess _process;
    private RpcCaller _caller;
    public IReadOnlyList<Benchmark> Benchmarks => _benchmarks;

    private AppleHost(string path, CancellationToken cancellationToken)
    {
        _path = path;
        _cancellationToken = cancellationToken;
        RestartAsync();
    }

    [MemberNotNull(nameof(_process), nameof(_caller))]
    public void RestartAsync()
    {
        _caller?.Dispose();
        _process?.Dispose();
        _process = new RpcProcess(_path, _cancellationToken);
        _caller = new RpcCaller(_process.Pipe, _cancellationToken);
    }

    public static async Task<AppleHost> CreateAsync(string path, CancellationToken cancellationToken)
    {
        var host = new AppleHost(path, cancellationToken);
        var names = await host._caller.GetNames();
        var list = host._benchmarks;
        foreach (var name in names)
        {
            var bench = new Benchmark(host, name);
            list.Add(bench);
        }
        var overheadBench = list.Single(b => b.IsOverhead);
        _ = list.Remove(overheadBench);
        list.Insert(0, overheadBench);
        foreach (var benchmark in list)
        {
            benchmark.Overhead = overheadBench;
        }
        return host;
    }

    public void Dispose()
    {
        _caller.Dispose();
        _process.Dispose();
    }

    public Benchmark GetBenchmark(string name) => Benchmarks.Single(b => b.Name == name);

    public Task<(long nanos, long bytes)> GetSample(string name, int i) => _caller.GetSample(name, i);

    public Task<(string Name, int Iterations)[]> WarmUp(int targetMs) => _caller.WarmUp(targetMs);
}
