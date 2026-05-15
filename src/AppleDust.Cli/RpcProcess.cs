using System.Diagnostics;
using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class RpcProcess : IDisposable
{
    private readonly Process? _process;
    private readonly DuplexServer _pipe;
    public IDuplexPipe Pipe => _pipe;

    public RpcProcess(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _pipe = DuplexServer.Create(HandleInheritability.Inheritable);
        var handles = _pipe.GetClientHandles();
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            ArgumentList = { handles.OutHandle, handles.InHandle },
            CreateNoWindow = false,
            EnvironmentVariables =
            {
                ["DOTNET_EnableDiagnostics"] = "0",
                ["COREHOST_EnableDiagnostics"] = "0",
                ["DOTNET_gcConcurrent"] = "0",
                // ["DOTNET_TieredCompilation"] = "0",
                // ["DOTNET_TC_QuickJit"] = "0",
                // ["DOTNET_TieredPGO"] = "0",
            },
        };
        _process = Process.Start(startInfo)!;
        if (_process is null)
        {
            Dispose();
            throw new InvalidOperationException($"Failed to start process: {path}");
        }
        _pipe.DisposeLocalCopyOfClientHandles();
    }

    public void Dispose()
    {
        _pipe.Dispose();
        if (_process is not null)
        {
            _process.Kill();
            _process.Dispose();
        }
    }
}
