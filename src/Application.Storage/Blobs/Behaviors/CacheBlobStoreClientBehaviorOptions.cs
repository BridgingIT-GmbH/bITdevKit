// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures read-through caching for <see cref="CacheBlobStoreClientBehavior" />.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithCacheBehavior(options => options.MaxCachedBlobSize = ByteSize.Megabytes(10));
/// </code>
/// </example>
public sealed class CacheBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the sliding-expiration window applied to cached blob downloads.
    /// </summary>
    /// <example>
    /// <code>
    /// options.SlidingExpiration = TimeSpan.FromMinutes(10);
    /// </code>
    /// </example>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the absolute expiration timestamp applied to cached blob downloads.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1);
    /// </code>
    /// </example>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the maximum blob length, in bytes, that may be buffered into the cache.
    /// </summary>
    /// <example>
    /// <code>
    /// options.MaxCachedBlobSize = ByteSize.Megabytes(10);
    /// </code>
    /// </example>
    public long MaxCachedBlobSize { get; set; } = ByteSize.Megabytes(10);

    /// <summary>
    /// Gets or sets the buffer size used when copying downloadable content into the cache.
    /// </summary>
    /// <example>
    /// <code>
    /// options.BufferSize = 81920;
    /// </code>
    /// </example>
    public int BufferSize { get; set; } = 81920;

    /// <summary>
    /// Validates the configured cache options.
    /// </summary>
    /// <returns>A successful result when options are valid; otherwise a validation failure.</returns>
    /// <example>
    /// <code>
    /// var result = options.Validate();
    /// </code>
    /// </example>
    public Result Validate()
    {
        if (this.MaxCachedBlobSize < 0)
        {
            return Result.Failure(new BlobStoreValidationError("Maximum cached blob size must be greater than or equal to zero."));
        }

        if (this.BufferSize <= 0)
        {
            return Result.Failure(new BlobStoreValidationError("Cache copy buffer size must be greater than zero."));
        }

        return Result.Success();
    }
}
