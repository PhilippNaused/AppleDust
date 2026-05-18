using AppleDust.Cli;
using Spectre.Console;

const int maxRounds = 200;
const int restartCount = 5;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var paths = args.Select(Path.GetFullPath).ToList();
    using var collection = await BenchmarkCollection.CreateAsync(paths, cts.Token);
    var benchmarks = collection.Benchmarks;

    var status = new ResultTable(benchmarks);
    var dash = new Dashboard(status, collection);

    async Task MainLoop()
    {
        await collection.WarmUp();
        await collection.CoolDown(cts.Token);
    start:
        for (int i = 0; i < maxRounds; i++)
        {
            // Add benchmarks that have discarded samples a second time.
            List<Benchmark> benches = [.. benchmarks, .. benchmarks.Where(b => b.SampleCount < i)];

            foreach (var bench in benches.Shuffle()) // shuffle benchmarks to avoid bias.
            {
                while (dash.CpuQuality < 0)
                {
                    status.SetBorderColor(Color.Red);
                    // high CPU usage, wait for it to cool down.
                    await collection.CoolDown(cts.Token);
                }
                status.SetBorderColor(Color.Default);

                await bench.GetSampleAsync();

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Delete)
                    {
                        foreach (var b in benchmarks)
                        {
                            b.Reset();
                        }
                        goto start;
                    }
                }
            }
            if (i % restartCount == restartCount - 1)
            {
                await collection.RestartAsync(true);
                await collection.CoolDown(cts.Token);
            }
        }
    }

    var mainLoop = MainLoop();

    await AnsiConsole.Live(dash).StartAsync(async ctx =>
    {
        while (!mainLoop.IsCompleted)
        {
            status.Refresh();
            dash.Update();
            ctx.Refresh();
            await Task.Delay(100, cts.Token);
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
    AnsiConsole.WriteLine(e.RemoteStackTrace);
}
#pragma warning disable CA1031 // Do not catch general exception types
catch (Exception e)
{
    AnsiConsole.WriteException(e);
}
#pragma warning restore CA1031 // Do not catch general exception types

static void Cancelled() => AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
