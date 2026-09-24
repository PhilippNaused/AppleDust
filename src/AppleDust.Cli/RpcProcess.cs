using System.Diagnostics;
using AppleDust.Shared;

namespace AppleDust.Cli;

internal sealed class RpcProcess : IDisposable
{
    private readonly Process? _process;
    public DuplexPipe Pipe { get; }

    public RpcProcess(HostParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo(parameters.Path)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
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
        Pipe = new DuplexPipe(_process.StandardOutput, _process.StandardInput);
        if (_process is null)
        {
            Dispose();
            throw new InvalidOperationException($"Failed to start process: '{parameters.Path}'");
        }
    }

    public void Dispose()
    {
        Pipe.Dispose();
        if (_process is not null)
        {
            _process.Kill();
            _process.Dispose();
        }
    }
}
