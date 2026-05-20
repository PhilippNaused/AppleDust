#if !NET6_0_OR_GREATER
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using AppleDust.Shared;

namespace AppleDust;

/// <summary>
/// https://github.com/dotnet/BenchmarkDotNet/blob/764ebd5fcdfc0b189b599fdac1bb777111218b53/src/BenchmarkDotNet/Engines/GcStats.cs
/// </summary>
internal static class GcHelper
{
    [MethodImpl(Utils.AggressiveOptimization)]
    public static long GetAllocatedBytes()
    {
#if NET6_0_OR_GREATER
        return GC.GetTotalAllocatedBytes(precise: true);
#else
        if (GcHelpers2.GetTotalAllocatedBytesDelegate != null) // it's .NET Core 3.0 with the new API available
            return GcHelpers2.GetTotalAllocatedBytesDelegate.Invoke(true); // true for the "precise" argument

        if (GcHelpers2.CanUseMonitoringTotalAllocatedMemorySize) // Monitoring is not available in Mono, see http://stackoverflow.com/questions/40234948/how-to-get-the-number-of-allocated-bytes-
            return AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

        if (GcHelpers2.GetAllocatedBytesForCurrentThreadDelegate != null)
            return GcHelpers2.GetAllocatedBytesForCurrentThreadDelegate.Invoke();

        return -1;
#endif
    }

    internal static void ForceGcCollect()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
    }
}

#if !NET6_0_OR_GREATER
// Separate class to have the cctor run lazily, to avoid enabling monitoring before the benchmarks are ran.
#pragma warning disable CA1031 // Do not catch general exception types
static file class GcHelpers2
{
    // do not reorder these, CheckMonitoringTotalAllocatedMemorySize relies on GetTotalAllocatedBytesDelegate being initialized first
    public static readonly Func<bool, long>? GetTotalAllocatedBytesDelegate = CreateGetTotalAllocatedBytesDelegate();
    public static readonly Func<long>? GetAllocatedBytesForCurrentThreadDelegate = CreateGetAllocatedBytesForCurrentThreadDelegate();
    public static readonly bool CanUseMonitoringTotalAllocatedMemorySize = CheckMonitoringTotalAllocatedMemorySize();

    private static Func<bool, long>? CreateGetTotalAllocatedBytesDelegate()
    {
        try
        {
            // this method is not a part of .NET Standard so we need to use reflection
            var method = typeof(GC).GetTypeInfo().GetMethod("GetTotalAllocatedBytes", BindingFlags.Public | BindingFlags.Static);

            if (method == null)
                return null;

            // we create delegate to avoid boxing, IMPORTANT!
            var del = (Func<bool, long>)method.CreateDelegate(typeof(Func<bool, long>));

            // verify the api works
            return del.Invoke(true) >= 0 ? del : null;
        }
        catch
        {
            return null;
        }
    }

    private static Func<long>? CreateGetAllocatedBytesForCurrentThreadDelegate()
    {
        try
        {
            // this method is not a part of .NET Standard so we need to use reflection
            var method = typeof(GC).GetTypeInfo().GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);

            if (method == null)
                return null;

            // we create delegate to avoid boxing, IMPORTANT!
            var del = (Func<long>)method.CreateDelegate(typeof(Func<long>));

            // verify the api works
            return del.Invoke() >= 0 ? del : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool CheckMonitoringTotalAllocatedMemorySize()
    {
        try
        {
            // we potentially don't want to enable monitoring if we don't need it
            if (GetTotalAllocatedBytesDelegate != null)
                return false;

            // check if monitoring is enabled
            if (!AppDomain.MonitoringIsEnabled)
                AppDomain.MonitoringIsEnabled = true;

            // verify the api works
            return AppDomain.MonitoringIsEnabled && AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize >= 0;
        }
        catch
        {
            return false;
        }
    }
}
#endif
