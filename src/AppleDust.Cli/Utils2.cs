using System.Collections.Immutable;
using Perfolizer.Mathematics.Distributions.ContinuousDistributions;
using Pragmastat.Algorithms;
using Spectre.Console;
using static System.Math;

namespace AppleDust.Cli;

internal static class Utils2
{
    public static string AsTime(double nanos)
    {
        if (nanos < 1_000)
            return $"{nanos:F2} ns";
        if (nanos < 1_000_000)
            return $"{nanos / 1_000:F2} µs";
        if (nanos < 1_000_000_000)
            return $"{nanos / 1_000_000:F2} ms";
        return $"{nanos / 1_000_000_000:F2} s";
    }

    // Number of milliseconds that each sample of the benchmark should take.
    // lower values will make the benchmark run faster, but may lead to higher relative errors.
    public const int TargetMs = 500;
    public const int TargetNs = TargetMs * 1_000_000;

    public static Stats Analyze(ImmutableArray<double> samples)
    {
        if (samples.Length == 0)
        {
            return Stats.NaN;
        }
        var (mean, variance) = GetMoments(samples);
        var stdDev = Sqrt(variance);
        var stdErr = stdDev / Sqrt(samples.Length);

        return new Stats(mean, stdDev, stdErr, samples);
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

    public static double Sq(double d) => d * d;

    private static (double Mean, double Variance) GetMoments(ImmutableArray<double> values)
    {
        if (values.IsDefaultOrEmpty)
        {
            return (double.NaN, double.NaN);
        }
        var mean = values.Average();
        var n = values.Length;
        var variance = n == 1 ? 0 : values.Sum(d => Sq(d - mean)) / (n - 1);
        return (mean, variance);
    }

    private static double[] GetRatios(ImmutableArray<double> x, ImmutableArray<double> y, double[] p)
    {
        x = x.RemoveAll(NotPositive).Sort();
        y = y.RemoveAll(NotPositive).Sort();
        if (x.Length < 1 || y.Length < 1)
            return new double[p.Length];
        return FastRatio.Estimate(x, y, p, true);
        static bool NotPositive(double d) => d <= 0;
    }

    public static (double Ratio, double Margin) GetRatio(ImmutableArray<double> x, ImmutableArray<double> y, double error = 0.05)
    {
        const double mid = 0.5d;
        var ratios = GetRatios(x, y, [mid - error, mid, mid + error]);
        var lower = ratios[0];
        var median = ratios[1];
        var upper = ratios[2];
        return (median, (upper - lower) / 2);
    }

    public static Task WithStatus(this Task task, string status) => AnsiConsole.Status().StartAsync(status, async _ => await task);
    public static Task<T> WithStatus<T>(this Task<T> task, string status) => AnsiConsole.Status().StartAsync(status, async _ => await task);
    public static Task<T> WithStatus<T>(this ValueTask<T> task, string status) => AnsiConsole.Status().StartAsync(status, async _ => await task);
}

internal readonly record struct Stats(double Mean, double StdDev, double StdErr, ImmutableArray<double> Samples)
{
    public static Stats NaN { get; } = new(double.NaN, double.NaN, double.NaN, []);
}
