// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures bounded, process-local admission for blob uploads.
/// </summary>
/// <example>
/// <code>
/// var options = new UploadConcurrencyBlobStoreClientBehaviorOptions
/// {
///     MaxConcurrentUploads = 4,
///     MaxQueuedUploads = 16,
///     QueueWaitTimeout = TimeSpan.FromSeconds(30)
/// };
/// </code>
/// </example>
public sealed class UploadConcurrencyBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrently active uploads per named store.
    /// </summary>
    /// <example>
    /// <code>
    /// options.MaxConcurrentUploads = 4;
    /// </code>
    /// </example>
    public int MaxConcurrentUploads { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum number of uploads waiting for admission per named store.
    /// </summary>
    /// <example>
    /// <code>
    /// options.MaxQueuedUploads = 16;
    /// </code>
    /// </example>
    public int MaxQueuedUploads { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum time an upload may wait for admission.
    /// </summary>
    /// <example>
    /// <code>
    /// options.QueueWaitTimeout = TimeSpan.FromSeconds(30);
    /// </code>
    /// </example>
    public TimeSpan QueueWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the upload-admission settings.
    /// </summary>
    /// <returns>A successful result when all settings are valid.</returns>
    /// <example>
    /// <code>
    /// var validation = options.Validate();
    /// </code>
    /// </example>
    public Result Validate()
    {
        if (this.MaxConcurrentUploads <= 0)
        {
            return Result.Failure(new BlobStoreValidationError(
                "MaxConcurrentUploads must be greater than zero."));
        }

        if (this.MaxQueuedUploads < 0)
        {
            return Result.Failure(new BlobStoreValidationError(
                "MaxQueuedUploads must be greater than or equal to zero."));
        }

        if (this.QueueWaitTimeout <= TimeSpan.Zero)
        {
            return Result.Failure(new BlobStoreValidationError(
                "QueueWaitTimeout must be greater than zero."));
        }

        return Result.Success();
    }
}
