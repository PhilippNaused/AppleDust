using AppleDust.Cli;
using Spectre.Console;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var config = new Config { ColdStart = false };

try
{
    var paths = args.Select(Path.GetFullPath).ToList();
    var benchmarkNames = await AppleHost.GetBenchmarksAsync(new HostParameters(paths.First()), cts.Token).WithStatus("Initializing...");
    using var collection = BenchmarkCollection.Create(paths, benchmarkNames, cts.Token);

    using var cpuMonitor = new CpuMonitor(cts.Token);
    CpuMonitor.ExpectedThreads = config.ConcurrentSamples + 1;
    var status = new ResultTable(collection);
    var dash = new Dashboard(status, collection, cpuMonitor);

    using var resetEvent = new AutoResetEvent(false);

    async Task MainLoop()
    {
        if (!config.ColdStart)
        {
            await collection.WarmUp();
            await collection.CoolDown(cts.Token);
        }
    start:
        for (int i = 0; !cts.IsCancellationRequested; i++)
        {
            // Add benchmarks that have discarded samples a second time.
            List<Benchmark> benches = [.. collection, .. collection.Where(b => b.SampleCount < i)];
            foreach (var bench in benches.Shuffle().Chunk(config.ConcurrentSamples)) // shuffle benchmarks to avoid bias.
            {
                while (cpuMonitor.CpuQuality < 0)
                {
                    status.SetBorderStyle(Styles.Red);
                    // high CPU usage, wait for it to cool down.
                    await collection.CoolDown(cts.Token);
                }
                status.SetBorderStyle(Style.Plain);
                collection.State = BenchmarkCollection.StateEnum.Sampling;
                await Task.WhenAll(bench.Select(b => b.GetSampleAsync(config.ColdStart)));

                if (resetEvent.WaitOne(0))
                {
                    goto start;
                }
            }
            if (collection.Min(b => b.SampleCount) >= config.RestartCount)
                collection.CheckOverhead();
            if (!config.ColdStart && i % config.RestartCount == config.RestartCount - 1)
            {
                await collection.RestartAsync(true);
                await collection.CoolDown(cts.Token);
            }
        }
    }

    var mainLoop = MainLoop();

    await AnsiConsole.Live(dash).StartAsync(async ctx =>
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (!mainLoop.IsCompleted)
        {
            dash.Update();
            ctx.Refresh();
            if (!await timer.WaitForNextTickAsync(cts.Token))
            {
                break;
            }

            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Delete)
                {
                    await collection.ResetAsync();
                    _ = resetEvent.Set();
                }
            }
        }
    });

    await mainLoop;
}
catch (OperationCanceledException)
{
    Cancelled();
}
catch (EndOfStreamException) when (cts.IsCancellationRequested)
{
    Cancelled();
}
catch (RpcException e)
{
    AnsiConsole.WriteLine(e.Message);
    AnsiConsole.WriteLine(e.RemoteErrorMessage);
}
#pragma warning disable CA1031 // Do not catch general exception types
catch (Exception e)
{
    AnsiConsole.WriteException(e);
}
#pragma warning restore CA1031 // Do not catch general exception types

static void Cancelled() => AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
