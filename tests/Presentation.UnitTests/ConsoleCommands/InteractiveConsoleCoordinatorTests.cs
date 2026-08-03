namespace BridgingIT.DevKit.Presentation.UnitTests.ConsoleCommands;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;

[UnitTest("Presentation")]
public sealed class InteractiveConsoleCoordinatorTests
{
    [Fact]
    public void IsInputActive_WhenNoInputSessionStarted_ReturnsFalse()
    {
        InteractiveConsoleCoordinator.Instance.IsInputActive.ShouldBeFalse();
    }
}
