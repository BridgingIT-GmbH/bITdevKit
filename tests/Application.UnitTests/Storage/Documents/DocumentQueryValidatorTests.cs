// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

[UnitTest("Application")]
public class DocumentQueryValidatorTests
{
    private readonly DocumentStoreProviderCapabilities capabilities = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedClientSide,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedServerSide,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = true,
        SupportsKeyOnlyProjection = true
    };

    [Fact]
    public void ValidatePage_WithoutTake_UsesDefaultTake()
    {
        // Arrange
        var query = DocumentQueries.Query()
            .ForKey("people", "42")
            .Build();

        // Act
        var result = DocumentQueryValidator.ValidatePage<DocumentClientPersonStub>(
            "find",
            "provider",
            query,
            this.capabilities,
            new DocumentStoreOptions { DefaultTake = 25, MaxTake = 100 });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Take.ShouldBe(25);
    }

    [Fact]
    public void ValidatePage_WithTakeAboveMax_ShouldFail()
    {
        // Arrange
        var query = DocumentQueries.Query()
            .ForKey("people", "42")
            .Take(101)
            .Build();

        // Act
        var result = DocumentQueryValidator.ValidatePage<DocumentClientPersonStub>(
            "find",
            "provider",
            query,
            this.capabilities,
            new DocumentStoreOptions { DefaultTake = 25, MaxTake = 100 });

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePage_WithFullScanWhenOptionsDisallowFullScans_ShouldFail()
    {
        // Arrange
        var query = DocumentQueries.Query()
            .AllowFullScan()
            .Build();

        // Act
        var result = DocumentQueryValidator.ValidatePage<DocumentClientPersonStub>(
            "find",
            "provider",
            query,
            this.capabilities,
            new DocumentStoreOptions { AllowFullScans = false });

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePage_WithClientSideFilterWhenRejected_ShouldFail()
    {
        // Arrange
        var query = DocumentQueries.Query()
            .ForKey("people", "-2026")
            .WithRowKeySuffix()
            .Build();

        // Act
        var result = DocumentQueryValidator.ValidatePage<DocumentClientPersonStub>(
            "find",
            "provider",
            query,
            this.capabilities,
            new DocumentStoreOptions { RejectClientSideFilteredQueries = true });

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePage_WithContinuationTokenForDifferentProvider_ShouldFail()
    {
        // Arrange
        var query = DocumentQueries.Query()
            .ForKey("people", "42")
            .Take(10)
            .Build();
        var queryHash = DocumentQueryHash.Compute<DocumentClientPersonStub>("find", query, 10);
        var token = DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken
        {
            Provider = "other",
            QueryHash = queryHash,
            NativeToken = "native"
        }).Value;

        // Act
        var result = DocumentQueryValidator.ValidatePage<DocumentClientPersonStub>(
            "find",
            "provider",
            DocumentQueries.Query()
                .ForKey("people", "42")
                .Take(10)
                .ContinueWith(token)
                .Build(),
            this.capabilities,
            new DocumentStoreOptions());

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePage_WithContinuationTokenForDifferentQuery_ShouldFail()
    {
        // Arrange
        var token = DocumentContinuationTokenSerializer.Serialize(new DocumentContinuationToken
        {
            Provider = "provider",
            QueryHash = "different-query",
            NativeToken = "native"
        }).Value;

        // Act
        var result = DocumentQueryValidator.ValidatePage<DocumentClientPersonStub>(
            "find",
            "provider",
            DocumentQueries.Query()
                .ForKey("people", "42")
                .Take(10)
                .ContinueWith(token)
                .Build(),
            this.capabilities,
            new DocumentStoreOptions());

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCount_WithSupportedQuery_ShouldReturnQueryHash()
    {
        // Arrange
        var query = DocumentQueries.Count()
            .ForKey("people", "DE-")
            .WithRowKeyPrefix()
            .Build();

        // Act
        var result = DocumentQueryValidator.ValidateCount<DocumentClientPersonStub>(
            "count",
            query,
            this.capabilities,
            new DocumentStoreOptions());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.QueryHash.ShouldNotBeNullOrWhiteSpace();
    }
}
