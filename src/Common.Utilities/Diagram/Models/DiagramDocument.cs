// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a reusable diagram document.
/// </summary>
/// <param name="Kind">The diagram kind.</param>
/// <param name="Nodes">The diagram nodes.</param>
/// <param name="Edges">The diagram edges.</param>
/// <param name="Notes">The diagram notes.</param>
/// <param name="Groups">The diagram groups.</param>
/// <param name="Direction">The preferred diagram direction.</param>
public sealed record DiagramDocument(
    DiagramKind Kind,
    IReadOnlyList<DiagramNode> Nodes,
    IReadOnlyList<DiagramEdge> Edges,
    IReadOnlyList<DiagramNote> Notes,
    IReadOnlyList<DiagramGroup> Groups,
    DiagramDirection Direction = DiagramDirection.TopToBottom);