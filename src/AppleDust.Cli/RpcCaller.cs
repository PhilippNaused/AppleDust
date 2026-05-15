using System.Runtime.ExceptionServices;
using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class RpcCaller(IDuplexPipe pipe, CancellationToken cancellationToken) : IAppleRpc
{
    public void Dispose()
    {
        pipe.Dispose();
    }

    private async Task<TRet> InvokeAsync<TRet>(Delegate del, params object[] values) where TRet : notnull
    {
        var name = del.Method.Name;
        var command = string.Join(Utils.CommandSeparator, [name, .. values.Select(Utils.Serialize)]); // e.g. "Add|1|2"
        var response = await SendCommandAsync(command);
        if (response.StartsWith(Utils.ErrorPrefix))
        {
            var parts = response[Utils.ErrorPrefix.Length..].Split(Utils.CommandSeparator);
            var message = Utils.Deserialize<string>(parts[0]);
            var stackTrace = Utils.Deserialize<string>(parts[1]);
            throw ExceptionDispatchInfo.SetRemoteStackTrace(new RpcException(message, stackTrace), stackTrace);
        }
        return Utils.Deserialize<TRet>(response);
    }

    private async Task<string> SendCommandAsync(string command)
    {
        await pipe.WriteLineAsync(command, cancellationToken);
        return await pipe.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException();
    }

    public Task<(string Name, int Iterations)[]> WarmUp(int targetMs) => InvokeAsync<(string, int)[]>(WarmUp, targetMs);

    public Task<(long Nanos, long Bytes)> GetSample(string name, int iterations) => InvokeAsync<(long Nanos, long Bytes)>(GetSample, name, iterations);

    public Task<string[]> GetNames() => InvokeAsync<string[]>(GetNames);
}
