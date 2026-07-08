// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Marks the document-storage feature as active in the current service collection.
/// </summary>
/// <example>
/// <code>
/// var feature = services.GetService&lt;DocumentStorageFeature&gt;();
/// if (feature?.IsEnabled == true)
/// {
///     // Document storage is active.
/// }
/// </code>
/// </example>
public sealed class DocumentStorageFeature
{
    /// <summary>
    /// Gets a value indicating whether document storage is enabled.
    /// </summary>
    /// <example>
    /// <code>
    /// if (feature.IsEnabled)
    /// {
    ///     // Render document-storage dashboard pages.
    /// }
    /// </code>
    /// </example>
    public bool IsEnabled { get; init; } = true;
}
