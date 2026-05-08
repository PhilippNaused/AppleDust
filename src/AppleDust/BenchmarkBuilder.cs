using System.Runtime.CompilerServices;
using AppleDust.Shared;

namespace AppleDust;

public sealed class BenchmarkBuilder
{
    private readonly List<Benchmark> _benchmarks = [];

    public BenchmarkBuilder Add<T>(Func<T> func, [CallerArgumentExpression(nameof(func))] string? name = null)
    {
        name ??= func.Method.Name;
        var b = new Benchmark<T>(func, name);
        _benchmarks.Add(b);
        return this;
    }

    public BenchmarkBuilder UseOverhead<T>(Func<T> func)
    {
        return Add(func, Utils.OverheadBenchmarkName);
    }

    public Task RunAsync(string[] args)
    {
        if (_benchmarks.All(b => b.Name != Utils.OverheadBenchmarkName))
        {
            _ = UseOverhead(Nothing);
        }
        return AppleClient.RunAsync(_benchmarks, args);
    }

    // For some reason, this method is faster when it's an instance method instead of static.
    private object? Nothing() => null;
}
