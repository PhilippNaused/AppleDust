using Spectre.Console;

namespace AppleDust.Cli;

internal static class Styles
{
    public static readonly Style Plain = new();
    public static readonly Style Red = new(Color.Red);
    public static readonly Style Green = new(Color.Green);
    public static readonly Style GreenYellow = new(Color.GreenYellow);
    public static readonly Style Yellow = new(Color.Yellow);
    public static readonly Style Orange = new(Color.Orange1);
    public static readonly Style Underline = new(decoration: Decoration.Underline);
    public static readonly Style Dim = new(decoration: Decoration.Dim);
}
