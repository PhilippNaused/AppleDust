using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AppleDust.Cli;

internal sealed class AppleHost : IDisposable
{
    private readonly HostParameters _parameters;
    private readonly CancellationToken _cancellationToken;
    private RpcProcess? _process;
    private RpcCaller? _caller;
    public bool Restarting { get; private set; }

    private AppleHost(HostParameters parameters, CancellationToken cancellationToken)
    {
        _parameters = parameters;
        _cancellationToken = cancellationToken;
        Restart();
    }

    [MemberNotNull(nameof(_process), nameof(_caller))]
    public void Restart()
    {
        Restarting = true;
        _caller?.Dispose();
        _process?.Dispose();
        _process = new RpcProcess(_parameters, _cancellationToken);
        _caller = new RpcCaller(_process.Pipe, _cancellationToken);
        Restarting = false;
    }

    public static AppleHost Create(HostParameters parameters, CancellationToken cancellationToken)
    {
        return new AppleHost(parameters, cancellationToken);
    }

    public static async Task<List<string>> GetBenchmarksAsync(HostParameters parameters, CancellationToken cancellationToken)
    {
        using var host = new AppleHost(parameters, cancellationToken);
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
