namespace AppleDust.Shared;

#pragma warning disable IDE0051 // Remove unused private members (false positive)

internal interface IAppleRpc : IDisposable
{
    Task<(string Name, int Iterations)[]> WarmUp(int targetMs);
    Task<(long Nanos, long Bytes)> GetSample(string name, int iterations);
    Task<string[]> GetNames();
}
