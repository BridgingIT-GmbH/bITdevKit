namespace BridgingIT.DevKit.Presentation.UnitTests.ConsoleCommands;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

[UnitTest("Presentation")]
public sealed class DocsConsoleCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRegisteredAsRegularConsoleCommand_OpensOfficialDocumentation()
    {
        // Arrange
        var launcher = new FakeDocsBrowserLauncher();
        var services = new ServiceCollection()
            .AddSingleton<IDocsBrowserLauncher>(launcher)
            .BuildServiceProvider();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new StringWriterAnsiConsoleOutput(new StringWriter()) });
        var sut = new DocsConsoleCommand();

        // Act
        await sut.ExecuteAsync(console, services);

        // Assert
        launcher.OpenedUrl.ShouldBe(DocsConsoleCommand.OfficialDocsUrl);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUrlOnly_DoesNotOpenBrowser()
    {
        // Arrange
        var launcher = new FakeDocsBrowserLauncher();
        var services = new ServiceCollection()
            .AddSingleton<IDocsBrowserLauncher>(launcher)
            .BuildServiceProvider();
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new StringWriterAnsiConsoleOutput(writer) });
        var sut = new DocsConsoleCommand { UrlOnly = true };

        // Act
        await sut.ExecuteAsync(console, services);

        // Assert
        launcher.OpenedUrl.ShouldBeNull();
        writer.ToString().ShouldContain(DocsConsoleCommand.OfficialDocsUrl);
    }

    private sealed class FakeDocsBrowserLauncher : IDocsBrowserLauncher
    {
        public string OpenedUrl { get; private set; }

        public void Open(string url) => this.OpenedUrl = url;
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
}
