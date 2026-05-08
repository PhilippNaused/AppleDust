using System.Collections.Immutable;
using AppleDust.Cli;

namespace AppleDust.Tests.Cli;

internal class MathTests
{
    [Test]
    public async Task WelchTest()
    {
        ImmutableArray<double> x = [14, 15, 15, 15, 16, 18, 22, 23, 24, 25, 25];
        ImmutableArray<double> y = [10, 12, 14, 15, 18, 22, 24, 27, 31, 33, 34, 34, 34];
        var (t, df, pValue) = Utils2.WelchTest(x, y);
        await Assert.That(t).IsEqualTo(-1.5379022758390941);
        await Assert.That(df).IsEqualTo(18.137377998778444);
        await Assert.That(pValue).IsEqualTo(0.1413355279311126);
    }

    [Test]
    public async Task WelchTest2()
    {
        ImmutableArray<double> x = [14, 15, 15, 15, 16, 18, 22, 23, 24, 25, 25];
        ImmutableArray<double> y = [25, 15, 15, 15, 16, 18, 22, 23, 24, 25, 14];
        var (t, df, pValue) = Utils2.WelchTest(x, y);
        await Assert.That(t).IsEqualTo(0);
        await Assert.That(df).IsEqualTo(x.Length - 1 + y.Length - 1);
        await Assert.That(pValue).IsEqualTo(1);
    }
}
