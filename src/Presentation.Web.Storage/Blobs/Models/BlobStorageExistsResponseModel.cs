// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents the result of a blob existence check.
/// </summary>
/// <example>
/// <code>
/// var response = new BlobStorageExistsResponseModel { Container = "reports", Name = "a.pdf", Exists = true };
/// </code>
/// </example>
public class BlobStorageExistsResponseModel
{
    /// <summary>
    /// Gets or sets the blob container.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = response.Container;
    /// </code>
    /// </example>
    public string Container { get; set; }

    /// <summary>
    /// Gets or sets the blob name.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = response.Name;
    /// </code>
    /// </example>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the blob exists.
    /// </summary>
    /// <example>
    /// <code>
    /// var exists = response.Exists;
    /// </code>
    /// </example>
    public bool Exists { get; set; }
}
