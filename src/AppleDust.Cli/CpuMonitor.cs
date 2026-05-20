using System.Diagnostics;

namespace AppleDust.Cli;

internal sealed class CpuMonitor : IDisposable
{
    private readonly Queue<double> _cpuSamples = new(10);
    private readonly PerformanceCounter? _cpuCounter;
    private readonly CancellationTokenSource _cts;

    public CpuMonitor(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _ = Task.Run(async () =>
        {
            Debug.Assert(OperatingSystem.IsWindows());
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_cpuSamples.Count == _cpuSamples.Capacity)
                {
                    _ = _cpuSamples.Dequeue();
                }
                _cpuSamples.Enqueue(_cpuCounter.NextValue() / 100);
                CpuUsage = _cpuSamples.Average();
                CpuQuality = GetQuality(CpuUsage);
                await Task.Delay(100, cancellationToken);
            }
        }, cancellationToken);
    }

    public double CpuUsage { get; private set; } = double.NaN;

    public int CpuQuality { get; private set; }

    private static int GetQuality(double cpuUsage)
    {
        if (double.IsNaN(cpuUsage))
        {
            Debug.Assert(!OperatingSystem.IsWindows());
            return 0;
        }
        // 2 thread of work.
        var target = 2d / Environment.ProcessorCount;
        target = Math.Max(target, 0.1); // at least 10% CPU usage is expected.
        if (cpuUsage < target)
        {
            return 2; // great
        }
        if (cpuUsage < target * 1.5)
        {
            return 1; // good
        }
        if (cpuUsage < target * 2)
        {
            return 0; // meh
        }
        if (cpuUsage < target * 2.5)
        {
            return -1; // bad
        }
        return -2; // terrible
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cpuCounter?.Dispose();
    }
}
