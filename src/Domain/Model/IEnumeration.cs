// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

public interface IEnumeration : IEnumeration<int, string> { }

public interface IEnumeration<out TValue> : IEnumeration<int, TValue> { }

public interface IEnumeration<out TId, out TValue> : IComparable, IEquatable<Enumeration>
    where TId : IComparable
{
    TId Id { get; }

    TValue Value { get; }
}