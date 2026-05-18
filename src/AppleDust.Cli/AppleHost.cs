using System.Diagnostics.CodeAnalysis;

namespace AppleDust.Cli;

internal sealed class AppleHost : IDisposable
{
    private readonly string _path;
    private readonly CancellationToken _cancellationToken;
    private RpcProcess _process;
    private RpcCaller _caller;
    public bool Restarting { get; private set; }

    private AppleHost(string path, CancellationToken cancellationToken)
    {
        _path = path;
        _cancellationToken = cancellationToken;
        Restart();
    }

    [MemberNotNull(nameof(_process), nameof(_caller))]
    public void Restart()
    {
        Restarting = true;
        _caller?.Dispose();
        _process?.Dispose();
        _process = new RpcProcess(_path, _cancellationToken);
        _caller = new RpcCaller(_process.Pipe, _cancellationToken);
        Restarting = false;
    }

    public static AppleHost Create(HostParameters config, CancellationToken cancellationToken)
    {
        return new AppleHost(config.Path, cancellationToken);
    }

    public static async Task<List<Benchmark>> GetBenchmarksAsync(HostParameters config, CancellationToken cancellationToken)
    {
        using var host = Create(config, cancellationToken);
        var names = await host._caller.GetNames();
        var list = new List<Benchmark>(names.Select(name => new Benchmark(config, name, cancellationToken)));
        var overheadBench = list.Single(b => b.IsOverhead);
        _ = list.Remove(overheadBench);
        list.Insert(0, overheadBench);
        foreach (var benchmark in list)
        {
            benchmark.Overhead = overheadBench;
        }
        return list;
    }

    public void Dispose()
    {
        _caller.Dispose();
        _process.Dispose();
    }

    public Task<(long nanos, long bytes)> GetSample(string name, int i) => _caller.GetSample(name, i);

    public Task<int> WarmUp(string name, int targetMs) => _caller.WarmUp(name, targetMs);
}
