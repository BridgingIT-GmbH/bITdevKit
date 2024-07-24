// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;

//[DebuggerDisplay("{Value}")]
//public abstract class AggregateRootId<TId> : ValueObject
//{
//    public abstract TId Value { get; protected set; }

//    public override string ToString()
//    {
//        return this.Value?.ToString();
//    }
//}

[DebuggerDisplay("{Value}")]
public abstract class AggregateRootId<TId> : EntityId<TId>
{
    public override string ToString()
    {
        return this.Value?.ToString();
    }
}