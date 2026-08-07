// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines how serialized ChangeHistory values exceeding the configured length are handled.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;().UseOversizedValuePolicy(ChangeHistoryOversizedValuePolicy.HashOnly, 4000);
/// </code>
/// </example>
public enum ChangeHistoryOversizedValuePolicy
{
    /// <summary>
    /// Keep the full serialized value.
    /// </summary>
    Include,

    /// <summary>
    /// Truncate the serialized value to the configured length.
    /// </summary>
    Truncate,

    /// <summary>
    /// Do not store the serialized value; store only its hash.
    /// </summary>
    HashOnly,

    /// <summary>
    /// Reject the capture when the serialized value exceeds the configured length.
    /// </summary>
    Reject
}
