// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the supported high-level diagram categories.
/// </summary>
public enum DiagramKind
{
    /// <summary>
    /// Represents a state diagram.
    /// </summary>
    State,

    /// <summary>
    /// Represents a flow diagram.
    /// </summary>
    Flow,

    /// <summary>
    /// Represents an activity diagram.
    /// </summary>
    Activity,

    /// <summary>
    /// Represents a sequence diagram.
    /// </summary>
    Sequence,

    /// <summary>
    /// Represents a class diagram.
    /// </summary>
    Class,

    /// <summary>
    /// Represents a component diagram.
    /// </summary>
    Component,
}