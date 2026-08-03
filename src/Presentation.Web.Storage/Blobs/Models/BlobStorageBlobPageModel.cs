// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents a page of blob metadata returned from maintenance endpoints.
/// </summary>
/// <example>
/// <code>
/// var page = new BlobStorageBlobPageModel { Items = [] };
/// </code>
/// </example>
public class BlobStorageBlobPageModel
{
    /// <summary>
    /// Gets or sets the blob metadata items in the page.
    /// </summary>
    /// <example>
    /// <code>
    /// var count = page.Items.Length;
    /// </code>
    /// </example>
    public BlobStorageBlobInfoModel[] Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the opaque continuation token for the next page.
    /// </summary>
    /// <example>
    /// <code>
    /// var token = page.ContinuationToken;
    /// </code>
    /// </example>
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether another page is available.
    /// </summary>
    /// <example>
    /// <code>
    /// if (page.HasMore) { }
    /// </code>
    /// </example>
    public bool HasMore { get; set; }
}
