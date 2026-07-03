namespace BridgingIT.DevKit.Presentation.Web;

using System.Text;
using Spectre.Console;

/// <summary>
/// Spectre.Console output adapter backed by a text writer.
/// </summary>
/// <example>
/// <code>
/// var output = new TextWriterAnsiConsoleOutput(writer, 120, 32);
/// </code>
/// </example>
public sealed class TextWriterAnsiConsoleOutput(TextWriter writer, int width, int height) : IAnsiConsoleOutput
{
    /// <inheritdoc />
    public TextWriter Writer { get; } = writer;

    /// <inheritdoc />
    public bool IsTerminal => true;

    /// <inheritdoc />
    public int Width { get; } = width;

    /// <inheritdoc />
    public int Height { get; } = height;

    /// <inheritdoc />
    public void SetEncoding(Encoding encoding)
    {
    }
}