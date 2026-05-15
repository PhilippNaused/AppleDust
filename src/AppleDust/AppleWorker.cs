using System.Diagnostics;
using System.Runtime.CompilerServices;
using AppleDust.Shared;

namespace AppleDust;

internal sealed class AppleWorker : IAppleRpc
{
    private readonly IReadOnlyList<Benchmark> _benchmarks;
    private readonly DuplexClient _pipe;
    private readonly RpcClient<IAppleRpc> _rpcClient;

    private AppleWorker(string downPipeHandle, string upPipeHandle, IReadOnlyList<Benchmark> benchmarks)
    {
        _benchmarks = benchmarks;
        _pipe = DuplexClient.FromHandles(downPipeHandle, upPipeHandle);
        _rpcClient = new RpcClient<IAppleRpc>(this, _pipe);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var benchmark in _benchmarks)
        {
            if (benchmark is IDisposable d)
            {
                d.Dispose();
            }
        }
        _pipe.Dispose();
    }

    internal static async Task RunAsync(IReadOnlyList<Benchmark> benchmarks, string[] args)
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        using var client = new AppleWorker(args[0], args[1], benchmarks);

        await client._rpcClient.RunAsync();
    }

    private Benchmark Get(string name) => _benchmarks.Single(b => b.Name == name);

    [MethodImpl(Utils.AggressiveOptimization)]
    public Task<(string, int)[]> WarmUp(int targetMs)
    {
#pragma warning disable CA1849 // Call async methods when in an async method
        // const int warmUpCount = 5;
        // var parallel = Environment.ProcessorCount / 4;
        //parallel = 1;
        // parallel = Math.Max(1, parallel);
        var iterations = _benchmarks.Select(b => b.Pilot(targetMs)).ToArray();
        // Thread.Sleep(Utils.JitDelayMs);
        // _ = Parallel.For(0, _benchmarks.Count, new ParallelOptions { MaxDegreeOfParallelism = parallel }, i =>
        // {
        //     for (int j = 0; j < warmUpCount; j++)
        //     {
        //         _ = _benchmarks[i].Measure(iterations[i]);
        //     }
        // });
        Thread.Sleep(Utils.JitDelayMs);
        for (int i = 0; i < _benchmarks.Count; i++)
        {
            iterations[i] = _benchmarks[i].Pilot(targetMs, iterations[i]); // refine the pilot result after warming up
        }
        var result = _benchmarks.Select((b, i) => (b.Name, iterations[i])).ToArray();
        return Task.FromResult(result);
#pragma warning restore CA1849 // Call async methods when in an async method
    }

    public Task<(long Nanos, long Bytes)> GetSample(string name, int iterations) => Task.FromResult(Get(name).Measure(iterations));

    public Task<string[]> GetNames() => Task.FromResult(_benchmarks.Select(b => b.Name).ToArray());
}
