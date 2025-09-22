// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public class ResultLoggerTests
{
    private readonly ILogger<ResultLogger> logger;
    private readonly ResultLogger sut;

    public ResultLoggerTests()
    {
        this.logger = Substitute.For<ILogger<ResultLogger>>();
        this.sut = new ResultLogger(this.logger);
    }

    //[Fact]
    //public void Log_ShouldLogCorrectInformation()
    //{
    //    // Arrange
    //    var context = "TestContext";
    //    var content = "TestContent";
    //    var result = Result.Failure("Message1").WithError(new Exception("Error1"));
    //    var logLevel = LogLevel.Information;

    //    // Act
    //    this.sut.Log(context, content, result, logLevel);

    //    // Assert
    //    this.logger.Received(1).Log(
    //        Arg.Is(logLevel),
    //        Arg.Any<EventId>(),
    //        Arg.Any<object>(),
    //        Arg.Any<Exception>(),
    //        Arg.Any<Func<object, Exception, string>>());
    //}

    [Fact]
    public void Log_Generic_ShouldLogCorrectInformation()
    {
        // Arrange
        var content = "TestContent";
        var result = Result.Failure("Message1")
            .WithError(new Exception("Error1"));
        var logLevel = LogLevel.Error;

        // Act
        this.sut.Log<ResultLoggerTests>(content, result, logLevel);

        // Assert
        this.logger.Received(1).Log(
            Arg.Is(logLevel),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }
}