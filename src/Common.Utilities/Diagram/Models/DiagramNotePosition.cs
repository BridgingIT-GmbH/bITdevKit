// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the placement of a note relative to its target node.
/// </summary>
public enum DiagramNotePosition
{
    /// <summary>
    /// Represents a note rendered to the right of the target.
    /// </summary>
    Right,

    /// <summary>
    /// Represents a note rendered to the left of the target.
    /// </summary>
    Left,

    /// <summary>
    /// Represents a note rendered above the target.
    /// </summary>
    Above,

    /// <summary>
    /// Represents a note rendered below the target.
    /// </summary>
    Below,
}