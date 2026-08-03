// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Selects the truncation direction used when a segment-shortening strategy still exceeds its character budget.
/// </summary>
/// <example>
/// <code>
/// var options = new PathShorteningOptions
/// {
///     OverflowTruncation = PathShorteningOverflowTruncation.Right
/// };
/// </code>
/// </example>
public enum ShorteningOverflowTruncation
{
    /// <summary>
    /// Removes characters from the left and preserves the terminal value, such as a filename or final identifier.
    /// </summary>
    Left,

    /// <summary>
    /// Removes characters from the right and preserves the initial value.
    /// </summary>
    Right
}
