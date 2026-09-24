namespace AppleDust.Shared;

#pragma warning disable IDE0051 // Remove unused private members (false positive)

internal interface IAppleRpc : IDisposable
{
    Task<long> WarmUp(string name, int targetMs);
    Task<(long Nanos, long Bytes)> GetSample(string name, long iterations);
    Task<string[]> GetNames();
}
