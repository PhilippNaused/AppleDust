using System.Diagnostics;
using AppleDust.Shared;

namespace AppleDust.Tests;

internal class UtilsTests
{
    [Test]
    public async Task GetElapsedNanoSeconds()
    {
        var sw = Stopwatch.StartNew();
        await Task.Delay(10);
        sw.Stop();
        var expected = sw.Elapsed.Ticks * 100;
        // ns per tick is constant for TimeSpan, but Stopwatch ticks vary by OS.
        var actual = sw.ElapsedNanoseconds; // This has higher precision on Unix.
        await Assert.That(actual).IsEqualTo(expected).Within(100);
    }
}
