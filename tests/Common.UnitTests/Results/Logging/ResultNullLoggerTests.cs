// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using Microsoft.Extensions.Logging;
using Xunit;

public class ResultNullLoggerTests
{
    private readonly ResultNullLogger logger;

    public ResultNullLoggerTests() => this.logger = new ResultNullLogger();

    [Fact]
    public void Log_WithStringContext_DoesNotThrowException()
    {
        // Arrange
        const string context = "TestContext";
        const string content = "Test content";
        var result = Result.Success();
        const LogLevel logLevel = LogLevel.Information;

        // Act & Assert
        var exception = Record.Exception(() => this.logger.Log(context, content, result, logLevel));
        Assert.Null(exception);
    }

    [Fact]
    public void Log_WithGenericContext_DoesNotThrowException()
    {
        // Arrange
        const string content = "Test content";
        var result = Result.Success();
        const LogLevel logLevel = LogLevel.Information;

        // Act & Assert
        var exception = Record.Exception(() => this.logger.Log<ResultNullLoggerTests>(content, result, logLevel));
        Assert.Null(exception);
    }
}
