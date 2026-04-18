// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents the existence state for a file-system entry path.
/// </summary>
public class FileStorageExistsResponseModel
{
    /// <summary>
    /// Gets or sets the queried path.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the path exists.
    /// </summary>
    public bool Exists { get; set; }
}
