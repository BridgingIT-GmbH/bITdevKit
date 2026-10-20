// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Selects the constructor used to deserialize a CBOR object.</summary>
/// <remarks>
/// Only one constructor may be selected. Parameters bind to CLR member names and types,
/// independently of any <c>DataMember</c> wire names. Public and nonpublic constructors are supported.
/// </remarks>
/// <example>
/// <code>
/// public sealed class Customer
/// {
///     [CborConstructor]
///     public Customer(string name) => this.Name = name;
///     public string Name { get; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class CborConstructorAttribute : Attribute { }