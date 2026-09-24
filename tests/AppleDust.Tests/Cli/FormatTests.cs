using AppleDust.Cli;

namespace AppleDust.Tests.Cli;

internal class FormatTests
{
    public static (double, string)[] GetTestValues() =>
        [
            (1_000_000_000_000, "1000.000 s"),
            (1_000_000_000,        "1.000 s"),
            (1_000_000,            "1.000 ms"),
            (1_000,                "1.000 µs"),
            (100,                "100.000 ns"),
            (10,                  "10.000 ns"),
            (1,                    "1.000 ns"),
            (0.1,                "100.000 ps"),
            (0.01,                "10.000 ps"),
            (0.001,                "1.000 ps"),
            (0.000001,             "0.001 ps"),
        ];

    [Test]
    [MethodDataSource(nameof(GetTestValues))]
    public async Task FormatNanoseconds(double nanos, string expected)
    {
        var actual = Utils2.AsTime(nanos);
        await Assert.That(actual).IsEqualTo(expected);
    }
}
