namespace BridgingIT.DevKit.Presentation.UnitTests.ConsoleCommands;

using BridgingIT.DevKit.Common;
using ConsoleCommandApplicationBuilderExtensions = Microsoft.Extensions.DependencyInjection.ApplicationBuilderExtensions;

[UnitTest("Presentation")]
public sealed class ApplicationBuilderExtensionsTests
{
    [Fact]
    public void SelectTerminalInputMode_LinuxInteractiveTerminal_ReturnsBasic()
    {
        var result = ConsoleCommandApplicationBuilderExtensions.SelectTerminalInputMode(
            isInputRedirected: false,
            isOutputRedirected: false,
            isLinux: true);

        result.ShouldBe(ConsoleCommandApplicationBuilderExtensions.TerminalInputMode.Basic);
    }

    [Fact]
    public void SelectTerminalInputMode_NonLinuxInteractiveTerminal_ReturnsEnhanced()
    {
        var result = ConsoleCommandApplicationBuilderExtensions.SelectTerminalInputMode(
            isInputRedirected: false,
            isOutputRedirected: false,
            isLinux: false);

        result.ShouldBe(ConsoleCommandApplicationBuilderExtensions.TerminalInputMode.Enhanced);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void SelectTerminalInputMode_RedirectedConsole_ReturnsRedirected(
        bool isInputRedirected,
        bool isOutputRedirected,
        bool isLinux)
    {
        var result = ConsoleCommandApplicationBuilderExtensions.SelectTerminalInputMode(
            isInputRedirected,
            isOutputRedirected,
            isLinux);

        result.ShouldBe(ConsoleCommandApplicationBuilderExtensions.TerminalInputMode.Redirected);
    }
}
