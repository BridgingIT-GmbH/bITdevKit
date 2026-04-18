// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents a paged file listing response from a file storage provider.
/// </summary>
public class FileStorageFilesResponseModel
{
    /// <summary>
    /// Gets or sets the file paths returned in the current page.
    /// </summary>
    public IEnumerable<string> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets the opaque continuation token for the next page, if any.
    /// </summary>
    public string NextContinuationToken { get; set; }
}
