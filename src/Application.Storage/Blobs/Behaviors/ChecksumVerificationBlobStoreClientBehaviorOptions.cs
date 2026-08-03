// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures automatic checksum verification for blob downloads.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithChecksumVerificationBehavior(options => options.AllowMissingContentHash = true);
/// </code>
/// </example>
public sealed class ChecksumVerificationBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether downloads without <see cref="BlobInfo.ContentHash" /> are allowed.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AllowMissingContentHash = true;
    /// </code>
    /// </example>
    public bool AllowMissingContentHash { get; set; }

    /// <summary>
    /// Gets or sets the buffer size used while copying and hashing downloaded content.
    /// </summary>
    /// <example>
    /// <code>
    /// options.BufferSize = (int)ByteSize.Kilobytes(128);
    /// </code>
    /// </example>
    public int BufferSize { get; set; } = 81920;

    /// <summary>
    /// Validates the checksum verification behavior options.
    /// </summary>
    /// <returns>A success result when the options are valid; otherwise a validation failure.</returns>
    /// <example>
    /// <code>
    /// var result = options.Validate();
    /// </code>
    /// </example>
    public Result Validate()
    {
        return this.BufferSize > 0
            ? Result.Success()
            : Result.Failure(new BlobStoreValidationError("Checksum verification buffer size must be greater than zero."));
    }
}
