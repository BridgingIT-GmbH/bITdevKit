// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a reusable diagram edge.
/// </summary>
/// <param name="From">The source node identifier.</param>
/// <param name="To">The target node identifier.</param>
/// <param name="Label">The optional edge label.</param>
/// <param name="Kind">The edge kind.</param>
public sealed record DiagramEdge(
    string From,
    string To,
    string Label = null,
    DiagramEdgeKind Kind = DiagramEdgeKind.Normal);