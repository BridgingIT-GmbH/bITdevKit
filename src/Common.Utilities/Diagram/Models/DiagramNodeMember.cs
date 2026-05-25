// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a reusable class-diagram member.
/// </summary>
/// <param name="Name">The member name or signature.</param>
/// <param name="Kind">The member kind.</param>
/// <param name="Type">The optional return or value type.</param>
/// <param name="Visibility">The member visibility.</param>
public sealed record DiagramNodeMember(
    string Name,
    DiagramMemberKind Kind = DiagramMemberKind.Property,
    string Type = null,
    DiagramVisibility Visibility = DiagramVisibility.Public);