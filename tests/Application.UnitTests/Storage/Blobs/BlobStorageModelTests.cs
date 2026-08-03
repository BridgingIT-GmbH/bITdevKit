// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;
using System.Reflection;

[UnitTest("Application")]
public class BlobStorageModelTests
{
    [Fact]
    public void BlobKey_WithContainerAndName_StoresValues()
    {
        // Arrange & Act
        var sut = new BlobKey("reports", "2026/06/report.pdf");

        // Assert
        sut.Container.ShouldBe("reports");
        sut.Name.ShouldBe("2026/06/report.pdf");
    }

    [Fact]
    public void BlobKey_WithPathLikeSeparators_StoresNameWithoutPathNormalization()
    {
        // Arrange & Act
        var sut = new BlobKey("attachments", "orders/10001/invoice.pdf");

        // Assert
        sut.Name.ShouldBe("orders/10001/invoice.pdf");
    }

    [Fact]
    public void BlobKeyValidator_WithValidKey_ReturnsSuccess()
    {
        // Arrange
        var key = new BlobKey("reports", "2026/06/report.pdf");

        // Act
        var result = BlobKeyValidator.Validate(key);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "2026/06/report.pdf", "container")]
    [InlineData("", "2026/06/report.pdf", "container")]
    [InlineData("reports", null, "name")]
    [InlineData("reports", "", "name")]
    public void BlobKeyValidator_WithMissingRequiredValues_ReturnsValidationError(
        string container,
        string name,
        string expectedMessagePart)
    {
        // Arrange
        var key = new BlobKey(container, name);

        // Act
        var result = BlobKeyValidator.Validate(key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.GetError<BlobStoreValidationError>().Message.ShouldContain(expectedMessagePart);
    }

    [Fact]
    public void BlobKeyValidator_WithNullKey_ReturnsValidationError()
    {
        // Arrange & Act
        var result = BlobKeyValidator.Validate(null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.GetError<BlobStoreValidationError>().Message.ShouldContain("key");
    }

    [Fact]
    public void BlobInfo_WithDefaults_HasEmptyProperties()
    {
        // Arrange & Act
        var sut = new BlobInfo
        {
            Key = new BlobKey("reports", "2026/06/report.pdf"),
            Length = 42,
            ContentType = ContentType.PDF
        };

        // Assert
        sut.Key.ShouldBe(new BlobKey("reports", "2026/06/report.pdf"));
        sut.Length.ShouldBe(42);
        sut.ContentType.ShouldBe(ContentType.PDF);
        sut.ExpiresAt.ShouldBeNull();
        sut.Properties.ShouldNotBeNull();
        sut.Properties.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(typeof(BlobInfo), nameof(BlobInfo.ContentType))]
    [InlineData(typeof(BlobUpload), nameof(BlobUpload.ContentType))]
    [InlineData(typeof(BlobPropertiesUpdate), nameof(BlobPropertiesUpdate.ContentType))]
    public void ContentTypeProperties_WithPublicModelShape_UseNullableContentType(Type modelType, string propertyName)
    {
        // Arrange & Act
        var property = modelType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        // Assert
        property.ShouldNotBeNull();
        property.PropertyType.ShouldBe(typeof(ContentType?));
    }

    [Fact]
    public void PublicBlobModels_WithProviderNeutralShape_DoNotExposeProviderSpecificTypes()
    {
        // Arrange
        var modelTypes = new[]
        {
            typeof(BlobKey),
            typeof(BlobInfo),
            typeof(BlobDownload),
            typeof(BlobUpload),
            typeof(BlobPropertiesUpdate),
            typeof(BlobQuery),
            typeof(BlobPage),
            typeof(BlobStoreOptions),
            typeof(BlobStoreProviderCapabilities)
        };
        var providerNamespacePrefixes = new[] { "Azure.", "Microsoft.EntityFrameworkCore" };

        // Act
        var exposedTypes = modelTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType)
            .ToList();
        var providerSpecificTypes = exposedTypes
            .Where(type => providerNamespacePrefixes.Any(prefix =>
                type.FullName is not null &&
                type.FullName.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        // Assert
        providerSpecificTypes.ShouldBeEmpty();
    }

    [Fact]
    public void BlobUpload_WithDefaults_UsesOverwriteAndEmptyProperties()
    {
        // Arrange
        using var content = new MemoryStream([1, 2, 3]);

        // Act
        var sut = new BlobUpload
        {
            Key = new BlobKey("reports", "2026/06/report.pdf"),
            Content = content
        };

        // Assert
        sut.OverwriteMode.ShouldBe(BlobOverwriteMode.Overwrite);
        sut.Properties.ShouldNotBeNull();
        sut.Properties.Count.ShouldBe(0);
        typeof(IDisposable).IsAssignableFrom(typeof(BlobUpload)).ShouldBeFalse();
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(BlobUpload)).ShouldBeFalse();
        content.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task BlobDownload_DisposeAsync_DisposesReturnedContentStream()
    {
        // Arrange
        var stream = new TrackingStream();
        var sut = new BlobDownload
        {
            Content = stream,
            Info = new BlobInfo
            {
                Key = new BlobKey("reports", "2026/06/report.pdf")
            }
        };

        // Act
        await sut.DisposeAsync();

        // Assert
        stream.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void BlobPage_WithContinuationToken_ReportsHasMore()
    {
        // Arrange & Act
        var sut = new BlobPage
        {
            Items =
            [
                new BlobInfo
                {
                    Key = new BlobKey("reports", "2026/06/report.pdf")
                }
            ],
            ContinuationToken = "opaque"
        };

        // Assert
        sut.Items.Count.ShouldBe(1);
        sut.HasMore.ShouldBeTrue();
    }

    [Fact]
    public void BlobPage_WithoutContinuationToken_ReportsNoMoreResults()
    {
        // Arrange & Act
        var sut = new BlobPage();

        // Assert
        sut.Items.ShouldNotBeNull();
        sut.Items.Count.ShouldBe(0);
        sut.HasMore.ShouldBeFalse();
    }

    [Fact]
    public void BlobStoreOptions_WithDefaults_MatchesSpecification()
    {
        // Arrange & Act
        var sut = new BlobStoreOptions();

        // Assert
        sut.DefaultTake.ShouldBe(100);
        sut.MaxTake.ShouldBe(1000);
        sut.MaxBlobSize.ShouldBeNull();
        sut.AllowFullScans.ShouldBeFalse();
        sut.RequireExplicitFullScanApproval.ShouldBeTrue();
        sut.ChunkSize.ShouldBe((int)ByteSize.Megabytes(4));
        sut.LeaseDuration.ShouldBe(TimeSpan.FromMinutes(1));
        sut.LeaseOwner.ShouldBeNull();
    }

    [Fact]
    public void BlobStorageOptions_WithDefaults_EnablesRetentionSweeperOptions()
    {
        // Arrange & Act
        var sut = new BlobStorageOptions();

        // Assert
        sut.Retention.Enabled.ShouldBeTrue();
        sut.Retention.BatchSize.ShouldBe(1000);
        sut.Retention.MaxBatchesPerStore.ShouldBe(10);
        sut.Retention.SweepInterval.ShouldBe(TimeSpan.FromHours(1));
        sut.Retention.Validate().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void BlobStoreProviderCapabilities_WithDefaults_DisablesCapabilities()
    {
        // Arrange & Act
        var sut = new BlobStoreProviderCapabilities();

        // Assert
        sut.SupportsContinuationPaging.ShouldBeFalse();
        sut.SupportsPrefixListing.ShouldBeFalse();
        sut.SupportsFullContainerScan.ShouldBeFalse();
        sut.SupportsProperties.ShouldBeFalse();
        sut.SupportsContentType.ShouldBeFalse();
        sut.SupportsETag.ShouldBeFalse();
        sut.SupportsContentHash.ShouldBeFalse();
        sut.SupportsNativeLeases.ShouldBeFalse();
        sut.SupportsInternalLeases.ShouldBeFalse();
        sut.SupportsConditionalPropertiesUpdate.ShouldBeFalse();
        sut.SupportsStreamingUpload.ShouldBeFalse();
        sut.SupportsStreamingDownload.ShouldBeFalse();
        sut.SupportsExpiration.ShouldBeFalse();
        sut.SupportsRetentionSweep.ShouldBeFalse();
        sut.SupportsNativeRetention.ShouldBeFalse();
    }

    [Fact]
    public void BlobStoreNotFoundError_WithKey_ExposesKeyAndMessage()
    {
        // Arrange
        var key = new BlobKey("reports", "missing.pdf");

        // Act
        var sut = new BlobStoreNotFoundError(key);

        // Assert
        sut.Key.ShouldBe(key);
        sut.Message.ShouldContain("reports");
        sut.Message.ShouldContain("missing.pdf");
    }

    [Fact]
    public void BlobStorePageSizeExceededError_WithSizes_ExposesRequestedAndMaximumValues()
    {
        // Arrange & Act
        var sut = new BlobStorePageSizeExceededError(1001, 1000);

        // Assert
        sut.Take.ShouldBe(1001);
        sut.MaxTake.ShouldBe(1000);
        sut.Message.ShouldContain("1001");
        sut.Message.ShouldContain("1000");
    }

    [Fact]
    public void BlobStoreSizeLimitExceededError_WithSizes_ExposesActualAndMaximumValues()
    {
        // Arrange & Act
        var sut = new BlobStoreSizeLimitExceededError(1025, 1024);

        // Assert
        sut.ActualSize.ShouldBe(1025);
        sut.MaxSize.ShouldBe(1024);
        sut.Message.ShouldContain("1025");
        sut.Message.ShouldContain("1024");
    }

    [Fact]
    public void BlobStoreTimeoutError_WithOperationAndTimeout_ExposesValues()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var sut = new BlobStoreTimeoutError("upload", timeout);

        // Assert
        sut.Operation.ShouldBe("upload");
        sut.Timeout.ShouldBe(timeout);
        sut.Message.ShouldContain("upload");
        sut.Message.ShouldContain(timeout.ToString());
    }

    private sealed class TrackingStream : MemoryStream
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            this.IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
