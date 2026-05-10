using System.Runtime.CompilerServices;
using AppleDust.Shared;

namespace AppleDust;

internal static class GCHelper
{
    [MethodImpl(Utils.AggressiveOptimization)]
    public static long GetAllocatedBytes()
    {
#if NET6_0_OR_GREATER
        return GC.GetTotalAllocatedBytes(precise: true);
#else
        return -1;
#endif
    }

    internal static void ForceGcCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
