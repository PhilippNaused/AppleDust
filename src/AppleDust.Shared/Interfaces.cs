namespace AppleDust.Shared;

#pragma warning disable IDE0051 // Remove unused private members (false positive)

internal interface IAppleClient : IDisposable
{
    Task<int[]> WarmUp(int targetMs);
    Task<long> GetSample(string name, int iterations);
    Task<string[]> GetNames();
}
