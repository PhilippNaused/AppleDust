using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Text;
using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class AppleServer : IAppleClient
{
    private readonly CancellationToken _cancellationToken;
    private readonly AnonymousPipeServerStream _downPipe;
    private readonly AnonymousPipeServerStream _upPipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly Process? _process;

    public AppleServer(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cancellationToken = cancellationToken;
        _downPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        _upPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var encoding = new UTF8Encoding(false);
        _writer = new StreamWriter(_downPipe, encoding) { AutoFlush = true };
        _reader = new StreamReader(_upPipe, encoding);
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            ArgumentList = { _downPipe.GetClientHandleAsString(), _upPipe.GetClientHandleAsString() },
            CreateNoWindow = true
        };
        _process = Process.Start(startInfo)!;
        if (_process is null)
        {
            Dispose();
            throw new InvalidOperationException($"Failed to start process: {path}");
        }
        _downPipe.DisposeLocalCopyOfClientHandle();
        _upPipe.DisposeLocalCopyOfClientHandle();
    }

    public void Dispose()
    {
        _reader.Dispose();
        _writer.Dispose();
        _downPipe.Dispose();
        _upPipe.Dispose();
        if (_process is not null)
        {
            _process.Kill();
            _process.Dispose();
        }
        //GC.SuppressFinalize(this);
    }

    private async Task<TRet> InvokeAsync<TRet>(Delegate del, params object[] values) where TRet : notnull
    {
        var name = del.Method.Name;
        var command = string.Join("|", [name, .. values.Select(Utils.Serialize)]); // e.g. "Add|1|2"
        var response = await SendCommandAsync(command);
        if (response.StartsWith("Error:"))
        {
            var parts = response["Error:".Length..].Split('|');
            var message = Utils.Deserialize<string>(parts[0]);
            var stackTrace = Utils.Deserialize<string>(parts[1]);
            throw ExceptionDispatchInfo.SetRemoteStackTrace(new ClientException(message, stackTrace), stackTrace);
        }
        return Utils.Deserialize<TRet>(response);
    }

    internal sealed class ClientException(string text, string remoteStackTrace) : Exception(text)
    {
        public string RemoteStackTrace => remoteStackTrace;
    }

    private async Task<string> SendCommandAsync(string command)
    {
        await _writer.WriteLineAsync(command.AsMemory(), _cancellationToken);
        //_downPipe.WaitForPipeDrain();
        return await _reader.ReadLineAsync(_cancellationToken) ?? throw new InvalidOperationException("Unexpected end of stream");
    }

    public Task<int[]> WarmUp(int targetMs) => InvokeAsync<int[]>(WarmUp, targetMs);

    public Task<long> GetSample(string name, int iterations) => InvokeAsync<long>(GetSample, name, iterations);

    public Task<string[]> GetNames() => InvokeAsync<string[]>(GetNames);
}
