// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;

public interface IEnumeration : IEnumeration<int, string>
{
}

public interface IEnumeration<TValue> : IEnumeration<int, TValue>
    where TValue : IComparable
{
}

public interface IEnumeration<TId, TValue> : IComparable
    where TId : IComparable
    where TValue : IComparable
{
    TId Id { get; }

    TValue Value { get; }
}