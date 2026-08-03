// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using System.Text;

[UnitTest("Application")]
public class BlobStorageRuntimeHelperTests
{
    private readonly BlobStoreProviderCapabilities capabilities = new()
    {
        SupportsContinuationPaging = true,
        SupportsPrefixListing = true,
        SupportsFullContainerScan = true
    };

    [Fact]
    public async Task ComputeSha256Async_WithContent_ReturnsLowercasePrefixedHash()
    {
        // Arrange
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        // Act
        var result = await BlobContentHash.ComputeSha256Async(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public async Task ComputeSha256Async_WithDifferentMetadata_DependsOnlyOnContentBytes()
    {
        // Arrange
        var first = new BlobUpload
        {
            Key = new BlobKey("reports", "one.pdf"),
            Content = new MemoryStream([1, 2, 3]),
            ContentType = ContentType.PDF
        };
        var second = new BlobUpload
        {
            Key = new BlobKey("exports", "two.csv"),
            Content = new MemoryStream([1, 2, 3]),
            ContentType = ContentType.CSV
        };
        var differentContent = new MemoryStream([1, 2, 4]);

        // Act
        var firstHash = await BlobContentHash.ComputeSha256Async(first.Content);
        var secondHash = await BlobContentHash.ComputeSha256Async(second.Content);
        var differentHash = await BlobContentHash.ComputeSha256Async(differentContent);

        // Assert
        firstHash.Value.ShouldBe(secondHash.Value);
        firstHash.Value.ShouldNotBe(differentHash.Value);
    }

    [Theory]
    [InlineData("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    [InlineData("sha1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("sha256:aaaaaaaa", false)]
    public void ValidateExpectedHash_WithCandidate_RequiresSha256LowercaseFormat(string expectedHash, bool shouldSucceed)
    {
        // Arrange & Act
        var result = BlobContentHash.ValidateExpectedHash(expectedHash);

        // Assert
        result.IsSuccess.ShouldBe(shouldSucceed);
    }

    [Fact]
    public void BlobValidator_WithKnownLengthOverMaxBlobSize_FailsBeforeReadingContent()
    {
        // Arrange
        using var content = new MemoryStream([1, 2, 3, 4, 5]);
        var upload = new BlobUpload
        {
            Key = new BlobKey("reports", "large.bin"),
            Content = content
        };

        // Act
        var result = BlobValidator.Validate(upload, new BlobStoreOptions { MaxBlobSize = 4 });

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.GetError<BlobStoreSizeLimitExceededError>().ActualSize.ShouldBe(5);
        content.Position.ShouldBe(0);
    }

    [Fact]
    public async Task CopyToAsync_WithUnknownLengthOverMaxBlobSize_FailsWhenStreamExceedsLimit()
    {
        // Arrange
        using var source = new UnknownLengthReadStream([1, 2, 3, 4, 5]);
        using var destination = new MemoryStream();

        // Act
        var result = await BlobSizeLimit.CopyToAsync(source, destination, 4, bufferSize: 2);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var error = result.GetError<BlobStoreSizeLimitExceededError>();
        error.ActualSize.ShouldBe(5);
        error.MaxSize.ShouldBe(4);
        destination.ToArray().ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void NormalizeAndValidate_WithoutTake_UsesDefaultTake()
    {
        // Arrange
        var query = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2026/")
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            query,
            new BlobStoreOptions { DefaultTake = 25, MaxTake = 100 },
            this.capabilities);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Take.ShouldBe(25);
        result.Value.Query.Take.ShouldBe(25);
    }

    [Fact]
    public void NormalizeAndValidate_WithTakeAboveMaxTake_ReturnsPageSizeError()
    {
        // Arrange
        var query = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2026/")
            .Take(101)
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            query,
            new BlobStoreOptions { DefaultTake = 25, MaxTake = 100 },
            this.capabilities);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.GetError<BlobStorePageSizeExceededError>().Take.ShouldBe(101);
    }

    [Fact]
    public void NormalizeAndValidate_WithFullScanWithoutQueryApproval_ReturnsQueryTooBroad()
    {
        // Arrange
        var query = BlobQueries.Query()
            .InContainer("reports")
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            query,
            new BlobStoreOptions { AllowFullScans = true, RequireExplicitFullScanApproval = true },
            this.capabilities);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.GetError<BlobStoreQueryTooBroadError>().Message.ShouldContain("AllowFullScan");
    }

    [Fact]
    public void NormalizeAndValidate_WithFullScanWithoutGlobalApproval_ReturnsQueryTooBroad()
    {
        // Arrange
        var query = BlobQueries.Query()
            .InContainer("reports")
            .AllowFullScan()
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            query,
            new BlobStoreOptions { AllowFullScans = false },
            this.capabilities);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.GetError<BlobStoreQueryTooBroadError>().Message.ShouldContain("disabled");
    }

    [Fact]
    public void NormalizeAndValidate_WithApprovedFullScan_ReturnsSuccess()
    {
        // Arrange
        var query = BlobQueries.Query()
            .InContainer("reports")
            .AllowFullScan()
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            query,
            new BlobStoreOptions { AllowFullScans = true },
            this.capabilities);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void BlobContinuationTokenSerializer_WithToken_ReturnsOpaqueRoundTrippableToken()
    {
        // Arrange
        var token = new BlobContinuationToken
        {
            Provider = "provider",
            QueryHash = "query-hash",
            Container = "reports",
            Name = "2026/06/report.pdf",
            NativeToken = "raw-provider-token"
        };

        // Act
        var serialized = BlobContinuationTokenSerializer.Serialize(token);
        var deserialized = BlobContinuationTokenSerializer.Deserialize(serialized.Value);

        // Assert
        serialized.IsSuccess.ShouldBeTrue();
        serialized.Value.ShouldNotContain("raw-provider-token");
        deserialized.IsSuccess.ShouldBeTrue();
        deserialized.Value.Provider.ShouldBe("provider");
        deserialized.Value.QueryHash.ShouldBe("query-hash");
        deserialized.Value.NativeToken.ShouldBe("raw-provider-token");
    }

    [Fact]
    public void NormalizeAndValidate_WithContinuationTokenForDifferentQuery_ReturnsInvalidToken()
    {
        // Arrange
        var firstQuery = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2026/")
            .Build();
        var token = BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken
        {
            Provider = "provider",
            QueryHash = BlobQueryHash.Compute(firstQuery),
            NativeToken = "native"
        }).Value;
        var secondQuery = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2025/")
            .ContinueWith(token)
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            secondQuery,
            new BlobStoreOptions(),
            this.capabilities);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.GetError<BlobStoreInvalidContinuationTokenError>().Message.ShouldContain("query");
    }

    [Fact]
    public void NormalizeAndValidate_WithContinuationTokenAndDifferentTake_ReturnsSuccess()
    {
        // Arrange
        var firstQuery = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2026/")
            .Take(10)
            .Build();
        var token = BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken
        {
            Provider = "provider",
            QueryHash = BlobQueryHash.Compute(firstQuery),
            NativeToken = "native"
        }).Value;
        var secondQuery = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2026/")
            .Take(20)
            .ContinueWith(token)
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            secondQuery,
            new BlobStoreOptions { MaxTake = 100 },
            this.capabilities);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Take.ShouldBe(20);
        result.Value.ContinuationToken.NativeToken.ShouldBe("native");
    }

    [Fact]
    public void BlobQueryHash_WithDifferentTakeAndContinuationToken_ReturnsSameHash()
    {
        // Arrange
        var first = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2026/")
            .Take(10)
            .Build();
        var second = BlobQueries.Query()
            .InContainer("reports")
            .WithPrefix("2026/")
            .Take(20)
            .ContinueWith("opaque")
            .Build();

        // Act
        var firstHash = BlobQueryHash.Compute(first);
        var secondHash = BlobQueryHash.Compute(second);

        // Assert
        firstHash.ShouldBe(secondHash);
    }

    [Fact]
    public void BlobQueryBuilder_WithInvalidLocalArguments_Throws()
    {
        // Arrange
        var blankContainer = () => BlobQueries.Query().InContainer(" ");
        var nullPrefix = () => BlobQueries.Query().WithPrefix(null);
        var invalidTake = () => BlobQueries.Query().Take(0);
        var blankToken = () => BlobQueries.Query().ContinueWith(" ");

        // Act & Assert
        blankContainer.ShouldThrow<ArgumentException>();
        nullPrefix.ShouldThrow<ArgumentNullException>();
        invalidTake.ShouldThrow<ArgumentOutOfRangeException>();
        blankToken.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void BlobQueryBuilder_WithFullScanApproval_DoesNotBypassValidatorRules()
    {
        // Arrange
        var query = BlobQueries.Query()
            .InContainer("reports")
            .AllowFullScan()
            .Build();

        // Act
        var result = BlobQueryValidator.NormalizeAndValidate(
            "provider",
            query,
            new BlobStoreOptions { AllowFullScans = false },
            this.capabilities);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.HasError<BlobStoreQueryTooBroadError>().ShouldBeTrue();
    }

    private sealed class UnknownLengthReadStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
