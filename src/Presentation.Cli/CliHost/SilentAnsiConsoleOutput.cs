namespace BridgingIT.DevKit.Cli;

using System.Text;
using Spectre.Console;

/// <summary>
/// Spectre.Console output adapter that discards all written output.
/// </summary>
public sealed class SilentAnsiConsoleOutput : IAnsiConsoleOutput
{
    /// <inheritdoc />
    public TextWriter Writer { get; } = TextWriter.Null;

    /// <inheritdoc />
    public bool IsTerminal => false;

    /// <inheritdoc />
    public int Width => 120;

    /// <inheritdoc />
    public int Height => 32;

    /// <inheritdoc />
    public void SetEncoding(Encoding encoding)
    {
    }
}
