// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a reusable diagram group.
/// </summary>
/// <param name="Id">The stable group identifier.</param>
/// <param name="Label">The optional group label.</param>
/// <param name="Kind">The group kind.</param>
/// <param name="NodeIds">The node identifiers contained by the group.</param>
public sealed record DiagramGroup(
    string Id,
    string Label,
    DiagramGroupKind Kind,
    IReadOnlyList<string> NodeIds);