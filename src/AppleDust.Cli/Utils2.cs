using System.Collections.Immutable;
using System.Diagnostics;
using Perfolizer.Mathematics.Distributions.ContinuousDistributions;
using Pragmastat;
using Pragmastat.Exceptions;
using Spectre.Console;
using static System.Math;

namespace AppleDust.Cli;

internal static class Utils2
{
    public static string AsTime(double nanos)
    {
        var abs = Abs(nanos);
        if (abs < 1_000)
            return $"{nanos:F3} ns";
        if (abs < 1_000_000)
            return $"{nanos / 1_000:F3} µs";
        if (abs < 1_000_000_000)
            return $"{nanos / 1_000_000:F3} ms";
        return $"{nanos / 1_000_000_000:F3} s";
    }

    // Number of milliseconds that each sample of the benchmark should take.
    // lower values will make the benchmark run faster, but may lead to higher relative errors.
    public const int TargetMs = 250;
    public const int TargetNs = TargetMs * 1_000_000;

    public static Stats Analyze(ImmutableArray<double> samples)
    {
        if (samples.Length == 0)
        {
            return Stats.NaN;
        }
        var s = new Sample(samples);
        double center = Toolkit.Center(s).NominalValue;
        double spread = double.NaN;

        try
        {
            spread = Toolkit.Spread(s).NominalValue;
        }
        catch (AssumptionException)
        {
            // sparity assumption not met.
        }

        return new Stats(center, spread, samples);
    }

    public static (double ratio, double shift, double disparity, double pValue) CompareToBaseline(ImmutableArray<double> samples, ImmutableArray<double> baselineSamples)
    {
        if (samples.Length < 1 || baselineSamples.Length < 1)
        {
            return (double.NaN, double.NaN, double.NaN, double.NaN);
        }
        var s = samples.AsSample();
        var b = baselineSamples.AsSample();
        if (s.Size < 1 || b.Size < 1)
        {
            return (double.NaN, double.NaN, double.NaN, double.NaN);
        }
        double ratio;
        double shift = Toolkit.Shift(s, b).NominalValue;
        ratio = Toolkit.Ratio(s, b).NominalValue;
        if (s.Size < 2 || b.Size < 2)
        {
            return (ratio, shift, double.NaN, double.NaN);
        }
        double disparity = double.NaN;
        try
        {
            disparity = Toolkit.Disparity(s, b).NominalValue;
        }
        catch (AssumptionException)
        {
            // Sparity assumption not met.
        }
        var (_, _, pValue) = WelchTest(samples, baselineSamples);
        return (ratio, shift, disparity, pValue);
    }

    /// <summary>
    /// Welch's t-test for two independent samples with unequal variances.
    /// </summary>
    /// <returns>The t-statistic, degrees of freedom, and p-value of the test.</returns>
    public static (double tStat, double df, double pValue) WelchTest(ImmutableArray<double> samples1, ImmutableArray<double> samples2)
    {
        // sample size
        var n1 = samples1.Length;
        var n2 = samples2.Length;
        if (n1 < 2 || n2 < 2)
        {
            return (double.NaN, double.NaN, double.NaN);
        }
        // sample mean & variance
        var (m1, v1) = GetMoments(samples1);
        var (m2, v2) = GetMoments(samples2);
        double vn1 = v1 / n1;
        double vn2 = v2 / n2;
        // t-statistic
        var tStat = (m1 - m2) / Sqrt(vn1 + vn2);
        // degrees of freedom
        var df = Sq(vn1 + vn2) / (Sq(vn1) / (n1 - 1) + Sq(vn2) / (n2 - 1));
        // cumulative distribution function
        double cdf = new StudentDistribution(df).Cdf(tStat);
        var pValue = Min(Min(cdf, 1 - cdf) * 2, 1);
        return (tStat, df, pValue);
    }

    private static double Sq(double d) => d * d;

    private static (double Mean, double Variance) GetMoments(ImmutableArray<double> values)
    {
        if (values.IsDefaultOrEmpty)
        {
            return (double.NaN, double.NaN);
        }
        if (values.Length == 1)
        {
            return (values[0], double.NaN);
        }
        var mean = values.Average();
        var n = values.Length;
        var variance = n == 1 ? 0 : values.Sum(d => Sq(d - mean)) / (n - 1);
        return (mean, variance);
    }

    public static Task WithStatus(this Task task, string status) => AnsiConsole.Status().StartAsync(status, async _ => await task);
    public static Task<T> WithStatus<T>(this Task<T> task, string status) => AnsiConsole.Status().StartAsync(status, async _ => await task);
    public static Task<T> WithStatus<T>(this ValueTask<T> task, string status) => AnsiConsole.Status().StartAsync(status, async _ => await task);

    public static double GetTotalCpuUsage()
    {
        if (!OperatingSystem.IsWindows())
            return double.NaN;
        _cpuCounter ??= new PerformanceCounter("Processor", "% Processor Time", "_Total");
        if (_cpuSamples.Count == _cpuSamples.Capacity)
        {
            _ = _cpuSamples.Dequeue();
        }
        _cpuSamples.Enqueue(_cpuCounter.NextValue() / 100);
        return _cpuSamples.Average();
    }

    private static readonly Queue<double> _cpuSamples = new(10);
    private static PerformanceCounter? _cpuCounter;

    public static Style GetColor(int index)
    {
        return index switch
        {
            2 => Styles.Green,
            1 => Styles.GreenYellow,
            0 => Styles.Yellow,
            -1 => Styles.Orange,
            -2 => Styles.Red,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
        };
    }

    public static Style GetRatioStyle(double ratio, double disparity, double pValue)
    {
        const double significanceLevel = 0.01;
        if (double.IsNaN(ratio))
        {
            return Styles.Dim;
        }
        var points = 0;
        if (pValue <= significanceLevel)
            points++;
        if (Abs(disparity) > 1)
            points++;

        return GetColor(ratio < 1 ? points : -points);
    }

    public static int ScoreDev(double mean, double dev)
    {
        var relDev = dev / Abs(mean);
        return relDev switch
        {
            < 0.01 => 2, // 1%
            < 0.02 => 1, // 2%
            < 0.05 => 0, // 5%
            < 0.10 => -1, // 10%
            _ => -2 // bad
        };
    }

    public static Style SignificanceColor(bool pass)
    {
        return pass ? Styles.Green : Styles.Yellow;
    }

    extension(ImmutableArray<double> samples)
    {
        public Sample AsSample() => new(samples.Where(double.IsNormal).ToList());
    }
}

internal readonly record struct Stats(double Center, double Spread, ImmutableArray<double> Samples)
{
    public static Stats NaN { get; } = new(double.NaN, double.NaN, []);
}
