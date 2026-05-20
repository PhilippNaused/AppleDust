namespace AppleDust.Cli;

internal sealed record HostParameters(string Path)
{
    public bool DisableConcurrentGc { get; init; }
    public bool DisableTieredJit { get; init; }
    public bool DisablePgo { get; init; }
    public bool DisableDiagnostics { get; init; }
}
