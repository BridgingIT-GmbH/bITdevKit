// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.EventSourcing.Model;
using Domain.EventSourcing.Store;

/// <summary>
/// Represents aggregate type selector.
/// </summary>
public class AggregateTypeSelector : IAggregateTypeSelector
{
    private Type[] typeCache; // TODO: perf

    /// <summary>
    /// Finds .
    /// </summary>
    /// <param name="typeName">The type name used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public Type Find(string typeName)
    {
        if (this.typeCache is null)
        {
            var type = typeof(EventSourcingAggregateRoot);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            this.typeCache = assemblies.SelectMany(a => a.GetTypes()
                    .Where(t => type.IsAssignableFrom(t) && !t.IsInterface))
                .ToArray();
        }

        return this.typeCache.First(t => t.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase));
    }
}
