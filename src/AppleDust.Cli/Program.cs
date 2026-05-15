using System.Diagnostics;
using AppleDust.Cli;
using AppleDust.Shared;
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
    using var hosts = await HostCollection.CreateAsync(paths, cts.Token);

    await Utils.Delay(cts.Token);

    var sw = Stopwatch.StartNew();
    await hosts.WarmUp().WithStatus("Warmup...");
    sw.Stop();
    AnsiConsole.WriteLine($"Warmup completed in {sw.Elapsed.TotalSeconds:F1} s");

    await Utils.Delay(cts.Token);

    var status = new StatusDisplay(hosts.Benchmarks);
    status.Refresh();
    var dash = new Dashboard(status);

    async Task MainLoop()
    {
    start:
        for (int i = 0; i < maxRounds; i++)
        {
            // Add benchmarks that have discarded samples a second time.
            List<Benchmark> benches = [.. hosts.Benchmarks, .. hosts.Benchmarks.Where(b => b.SampleCount < i)];

            foreach (var bench in benches.Shuffle()) // shuffle benchmarks to avoid bias.
            {
                while (dash.CpuQuality < 0)
                {
                    status.SetBorderColor(Color.Red);
                    // high CPU usage, wait for it to cool down.
                    await Task.Delay(1000, cts.Token);
                }
                status.SetBorderColor(Color.Default);

                await bench.GetSampleAsync();
                status.Refresh();

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Delete)
                    {
                        foreach (var b in hosts.Benchmarks)
                        {
                            b.Reset();
                        }
                        status.Refresh();
                        goto start;
                    }
                }
            }
            if (i % restartCount == restartCount - 1)
            {
                await hosts.RestartAsync(true);
            }
        }
    }

    var main = MainLoop();

    await AnsiConsole.Live(dash).StartAsync(async ctx =>
    {
        while (!main.IsCompleted)
        {
            dash.Update();
            ctx.Refresh();
            await Task.Delay(100, cts.Token);
        }
    });

    await main;
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
