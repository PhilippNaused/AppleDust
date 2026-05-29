using System.Runtime.CompilerServices;
using AppleDust.Shared;

namespace AppleDust;

public sealed class BenchmarkBuilder
{
    private readonly List<Benchmark> _benchmarks = [];

    private void AddBenchmark(Benchmark benchmark)
    {
        if (_benchmarks.Any(b => b.Name == benchmark.Name))
        {
            throw new ArgumentException($"A benchmark with the name {benchmark.Name} already exists");
        }
        _benchmarks.Add(benchmark);
    }

    public BenchmarkBuilder Add<T>(Func<T> func, [CallerArgumentExpression(nameof(func))] string? name = null)
    {
        name ??= func.Method.Name;
        AddBenchmark(new Benchmark<T>(func, name));
        return this;
    }

    public BenchmarkBuilder Add<T, TP>(Func<TP, T> func, TP[] parameters, [CallerArgumentExpression(nameof(func))] string? name = null)
    {
        name ??= func.Method.Name;

        foreach (TP parameter in parameters)
        {
            T f() => func(parameter);
            var b = new Benchmark<T>(f, $"{name} ({parameter})");
            AddBenchmark(b);
        }

        return this;
    }

    public BenchmarkBuilder UseOverhead<T>(Func<T> func)
    {
        return Add(func, Utils.OverheadBenchmarkName);
    }

    public Task RunAsync(string[] args)
    {
        _ = ThreadPool.SetMaxThreads(1, 1); // ensure benchmarks run sequentially to avoid interference. This also makes the results more stable, especially for GC stats.
        if (_benchmarks.All(b => b.Name != Utils.OverheadBenchmarkName))
        {
            _ = UseOverhead(Nothing);
        }
        return AppleWorker.RunAsync(_benchmarks, args);
    }

    // For some reason, this method is faster when it's an instance method instead of static.
    private object? Nothing() => null;
}
