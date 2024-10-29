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

    public static ResultChooseOption<T> Some(T value) => new(value, true);
    public static ResultChooseOption<T> None() => new(default, false);

    public bool HasValue => this.hasValue;
    public T Value => this.hasValue ? this.value : throw new InvalidOperationException("Option has no value");

    public bool TryGetValue(out T result)
    {
        result = this.value;
        return this.hasValue;
    }
}