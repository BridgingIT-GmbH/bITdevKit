// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents a file checksum response.
/// </summary>
public class FileStorageChecksumResponseModel
{
    /// <summary>
    /// Gets or sets the queried file path.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the computed checksum value.
    /// </summary>
    public string Checksum { get; set; }
}
