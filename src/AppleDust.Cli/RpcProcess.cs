using System.Diagnostics;
using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class RpcProcess : IDisposable
{
    private readonly Process? _process;
    private readonly DuplexServer _pipe;
    public IDuplexPipe Pipe => _pipe;

    public RpcProcess(HostParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _pipe = DuplexServer.Create(HandleInheritability.Inheritable);
        var handles = _pipe.GetClientHandles();
        var startInfo = new ProcessStartInfo(parameters.Path)
        {
            UseShellExecute = false,
            ArgumentList = { handles.OutHandle, handles.InHandle },
            CreateNoWindow = false
        };
        if (parameters.DisableConcurrentGc)
        {
            startInfo.EnvironmentVariables["DOTNET_gcConcurrent"] = "0";
        }
        if (parameters.DisableTieredJit)
        {
            startInfo.EnvironmentVariables["DOTNET_TieredCompilation"] = "0";
        }
        if (parameters.DisablePgo)
        {
            startInfo.EnvironmentVariables["DOTNET_TieredPGO"] = "0";
        }
        if (parameters.DisableDiagnostics)
        {
            startInfo.EnvironmentVariables["DOTNET_EnableDiagnostics"] = "0";
            startInfo.EnvironmentVariables["COREHOST_EnableDiagnostics"] = "0";
        }
        _process = Process.Start(startInfo)!;
        if (_process is null)
        {
            Dispose();
            throw new InvalidOperationException($"Failed to start process: '{parameters.Path}'");
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
