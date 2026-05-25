// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the semantic role of a diagram edge.
/// </summary>
public enum DiagramEdgeKind
{
    /// <summary>
    /// Represents a normal edge.
    /// </summary>
    Normal,

    /// <summary>
    /// Represents a signal edge.
    /// </summary>
    Signal,

    /// <summary>
    /// Represents a timeout edge.
    /// </summary>
    Timeout,

    /// <summary>
    /// Represents a branch edge.
    /// </summary>
    Branch,

    /// <summary>
    /// Represents a join edge.
    /// </summary>
    Join,

    /// <summary>
    /// Represents a terminal edge.
    /// </summary>
    Terminal,

    /// <summary>
    /// Represents a sequence message edge.
    /// </summary>
    Message,

    /// <summary>
    /// Represents a sequence reply edge.
    /// </summary>
    Reply,

    /// <summary>
    /// Represents a class association edge.
    /// </summary>
    Association,

    /// <summary>
    /// Represents a class inheritance edge.
    /// </summary>
    Inheritance,

    /// <summary>
    /// Represents a class composition edge.
    /// </summary>
    Composition,

    /// <summary>
    /// Represents a class aggregation edge.
    /// </summary>
    Aggregation,

    /// <summary>
    /// Represents a class dependency edge.
    /// </summary>
    Dependency,

    /// <summary>
    /// Represents a class realization edge.
    /// </summary>
    Realization,
}