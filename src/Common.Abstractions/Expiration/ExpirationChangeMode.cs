// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes how an existing expiration timestamp should change.
/// </summary>
/// <example>
/// <code>
/// var mode = ExpirationChangeMode.Set;
/// </code>
/// </example>
public enum ExpirationChangeMode
{
    /// <summary>Preserves the current expiration timestamp.</summary>
    Preserve,

    /// <summary>Sets an absolute expiration timestamp.</summary>
    Set,

    /// <summary>Sets an expiration relative to the operation time.</summary>
    After,

    /// <summary>Clears the current expiration timestamp.</summary>
    Clear
}
