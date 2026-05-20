using System.Reflection;
using AppleDust.Shared;

namespace AppleDust;

internal sealed class RpcClient<T>(T instance, IDuplexPipe pipe)
{
    private static readonly Dictionary<string, MethodInfo> _methods = typeof(T).GetMethods().ToDictionary(m => m.Name);

    private async Task<string> InvokeAsync(MethodInfo info, IReadOnlyList<string> values)
    {
        var parameterInfos = info.GetParameters();
        var parameters = new object?[parameterInfos.Length];
        for (int i = 0; i < parameterInfos.Length; i++)
        {
            parameters[i] = Utils.Deserialize(values[i], parameterInfos[i].ParameterType);
        }
        Task task = (Task)info.Invoke(instance, parameters)!;
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
            Task<(long, long)> t => t.Result,
            Task<double> t => t.Result,
            Task<string> t => t.Result,
            Task<string[]> t => t.Result,
            Task<int[]> t => t.Result,
            Task<(string, int)[]> t => t.Result,
            _ => throw new NotSupportedException($"Unsupported task type: {task.GetType()}")
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            GcHelper.ForceGcCollect();
            await RunInnerAsync(cancellationToken);
        }
        catch (Exception e)
        {
            try
            {
                var message = Utils.Serialize(e.Message);
                var stackTrace = Utils.Serialize(e.StackTrace ?? "");
                await pipe.WriteLineAsync($"{Utils.ErrorPrefix}{message}|{stackTrace}", cancellationToken);
            }
            catch
            {
                // ignore
            }
            Console.WriteLine(e);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private async Task RunInnerAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var line = await pipe.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw new EndOfStreamException();
            }

            var parts = line.Split([Utils.CommandSeparator], StringSplitOptions.None);
            var command = parts[0];
            IReadOnlyList<string> parameters = new ArraySegment<string>(parts, 1, parts.Length - 1);
            var method = _methods[command];
            var response = await InvokeAsync(method, parameters);
            await pipe.WriteLineAsync(response, cancellationToken);
            GcHelper.ForceGcCollect();
        }
    }
}
