// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

[UnitTest("Application.Storage")]
public class UploadConcurrencyBlobStoreClientBehaviorOptionsTests
{
    [Fact]
    public void WithDefaults_MatchesSpecification()
    {
        // Arrange & Act
        var sut = new UploadConcurrencyBlobStoreClientBehaviorOptions();

        // Assert
        sut.MaxConcurrentUploads.ShouldBe(4);
        sut.MaxQueuedUploads.ShouldBe(16);
        sut.QueueWaitTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        sut.Validate().IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithInvalidMaxConcurrentUploads_FailsValidation(int value)
    {
        var sut = new UploadConcurrencyBlobStoreClientBehaviorOptions
        {
            MaxConcurrentUploads = value
        };

        sut.Validate().HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Fact]
    public void WithNegativeMaxQueuedUploads_FailsValidation()
    {
        var sut = new UploadConcurrencyBlobStoreClientBehaviorOptions
        {
            MaxQueuedUploads = -1
        };

        sut.Validate().HasError<BlobStoreValidationError>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithInvalidQueueWaitTimeout_FailsValidation(int seconds)
    {
        var sut = new UploadConcurrencyBlobStoreClientBehaviorOptions
        {
            QueueWaitTimeout = TimeSpan.FromSeconds(seconds)
        };

        sut.Validate().HasError<BlobStoreValidationError>().ShouldBeTrue();
    }
}
