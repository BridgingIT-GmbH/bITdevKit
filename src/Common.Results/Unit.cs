// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a type with no meaningful value, used as a placeholder in scenarios
/// where a result is required but no data needs to be returned (e.g., for commands).
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The single instance of the Unit type.
    /// </summary>
    public static readonly Unit Value = default;

    public bool Equals(Unit other) => true;

    public override bool Equals(object obj) => obj is Unit;

    public override int GetHashCode() => 0;

    public override string ToString() => string.Empty;

    public static bool operator ==(Unit left, Unit right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Unit left, Unit right)
    {
        return !(left == right);
    }
}