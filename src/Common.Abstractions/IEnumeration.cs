// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an enumeration value identified by an integer and described by a string.
/// </summary>
public interface IEnumeration : IEnumeration<int, string>;

/// <summary>
/// Represents an enumeration value identified by an integer.
/// </summary>
/// <typeparam name="TValue">The type of descriptive value exposed to consumers.</typeparam>
public interface IEnumeration<out TValue> : IEnumeration<int, TValue>;

/// <summary>
/// Defines the identifier and descriptive value contract shared by enumeration objects.
/// </summary>
/// <typeparam name="TId">The comparable type used to identify an enumeration member.</typeparam>
/// <typeparam name="TValue">The type of descriptive value exposed to consumers.</typeparam>
public interface IEnumeration<out TId, out TValue> : IComparable, IEquatable<Enumeration>
    where TId : IComparable
{
    /// <summary>Gets the stable identifier of the enumeration member.</summary>
    TId Id { get; }

    /// <summary>Gets the descriptive value associated with the enumeration member.</summary>
    TValue Value { get; }
}
