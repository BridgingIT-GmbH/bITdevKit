// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Specifies the payload compression or packaging mode for DataPorter operations.
/// </summary>
public enum PayloadCompressionKind
{
    /// <summary>
    /// No compression or packaging is applied.
    /// </summary>
    None,

    /// <summary>
    /// Applies gzip compression to a single payload stream.
    /// </summary>
    GZip,

    /// <summary>
    /// Packages the payload into a single-entry ZIP archive.
    /// </summary>
    Zip
}
