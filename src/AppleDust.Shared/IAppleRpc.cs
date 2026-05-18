namespace AppleDust.Shared;

#pragma warning disable IDE0051 // Remove unused private members (false positive)

internal interface IAppleRpc : IDisposable
{
    Task<int> WarmUp(string name, int targetMs);
    Task<(long Nanos, long Bytes)> GetSample(string name, int iterations);
    Task<string[]> GetNames();
}
