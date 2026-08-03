namespace BridgingIT.DevKit.Presentation.UnitTests.ConsoleCommands;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Rendering;

[UnitTest("Presentation")]
public sealed class ConsoleThemeTests
{
    [Fact]
    public void All_WhenCalled_ContainsDashboardThemeNames()
    {
        // Arrange & Act
        var names = ConsoleThemeRegistry.All.Select(t => t.Name).ToArray();

        // Assert
        names.ShouldContain("dark");
        names.ShouldContain("catppuccin");
        names.ShouldContain("darkforest");
        names.ShouldContain("carbon");
        names.ShouldContain("tokionights");
        names.ShouldContain("matrix");
        names.ShouldContain("light");
        names.ShouldContain("system");
    }

    [Fact]
    public void Set_WhenKnownTheme_ChangesCurrentTheme()
    {
        // Arrange
        var previous = ConsoleTheme.Current.Name;

        try
        {
            // Act
            var changed = ConsoleTheme.Set("matrix");

            // Assert
            changed.ShouldBeTrue();
            ConsoleTheme.Current.Name.ShouldBe("matrix");
            ConsoleTheme.Current.PromptStyle.ShouldBe("lime");
        }
        finally
        {
            ConsoleTheme.Set(previous);
        }
    }

    [Fact]
    public void Set_WhenUnknownTheme_DoesNotChangeCurrentTheme()
    {
        // Arrange
        var previous = ConsoleTheme.Current.Name;

        try
        {
            // Act
            var changed = ConsoleTheme.Set("missing-theme");

            // Assert
            changed.ShouldBeFalse();
            ConsoleTheme.Current.Name.ShouldBe(previous);
        }
        finally
        {
            ConsoleTheme.Set(previous);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenThemeProvided_ChangesThemeAndWritesAvailableThemes()
    {
        // Arrange
        var previous = ConsoleTheme.Current.Name;
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new StringWriterAnsiConsoleOutput(writer) });
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new ConsoleThemeConsoleCommand { Theme = "carbon" };

        try
        {
            // Act
            await sut.ExecuteAsync(console, services);

            // Assert
            ConsoleTheme.Current.Name.ShouldBe("carbon");
            writer.ToString().ShouldContain("carbon");
            writer.ToString().ShouldContain("matrix");
        }
        finally
        {
            ConsoleTheme.Set(previous);
        }
    }

    [Fact]
    public void Process_WhenThemeChanges_RendersSpectreColorsWithCurrentTheme()
    {
        // Arrange
        var previous = ConsoleTheme.Current.Name;
        var sut = new ConsoleThemeRenderHook();
        var renderable = new SegmentRenderable(new Segment("failed", new Style(Color.Red, null, Decoration.Bold)));

        try
        {
            // Act
            ConsoleTheme.Set("catppuccin");
            var catppuccin = sut.Process(null, [renderable]).Single().Render(null, 120).Single();

            ConsoleTheme.Set("matrix");
            var matrix = sut.Process(null, [renderable]).Single().Render(null, 120).Single();

            // Assert
            catppuccin.Style.Foreground.ShouldBe(Style.Parse(ConsoleThemeRegistry.Get("catppuccin").ErrorStyle).Foreground);
            catppuccin.Style.Decoration.ShouldBe(Decoration.Bold);
            matrix.Style.Foreground.ShouldBe(Style.Parse(ConsoleThemeRegistry.Get("matrix").ErrorStyle).Foreground);
            matrix.Style.Decoration.ShouldBe(Decoration.Bold);
        }
        finally
        {
            ConsoleTheme.Set(previous);
        }
    }

    private sealed class StringWriterAnsiConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
    {
        public TextWriter Writer { get; } = writer;

        public bool IsTerminal => false;

        public int Width => 120;

        public int Height => 32;

        public void SetEncoding(System.Text.Encoding encoding)
        {
        }
    }

    private sealed class SegmentRenderable(Segment segment) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            return new Measurement(segment.CellCount(), segment.CellCount());
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            yield return segment;
        }
    }
}
