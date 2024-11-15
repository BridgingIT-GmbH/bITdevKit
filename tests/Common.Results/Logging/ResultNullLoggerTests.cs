
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

public class ResultNullLoggerTests
{
    private readonly ResultNullLogger sut;

    public ResultNullLoggerTests()
    {
        this.sut = new ResultNullLogger();
    }

    [Fact]
    public void Log_ShouldNotThrowException()
    {
        // Arrange
        var context = "TestContext";
        var content = "TestContent";
        var result = new Result
        {
            IsSuccess = true,
            Messages = new List<string> { "Test message" },
            Errors = new List<Exception> { new Exception("Test error") }
        };

        // Act & Assert
        Should.NotThrow(() => this.sut.Log(context, content, result, LogLevel.Information));
    }

    [Fact]
    public void Log_Generic_ShouldNotThrowException()
    {
        // Arrange
        var content = "TestContent";
        var result = new Result
        {
            IsSuccess = false,
            Messages = new List<string> { "Test message" },
            Errors = new List<Exception> { new Exception("Test error") }
        };

        // Act & Assert
        Should.NotThrow(() => this.sut.Log<ResultNullLoggerTests>(content, result, LogLevel.Error));
    }
}