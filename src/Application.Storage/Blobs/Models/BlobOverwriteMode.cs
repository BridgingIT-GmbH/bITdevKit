// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines how uploads behave when a target blob already exists.
/// </summary>
/// <example>
/// <code>
/// var mode = BlobOverwriteMode.FailIfExists;
/// </code>
/// </example>
public enum BlobOverwriteMode
{
    /// <summary>
    /// Creates or replaces the target blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var upload = new BlobUpload { OverwriteMode = BlobOverwriteMode.Overwrite };
    /// </code>
    /// </example>
    Overwrite,

    /// <summary>
    /// Fails the upload when the target blob already exists.
    /// </summary>
    /// <example>
    /// <code>
    /// var upload = new BlobUpload { OverwriteMode = BlobOverwriteMode.FailIfExists };
    /// </code>
    /// </example>
    FailIfExists
}
