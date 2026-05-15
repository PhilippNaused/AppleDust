using System.Diagnostics;
using System.Runtime.CompilerServices;
using AppleDust.Shared;

namespace AppleDust;

internal sealed class Benchmark<T>(Func<T> func, string name) : Benchmark(name)
{
    private readonly Func<T> target = func;

    [MethodImpl(Utils.AggressiveOptimization | MethodImplOptions.NoInlining)]
    protected override void Run(int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            Consume(target());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Consume(T _)
    {
        // This method is intentionally left empty to prevent compiler optimizations.
    }
}

internal abstract class Benchmark(string name)
{
    public string Name { get; } = name;

    [MethodImpl(Utils.AggressiveOptimization | MethodImplOptions.NoInlining)]
    protected abstract void Run(int iterations);

    [MethodImpl(Utils.AggressiveOptimization)]
    internal (long Nanos, long Bytes) Measure(int iterations)
    {
        GcHelper.ForceGcCollect();
        var sw = new Stopwatch();
        var before = GcHelper.GetAllocatedBytes();
        sw.Start();
        Run(iterations);
        sw.Stop();
        var after = GcHelper.GetAllocatedBytes();
        var bytes = after >= 0 ? after - before : -1;
        return (sw.ElapsedNanoseconds, bytes);
    }

    [MethodImpl(Utils.AggressiveOptimization)]
    public int Pilot(int targetMs, int iterations = Utils.MinIterations)
    {
        long targetNs = targetMs * 1_000_000L;

        long timeNs;
        while ((timeNs = Measure(iterations).Nanos) < targetNs * 1.1)
        {
            iterations *= 2;
        }

        iterations = (int)(iterations * (targetNs / (double)timeNs));
        iterations = Math.Max(iterations, Utils.MinIterations);
        return iterations;
    }
}
