// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>Identifies DevKit infrastructure entity types that are hidden by default, matched by full type name.</summary>
internal static class EntitiesDashboardDenyList
{
    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal)
    {
        "BridgingIT.DevKit.Domain.Outbox.OutboxMessage",
        "BridgingIT.DevKit.Infrastructure.EventSourcing.EventStoreAggregateEvent",
        "BridgingIT.DevKit.Infrastructure.EventSourcing.EventStoreSnapshot",
        "BridgingIT.DevKit.Infrastructure.EntityFramework.ChangeHistoryEntry",
        "BridgingIT.DevKit.Application.Storage.DocumentEntity"
    };

    private static readonly string[] NamespacePrefixes =
    [
        "BridgingIT.DevKit.Domain.ChangeHistory.",
        "BridgingIT.DevKit.Application.Auditing.",
        "BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing.",
        "BridgingIT.DevKit.Application.Storage."
    ];

    /// <summary>Determines whether the type is a DevKit infrastructure entity that the dashboard hides by default.</summary>
    /// <param name="type">The entity type.</param>
    /// <returns><c>true</c> when the type is deny-listed.</returns>
    public static bool IsDenied(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var fullName = type.FullName;
        if (fullName == null)
        {
            return false;
        }

        return TypeNames.Contains(fullName)
            || NamespacePrefixes.Any(prefix => fullName.StartsWith(prefix, StringComparison.Ordinal));
    }
}
