// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Identifies the blob-store operation that can receive injected chaos failures.
/// </summary>
/// <example>
/// <code>
/// var operation = BlobStoreChaosOperation.Upload;
/// </code>
/// </example>
public enum BlobStoreChaosOperation
{
    /// <summary>
    /// The upload operation.
    /// </summary>
    /// <example>
    /// <code>
    /// var operation = BlobStoreChaosOperation.Upload;
    /// </code>
    /// </example>
    Upload,

    /// <summary>
    /// The download operation.
    /// </summary>
    /// <example>
    /// <code>
    /// var operation = BlobStoreChaosOperation.Download;
    /// </code>
    /// </example>
    Download
}
