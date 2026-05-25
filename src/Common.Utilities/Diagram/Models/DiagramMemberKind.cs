// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the semantic role of a class-diagram member.
/// </summary>
public enum DiagramMemberKind
{
    /// <summary>
    /// Represents a field member.
    /// </summary>
    Field,

    /// <summary>
    /// Represents a property member.
    /// </summary>
    Property,

    /// <summary>
    /// Represents a method member.
    /// </summary>
    Method,
}