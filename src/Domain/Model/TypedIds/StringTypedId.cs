// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Represents string typed id.
/// </summary>
/// <typeparam name="StringTypedId">The string typed id type.</typeparam>
/// <param name="value">The value used by the operation.</param>
public abstract class StringTypedId(string value) : IEquatable<StringTypedId>, IComparable<StringTypedId>
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Executes the implicit operator string operation.
    /// </summary>
    /// <param name="typedId">The typed id used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static implicit operator string(StringTypedId typedId)
    {
        return typedId.Value;
    }

    /// <summary>
    /// Executes the operator == operation.
    /// </summary>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(StringTypedId left, StringTypedId right)
    {
        if (Equals(left, null))
        {
            return Equals(right, null);
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Executes the operator != operation.
    /// </summary>
    /// <param name="left">The left used by the operation.</param>
    /// <param name="right">The right used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(StringTypedId left, StringTypedId right)
    {
        return !(left == right);
    }

    //public static TypedId For(string value) => new TypedId(value);

    /// <inheritdoc/>
    public override bool Equals(object other)
    {
        if (other is null)
        {
            return false;
        }

        return other is StringTypedId obj && this.Equals(obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return this.Value.GetHashCode();
    }

    /// <summary>
    /// Executes the equals operation.
    /// </summary>
    /// <param name="other">The other used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool Equals(StringTypedId other)
    {
        return this.Value == other?.Value;
    }

    /// <summary>
    /// Executes the compare to operation.
    /// </summary>
    /// <param name="other">The other used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public int CompareTo(StringTypedId other)
    {
        return this.Value.CompareTo(other.Value);
    }

    /// <summary>
    /// Determines whether is empty.
    /// </summary>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool IsEmpty()
    {
        return this.Value?.Length == 0;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Value;
    }
}
