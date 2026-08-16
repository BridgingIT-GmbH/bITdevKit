// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.EventSourcing.Model;
using Domain.EventSourcing.Store;

/// <summary>
/// Represents event type selector.
/// </summary>
public class EventTypeSelector : IEventTypeSelector
{
    private Type[] typeCache = [];

    /// <summary>
    /// Finds type.
    /// </summary>
    /// <param name="typename">The typename used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public Type FindType(string typename)
    {
        if (this.typeCache.Length == 0)
        {
            var baseType = typeof(IAggregateEvent);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    var name = a.GetName().Name;

                    return name != null && !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
                });
            this.typeCache = assemblies.SelectMany(a => a.GetTypes()
                    .Where(t => baseType.IsAssignableFrom(t) && !t.IsInterface))
                .ToArray();
        }

        return this.typeCache.First(t => t.FullName == typename);
    }
}
