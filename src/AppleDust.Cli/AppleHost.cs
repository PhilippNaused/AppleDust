using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AppleDust.Cli;

internal sealed class AppleHost : IDisposable
{
    private readonly string _path;
    private readonly CancellationToken _cancellationToken;
    private RpcProcess? _process;
    private RpcCaller? _caller;
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

    public static async Task<List<string>> GetBenchmarksAsync(string path, CancellationToken cancellationToken)
    {
        using var host = new AppleHost(path, cancellationToken);
        Debug.Assert(host._caller is not null);
        var names = await host._caller.GetNames();
        return names.ToList();
    }

    public void Shutdown()
    {
        _caller?.Dispose();
        _caller = null;
        _process?.Dispose();
        _process = null;
    }

    public void Dispose()
    {
        Shutdown();
    }

    public Task<(long nanos, long bytes)> GetSample(string name, int i) => _caller!.GetSample(name, i);

    public Task<int> WarmUp(string name, int targetMs) => _caller!.WarmUp(name, targetMs);
}
