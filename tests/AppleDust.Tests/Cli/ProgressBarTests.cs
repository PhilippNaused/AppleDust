using AppleDust.Cli;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AppleDust.Tests.Cli;

internal class ProgressBarTests
{
    private sealed class TestConsole : IReadOnlyCapabilities
    {
        public ColorSystem ColorSystem => ColorSystem.Standard;
        public bool Ansi => true;
        public bool Links => true;
        public bool AlternateBuffer => false;
        public bool Legacy => false;
        public bool Interactive => true;
        public bool Unicode => true;
    }

    [Test]
    [Arguments(0.00, "          ")]
    [Arguments(0.01, "▏         ")]
    [Arguments(0.10, "█         ")]
    [Arguments(0.50, "█████     ")]
    [Arguments(0.51, "█████▏    ")]
    [Arguments(1.00, "██████████")]
    [Arguments(0.00, "    ")]
    [Arguments(0.08, "▍   ")]
    [Arguments(0.75, "███ ")]
    [Arguments(0.90, "███▋")]
    [Arguments(1.00, "████")]
    public async Task ProgressBar_Render(double value, string expected)
    {
        int size = expected.Length;
        var progressBar = new ProgressBar { Value = value, Width = size };
        var segments = progressBar.Render(new RenderOptions(new TestConsole(), new Size(size, 1)), size).ToArray();
        await Assert.That(segments).Count().IsEqualTo(1);
        await Assert.That(segments[0].Text).IsEqualTo(expected);
    }
}
