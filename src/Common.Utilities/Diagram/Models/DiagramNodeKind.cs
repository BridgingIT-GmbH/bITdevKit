// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the semantic role of a diagram node.
/// </summary>
public enum DiagramNodeKind
{
    /// <summary>
    /// Represents a normal node.
    /// </summary>
    Normal,

    /// <summary>
    /// Represents a generated branch node.
    /// </summary>
    Branch,

    /// <summary>
    /// Represents a generated join node.
    /// </summary>
    Join,

    /// <summary>
    /// Represents a terminal node.
    /// </summary>
    Terminal,

    /// <summary>
    /// Represents a flow or activity start node.
    /// </summary>
    Start,

    /// <summary>
    /// Represents a flow or activity decision node.
    /// </summary>
    Decision,

    /// <summary>
    /// Represents a sequence participant.
    /// </summary>
    Participant,

    /// <summary>
    /// Represents a sequence actor.
    /// </summary>
    Actor,

    /// <summary>
    /// Represents a class node.
    /// </summary>
    Class,

    /// <summary>
    /// Represents an interface node.
    /// </summary>
    Interface,

    /// <summary>
    /// Represents an abstract class node.
    /// </summary>
    AbstractClass,

    /// <summary>
    /// Represents an enumeration node.
    /// </summary>
    Enum,

    /// <summary>
    /// Represents a component node.
    /// </summary>
    Component,

    /// <summary>
    /// Represents a database or datastore node.
    /// </summary>
    Database,
}