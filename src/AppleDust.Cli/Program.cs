using System.Diagnostics;
using AppleDust.Cli;
using AppleDust.Shared;
using Spectre.Console;

var path = args[0];

const int maxRounds = 200;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    using var server = new AppleServer(path, cts.Token);

    await Utils.Delay(cts.Token);

    var names = await server.GetNames();

    var sw = Stopwatch.StartNew();
    var iterations = await server.WarmUp(Utils2.TargetMs).WithStatus("Warmup...");
    sw.Stop();
    AnsiConsole.WriteLine($"Warmup completed in {sw.Elapsed.TotalSeconds:F1} s");

    var benchmarks = names.Select((name, i) => new Benchmark(server, name, iterations[i])).ToList();
    var overheadBench = benchmarks.Single(b => b.IsOverhead);
    benchmarks.First(b => !b.IsOverhead).IsBaseline = true;
    foreach (var benchmark in benchmarks)
    {
        benchmark.Overhead = overheadBench;
    }

    await Task.Delay(5_000, cts.Token).WithStatus("5s delay");

    var status = new StatusDisplay(benchmarks);

    await AnsiConsole.Live(status).StartAsync(async ctx =>
    {
        for (int i = 0; i < maxRounds; i++)
        {
            foreach (var bench in benchmarks)
            {
                await bench.GetSampleAsync();
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Delete)
                    {
                        // reset
                        i = 0;
                        foreach (var b in benchmarks)
                        {
                            b.Reset();
                        }
                    }
                }
                status.Refresh();
                ctx.Refresh();
            }
        }
    });
}
catch (OperationCanceledException)
{
    AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
}
catch (AppleServer.ClientException e)
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
