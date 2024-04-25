// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Options;

using Microsoft.Extensions.Logging.Abstractions;

public class OptionsBuilderTests
{
    [Fact]
    public void Build_WithOptions_CreatesOptions()
    {
        // Arrange
        var sut = new OptionsStubBuilder()
            .LoggerFactory(new NullLoggerFactory())
            .Parameter1("test")
            .Parameter2(1)
            .SetParameter3();

        // Act
        var options = sut.Build();

        // Assert
        options.ShouldNotBeNull();
        options.LoggerFactory.ShouldNotBeNull();
        options.Parameter1.ShouldBe("test");
        options.Parameter2.ShouldBe(1);
        options.Parameter3.ShouldBeTrue();
    }

    [Fact]
    public void Build_WithOptions_ModifiesOptions()
    {
        // Arrange
        var sut = new OptionsStubBuilder()
            .LoggerFactory(new NullLoggerFactory())
            .Parameter1("test")
            .Parameter2(1)
            .SetParameter3();

        // Act
        sut.Parameter1("test_modified");
        sut.Parameter2(99);
        var options = sut.Build();

        // Assert
        options.ShouldNotBeNull();
        options.LoggerFactory.ShouldNotBeNull();
        options.Parameter1.ShouldBe("test_modified");
        options.Parameter2.ShouldBe(99);
        options.Parameter3.ShouldBeTrue();
    }
}