// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

[UnitTest("Application")]
public class DocumentQueryTests
{
    [Fact]
    public void DocumentQueryBuilder_WithPrefixAndTake_BuildsQuery()
    {
        // Arrange & Act
        var result = DocumentQueries.Query()
            .ForKey("people", "DE-")
            .WithRowKeyPrefix()
            .Take(25)
            .AllowFullScan()
            .Build();

        // Assert
        result.DocumentKey.ShouldBe(new DocumentKey("people", "DE-"));
        result.Filter.ShouldBe(DocumentKeyFilter.RowKeyPrefixMatch);
        result.Take.ShouldBe(25);
        result.AllowFullScan.ShouldBeTrue();
    }

    [Fact]
    public void DocumentQueryBuilder_WithInvalidTake_ShouldThrow()
    {
        // Arrange & Act
        var action = () => DocumentQueries.Query().Take(0);

        // Assert
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DocumentQueryBuilder_WithBlankContinuationToken_ShouldThrow()
    {
        // Arrange & Act
        var action = () => DocumentQueries.Query().ContinueWith(" ");

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void DocumentCountQueryBuilder_WithSuffix_BuildsQuery()
    {
        // Arrange & Act
        var result = DocumentQueries.Count()
            .ForKey("people", "-2026")
            .WithRowKeySuffix()
            .Build();

        // Assert
        result.DocumentKey.ShouldBe(new DocumentKey("people", "-2026"));
        result.Filter.ShouldBe(DocumentKeyFilter.RowKeySuffixMatch);
        result.AllowFullScan.ShouldBeFalse();
    }

    [Fact]
    public void DocumentStoreOptions_WithDefaultTakeGreaterThanMaxTake_ShouldFailValidation()
    {
        // Arrange
        var options = new DocumentStoreOptions
        {
            DefaultTake = 10,
            MaxTake = 5
        };

        // Act
        var result = options.Validate();

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void DocumentContinuationTokenSerializer_WithValidToken_ShouldRoundTrip()
    {
        // Arrange
        var token = new DocumentContinuationToken
        {
            Provider = "in-memory",
            QueryHash = "hash",
            NativeToken = "native"
        };

        // Act
        var serialized = DocumentContinuationTokenSerializer.Serialize(token);
        var deserialized = DocumentContinuationTokenSerializer.Deserialize(serialized.Value);

        // Assert
        serialized.IsSuccess.ShouldBeTrue();
        deserialized.IsSuccess.ShouldBeTrue();
        deserialized.Value.Provider.ShouldBe("in-memory");
        deserialized.Value.QueryHash.ShouldBe("hash");
        deserialized.Value.NativeToken.ShouldBe("native");
    }

    [Fact]
    public void DocumentContinuationTokenSerializer_WithMalformedToken_ShouldFail()
    {
        // Arrange & Act
        var result = DocumentContinuationTokenSerializer.Deserialize("not-a-valid-token");

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void DocumentQueryHash_WithEquivalentQueries_ShouldReturnSameHash()
    {
        // Arrange
        var first = DocumentQueries.Query()
            .ForKey("people", "DE-")
            .WithRowKeyPrefix()
            .Take(10)
            .Build();
        var second = DocumentQueries.Query()
            .ForKey("people", "DE-")
            .WithRowKeyPrefix()
            .Take(10)
            .Build();

        // Act
        var firstHash = DocumentQueryHash.Compute<DocumentClientPersonStub>("find", first, 10);
        var secondHash = DocumentQueryHash.Compute<DocumentClientPersonStub>("find", second, 10);

        // Assert
        firstHash.ShouldBe(secondHash);
    }
}
