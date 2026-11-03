// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure;

using Infrastructure.Azure;

public class CosmosSqlProviderOptionsBuilderTests
{
    [Theory]
    [InlineData(-1, -1)]
    [InlineData(-30, -1)]
    [InlineData(0, -1)]
    [InlineData(60, 60)]
    public void TimeToLive_WithValue_ShouldUseValueCosmosAccepts(int seconds, int expected)
    {
        // Arrange
        var sut = new CosmosSqlProviderOptionsBuilder<CosmosStorageDocument>();

        // Act
        var result = sut.TimeToLive(seconds).Build();

        // Assert
        result.TimeToLive.ShouldBe(expected);
    }
}
