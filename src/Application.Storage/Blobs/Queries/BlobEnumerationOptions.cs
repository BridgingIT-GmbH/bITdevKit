// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures provider-neutral blob enumeration helpers.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobEnumerationOptions
/// {
///     MaxItems = 500
/// };
/// </code>
/// </example>
public sealed class BlobEnumerationOptions
{
    /// <summary>
    /// Gets the maximum number of items to enumerate when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var maxItems = options.MaxItems;
    /// </code>
    /// </example>
    public int? MaxItems { get; init; }
}
