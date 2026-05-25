// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a reusable diagram node.
/// </summary>
/// <param name="Id">The stable node identifier.</param>
/// <param name="Label">The optional node label.</param>
/// <param name="Kind">The node kind.</param>
/// <param name="Stereotype">The optional node stereotype.</param>
/// <param name="Members">The optional node members.</param>
public sealed record DiagramNode(
    string Id,
    string Label = null,
    DiagramNodeKind Kind = DiagramNodeKind.Normal,
    string Stereotype = null,
    IReadOnlyList<DiagramNodeMember> Members = null);