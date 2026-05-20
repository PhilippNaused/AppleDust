using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class RpcCaller(IDuplexPipe pipe, CancellationToken cancellationToken) : IAppleRpc
{
    public void Dispose()
    {
        pipe.Dispose();
    }

    private async Task<T> InvokeAsync<T>(Delegate del, params object[] values) where T : notnull
    {
        var name = del.Method.Name;
        var command = string.Join(Utils.CommandSeparator, [name, .. values.Select(Utils.Serialize)]); // e.g. "Add|1|2"
        var response = await SendCommandAsync(command);
        return ParseResponse<T>(response);
    }

    internal static T ParseResponse<T>(string response) where T : notnull
    {
        if (response.StartsWith(Utils.ErrorPrefix))
        {
            var error = response[Utils.ErrorPrefix.Length..];
            error = Utils.Deserialize<string>(error);
            throw new RpcException("Hosted process threw an exception", error);
        }
        return Utils.Deserialize<T>(response);
    }

    private async Task<string> SendCommandAsync(string command)
    {
        await pipe.WriteLineAsync(command, cancellationToken);
        return await pipe.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException();
    }

    public Task<int> WarmUp(string name, int targetMs) => InvokeAsync<int>(WarmUp, name, targetMs);

    public Task<(long Nanos, long Bytes)> GetSample(string name, int iterations) => InvokeAsync<(long Nanos, long Bytes)>(GetSample, name, iterations);

    public Task<string[]> GetNames() => InvokeAsync<string[]>(GetNames);
}
