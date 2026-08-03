// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Identifies a blob by logical container and name.
/// </summary>
/// <example>
/// <code>
/// var key = new BlobKey("reports", "2026/06/report.pdf");
/// </code>
/// </example>
public sealed record BlobKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlobKey" /> class.
    /// </summary>
    /// <param name="container">The logical top-level blob container.</param>
    /// <param name="name">The path-like blob name inside the container.</param>
    /// <example>
    /// <code>
    /// var key = new BlobKey("exports", "customer/42/export.csv");
    /// </code>
    /// </example>
    public BlobKey(string container, string name)
    {
        this.Container = container;
        this.Name = name;
    }

    /// <summary>
    /// Gets the logical top-level blob container.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = key.Container;
    /// </code>
    /// </example>
    public string Container { get; init; }

    /// <summary>
    /// Gets the path-like blob name inside the container.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = key.Name;
    /// </code>
    /// </example>
    public string Name { get; init; }
}
