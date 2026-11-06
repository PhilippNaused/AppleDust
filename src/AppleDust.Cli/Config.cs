namespace AppleDust.Cli;

internal sealed record Config
{
    public bool ColdStart { get; init; }
    public int RestartCount { get; init; } = 5;
    public int ConcurrentSamples { get; init; } = 2;
}
