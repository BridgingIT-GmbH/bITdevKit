// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;


[DebuggerDisplay("{Value}")]
public abstract class AggregateRootId<TId> : EntityId<TId> // TODO: this is obsolete with the new codegen TypedIds, remove in future (DinnerFiesta depends on it for now)
{
    public override string ToString()
    {
        return this.Value?.ToString();
    }
}