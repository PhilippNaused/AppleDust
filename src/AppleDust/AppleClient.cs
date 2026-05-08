using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using AppleDust.Shared;

namespace AppleDust;

internal sealed class AppleClient : IAppleClient
{
    private readonly IReadOnlyList<Benchmark> _benchmarks;
    private readonly AnonymousPipeClientStream _downPipe;
    private readonly AnonymousPipeClientStream _upPipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
#if DEBUG
    private readonly StreamWriter _logFile;
#endif

    private AppleClient(string downPipeHandle, string upPipeHandle, IReadOnlyList<Benchmark> benchmarks)
    {
        _benchmarks = benchmarks;
        _downPipe = new AnonymousPipeClientStream(PipeDirection.In, downPipeHandle);
        _upPipe = new AnonymousPipeClientStream(PipeDirection.Out, upPipeHandle);
        var encoding = new UTF8Encoding(false);
        _reader = new StreamReader(_downPipe, encoding);
        _writer = new StreamWriter(_upPipe, encoding) { AutoFlush = true };
#if DEBUG
        _logFile = new StreamWriter(new FileStream("client.log", FileMode.Create, FileAccess.Write, FileShare.Read), encoding) { AutoFlush = true };
#endif
    }

#pragma warning disable CA1822 // Mark members as static
    private Task LogAsync(string message)
    {
#if DEBUG
        return _logFile.WriteLineAsync(message);
#else
        return Task.CompletedTask;
#endif
    }
#pragma warning restore CA1822 // Mark members as static

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
        _reader.Dispose();
        _writer.Dispose();
        _downPipe.Dispose();
        _upPipe.Dispose();
#if DEBUG
        _logFile.Dispose();
#endif
    }

    internal static async Task RunAsync(IReadOnlyList<Benchmark> benchmarks, string[] args)
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        using var client = new AppleClient(args[0], args[1], benchmarks);

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            await Utils.Delay();
            await client.RunInner();
        }
        catch (Exception e)
        {
            try
            {
                var message = Utils.Serialize(e.Message);
                var stackTrace = Utils.Serialize(e.StackTrace ?? "");
                await client._writer.WriteLineAsync($"Error:{message}|{stackTrace}");
            }
            catch
            {
                // ignore
            }
            Console.WriteLine(e);
            //await Console.Error.WriteLineAsync(e);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private async Task RunInner()
    {
        while (true)
        {
            var line = await _reader.ReadLineAsync();
            if (line is null)
            {
                Console.WriteLine("End of stream.");
                return;
            }
            await LogAsync("< " + line);

            var parts = line.Split([Utils.CommandSeparator], StringSplitOptions.None);
            var command = parts[0];
            IReadOnlyList<string> parameters = new ArraySegment<string>(parts, 1, parts.Length - 1);
            var method = _methods[command];
            var response = await InvokeAsync(method, parameters);
            await LogAsync("> " + response);
            await _writer.WriteLineAsync(response);
        }
    }

    private static readonly Dictionary<string, MethodInfo> _methods = (typeof(IAppleClient)).GetMethods().ToDictionary(m => m.Name);

    private async Task<string> InvokeAsync(MethodInfo info, IReadOnlyList<string> values)
    {
        var parameterInfos = info.GetParameters();
        var parameters = new object?[parameterInfos.Length];
        for (int i = 0; i < parameterInfos.Length; i++)
        {
            parameters[i] = Utils.Deserialize(values[i], parameterInfos[i].ParameterType);
        }
        Task task = (Task)info.Invoke(this, parameters)!;
        await task;
        var value = GetResult(task);
        return Utils.Serialize(value);
    }

    private static object GetResult(Task task)
    {
        return task switch
        {
            Task<int> t => t.Result,
            Task<long> t => t.Result,
            Task<double> t => t.Result,
            Task<string> t => t.Result,
            Task<string[]> t => t.Result,
            Task<int[]> t => t.Result,
            _ => throw new NotSupportedException($"Unsupported task type: {task.GetType()}")
        };
    }

    private Benchmark Get(string name) => _benchmarks.Single(b => b.Name == name);

    [MethodImpl(Utils.AggressiveOptimization)]
    public Task<int[]> WarmUp(int targetMs)
    {
#pragma warning disable CA1849 // Call async methods when in an async method
        const int warmUpCount = 5;
        var parallel = Environment.ProcessorCount / 4;
        //parallel = 1;
        parallel = Math.Max(1, parallel);
        var iterations = _benchmarks.Select(b => b.Pilot(targetMs)).ToArray();
        Thread.Sleep(Utils.JitDelayMs);
        _ = Parallel.For(0, _benchmarks.Count, new ParallelOptions { MaxDegreeOfParallelism = parallel }, i =>
        {
            for (int j = 0; j < warmUpCount; j++)
            {
                _ = _benchmarks[i].Measure(iterations[i]);
            }
        });
        Thread.Sleep(Utils.JitDelayMs);
        for (int i = 0; i < _benchmarks.Count; i++)
        {
            iterations[i] = _benchmarks[i].Pilot(targetMs, iterations[i]); // refine the pilot result after warming up
        }
        return Task.FromResult(iterations);
#pragma warning restore CA1849 // Call async methods when in an async method
    }

    public Task<long> GetSample(string name, int iterations) => Task.FromResult(Get(name).Measure(iterations));

    public Task<string[]> GetNames() => Task.FromResult(_benchmarks.Select(b => b.Name).ToArray());
}
