// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the visibility of a class-diagram member.
/// </summary>
public enum DiagramVisibility
{
    /// <summary>
    /// Represents a public member.
    /// </summary>
    Public,

    /// <summary>
    /// Represents a protected member.
    /// </summary>
    Protected,

    /// <summary>
    /// Represents an internal or package member.
    /// </summary>
    Internal,

    /// <summary>
    /// Represents a private member.
    /// </summary>
    Private,
}