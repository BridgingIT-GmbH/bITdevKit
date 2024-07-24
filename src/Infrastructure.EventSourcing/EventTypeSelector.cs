// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using System;
using System.Linq;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Store;

public class EventTypeSelector : IEventTypeSelector
{
    private Type[] typeCache = [];

    public Type FindType(string typename)
    {
        if (this.typeCache.Length == 0)
        {
            var baseType = typeof(IAggregateEvent);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a =>
            {
                var name = a.GetName().Name;
                return name != null &&
                       !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
            });
            this.typeCache = assemblies.SelectMany(a => a.GetTypes()
                .Where(t => baseType.IsAssignableFrom(t) && !t.IsInterface)).ToArray();
        }

        return this.typeCache.First(t => t.FullName == typename);
    }
}