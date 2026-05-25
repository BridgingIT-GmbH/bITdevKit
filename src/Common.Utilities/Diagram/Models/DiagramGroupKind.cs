// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the semantic role of a diagram group.
/// </summary>
public enum DiagramGroupKind
{
    /// <summary>
    /// Represents a state-scoped grouping.
    /// </summary>
    State,

    /// <summary>
    /// Represents a branch grouping.
    /// </summary>
    Branch,

    /// <summary>
    /// Represents a parallel grouping.
    /// </summary>
    Parallel,

    /// <summary>
    /// Represents a child-diagram grouping.
    /// </summary>
    ChildDiagram,
}