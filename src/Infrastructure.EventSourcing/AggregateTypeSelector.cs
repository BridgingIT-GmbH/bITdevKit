// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using System;
using System.Linq;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Store;

public class AggregateTypeSelector : IAggregateTypeSelector
{
    private Type[] typeCache; // TODO: perf

    public Type Find(string typeName)
    {
        if (this.typeCache is null)
        {
            var type = typeof(EventSourcingAggregateRoot);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            this.typeCache = assemblies.SelectMany(a => a.GetTypes()
                .Where(t => type.IsAssignableFrom(t) && !t.IsInterface)).ToArray();
        }

        return this.typeCache.First(t => t.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase));
    }
}