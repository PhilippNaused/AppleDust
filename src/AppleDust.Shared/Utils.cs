using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AppleDust.Shared;

internal static class Utils
{
    public const string OverheadBenchmarkName = "Overhead";
    public const char CommandSeparator = '|';
    public const char ListSeparator = ',';
    public const string ErrorPrefix = "Error:";

    public const int JitDelayMs = 500;
    public const int MinIterations = 4;

    public const MethodImplOptions AggressiveOptimization = (MethodImplOptions)0x0200;

    public static Task Delay(CancellationToken token = default) => Task.Delay(JitDelayMs, token); // Gives JIT time to optimize the code.

    private static string Escape(string text)
    {
        return Uri.EscapeDataString(text);
    }

    private static string Unescape(string text)
    {
        return Uri.UnescapeDataString(text);
    }

    public static string Serialize(object obj)
    {
        string text;
        if (obj is string s)
        {
            text = s;
        }
#if NETCOREAPP
        const char sep = ListSeparator;
#else
        string sep = ListSeparator.ToString();
#endif
        if (obj.GetType().IsTuple)
        {
            var fields = obj.GetType().GetFields();
            var values = fields.Select(f => f.GetValue(obj));
            var texts = values.Select(Serialize!);
            text = string.Join(sep, texts);
        }
        else if (obj is IList list)
        {
            text = string.Join(sep, list.Cast<object>().Select(Serialize));
        }
        else
        {
            text = Convert.ToString(obj, CultureInfo.InvariantCulture) ?? "";
        }
        return Escape(text);
    }

    public static T Deserialize<T>(string text) where T : notnull => (T)Deserialize(text, typeof(T));

    public static object Deserialize(string text, Type type)
    {
        text = Unescape(text);
        if (type == typeof(string))
            return text;
        if (type.IsPrimitive)
            return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
        if (type.IsTuple)
        {
            var fields = type.GetFields();
            var instance = Activator.CreateInstance(type)!;
            var split = text.Split([ListSeparator], StringSplitOptions.None);
            for (var i = 0; i < fields.Length; i++)
            {
                var value = Deserialize(split[i], fields[i].FieldType);
                fields[i].SetValue(instance, value);
            }
            return instance;
        }
        if (type.IsArray)
        {
            var innerType = type.GetElementType()!;
            if (text.Length == 0)
            {
                return Array.CreateInstance(innerType, 0);
            }
            string[] split = text.Split([ListSeparator], StringSplitOptions.None);
            var result = split.Select(t => Deserialize(t, innerType)).ToArray();
            var array = Array.CreateInstance(innerType, result.Length);
            result.CopyTo(array, 0);
            return array;
        }

        throw new NotSupportedException($"Cannot parse type {type.FullName}");
    }

    // ns per tick is constant for TimeSpan, but Stopwatch ticks vary by OS.
    // This is usually 100 on Windows and 1 on Unix.
    internal static readonly long StopwatchNanosecondsPerTick = 1_000_000_000 / Stopwatch.Frequency; // frequency is ticks per second

    extension(Stopwatch sw)
    {
        public long ElapsedNanoseconds => sw.ElapsedTicks * StopwatchNanosecondsPerTick;
    }

    extension(Type type)
    {
        public bool IsTuple =>
            type.IsGenericType &&
            type.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true;
    }
}
