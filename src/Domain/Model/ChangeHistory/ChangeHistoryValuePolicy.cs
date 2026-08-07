// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Defines how captured values for a property should be stored.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Customer&gt;().Redact(c =&gt; c.Email);
/// </code>
/// </example>
public enum ChangeHistoryValuePolicy
{
    /// <summary>
    /// Store serialized old and new values.
    /// </summary>
    Include,

    /// <summary>
    /// Do not store a row for the property.
    /// </summary>
    Exclude,

    /// <summary>
    /// Store a row but replace serialized values with a redaction marker.
    /// </summary>
    Redact,

    /// <summary>
    /// Store only value hashes.
    /// </summary>
    HashOnly
}
