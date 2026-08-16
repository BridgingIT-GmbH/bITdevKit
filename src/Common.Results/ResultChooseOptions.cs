// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents an optional value, similar to Nullable but for any type.
/// </summary>
/// <typeparam name="T">The type of the optional value.</typeparam>
public readonly struct ResultChooseOption<T>
{
    private readonly bool hasValue;
    private readonly T value;

    private ResultChooseOption(T value, bool hasValue)
    {
        this.value = value;
        this.hasValue = hasValue;
    }

    /// <summary>Creates an option that contains a value.</summary>
    /// <param name="value">The value to contain, including <see langword="null"/> when <typeparamref name="T"/> permits it.</param>
    /// <returns>An option whose <see cref="HasValue"/> property is <see langword="true"/>.</returns>
    public static ResultChooseOption<T> Some(T value) => new(value, true);

    /// <summary>Creates an option without a value.</summary>
    /// <returns>An empty option.</returns>
    public static ResultChooseOption<T> None() => new(default, false);

    /// <summary>Gets a value indicating whether this option contains a value.</summary>
    public bool HasValue => this.hasValue;

    /// <summary>Gets the contained value.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="HasValue"/> is <see langword="false"/>.</exception>
    public T Value => this.hasValue ? this.value : throw new InvalidOperationException("Option has no value");

    /// <summary>Attempts to retrieve the contained value without throwing.</summary>
    /// <param name="result">Receives the stored value, or the default value of <typeparamref name="T"/> for an empty option.</param>
    /// <returns><see langword="true"/> when a value is present; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out T result)
    {
        result = this.value;
        return this.hasValue;
    }
}
