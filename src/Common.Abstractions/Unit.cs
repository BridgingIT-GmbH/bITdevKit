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

    /// <summary>
    /// Determines whether the specified <see cref="Unit"/> value is equal to this instance.
    /// </summary>
    /// <param name="other">The other <see cref="Unit"/> value.</param>
    /// <returns>Always <c>true</c>, because all <see cref="Unit"/> values are equivalent.</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines whether the specified object is a <see cref="Unit"/> value.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns><c>true</c> when <paramref name="obj"/> is a <see cref="Unit"/> value; otherwise, <c>false</c>.</returns>
    public override bool Equals(object obj) => obj is Unit;

    /// <summary>
    /// Returns the hash code for this value.
    /// </summary>
    /// <returns>Always <c>0</c>, because all <see cref="Unit"/> values are equivalent.</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns the string representation of this value.
    /// </summary>
    /// <returns>An empty string.</returns>
    public override string ToString() => string.Empty;

    /// <summary>
    /// Determines whether two <see cref="Unit"/> values are equal.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>Always <c>true</c>, because all <see cref="Unit"/> values are equivalent.</returns>
    public static bool operator ==(Unit left, Unit right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="Unit"/> values are not equal.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>Always <c>false</c>, because all <see cref="Unit"/> values are equivalent.</returns>
    public static bool operator !=(Unit left, Unit right)
    {
        return !(left == right);
    }
}
