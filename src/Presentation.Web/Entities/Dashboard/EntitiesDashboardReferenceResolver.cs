// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

/// <summary>Detects and applies references between discovered entities (typed id equality or the <c>&lt;EntityName&gt;Id</c> convention).</summary>
internal static class EntitiesDashboardReferenceResolver
{
    /// <summary>Sets <see cref="EntityPropertyDescriptor.Reference"/> on the properties of the discovered entities.</summary>
    /// <param name="entities">All discovered entities, before they are published by the catalog.</param>
    /// <param name="options">The dashboard options holding the explicit reference configuration.</param>
    public static void Resolve(IReadOnlyList<EntitiesDashboardEntity> entities, EntitiesDashboardOptions options)
    {
        var byKey = entities.ToDictionary(e => e.Key, StringComparer.Ordinal);
        var byName = entities.ToLookup(e => e.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            options.EntityConfigurations.TryGetValue(entity.EntityType, out var configuration);

            foreach (var property in entity.Properties)
            {
                if (property.IsId || property.IsHidden || property.IsAudit || property.IsConcurrency)
                {
                    continue;
                }

                if (configuration != null && configuration.References.TryGetValue(property.Path, out var configured))
                {
                    // None() suppresses detection, To<T>() only links when the target was discovered
                    if (configured != null && byKey.ContainsKey(configured.FullName ?? configured.Name))
                    {
                        property.Reference = new EntitiesDashboardReference(configured.FullName ?? configured.Name);
                    }

                    continue;
                }

                var target = Detect(entity, property, entities, byName);
                if (target != null)
                {
                    property.Reference = new EntitiesDashboardReference(target.Key);
                }
            }
        }
    }

    private static EntitiesDashboardEntity Detect(
        EntitiesDashboardEntity owner,
        EntityPropertyDescriptor property,
        IReadOnlyList<EntitiesDashboardEntity> entities,
        ILookup<string, EntitiesDashboardEntity> byName)
    {
        if (property.EditorKind is not (EntitiesDashboardEditorKind.Guid or EntitiesDashboardEditorKind.Number or EntitiesDashboardEditorKind.Text))
        {
            return null;
        }

        if (property.IsTypedId)
        {
            var matches = entities.Where(e => e.IdType == property.UnderlyingType).ToList();

            return matches.Count == 1 ? matches[0] : null;
        }

        if (property.Name.Length > 2 && property.Name.EndsWith("Id", StringComparison.Ordinal))
        {
            var candidates = byName[property.Name[..^2]].ToList();
            var primitive = EntitiesDashboardIdConverter.GetPrimitiveType(property.ClrType);

            // several targets with the same short name are ambiguous and need explicit configuration
            return candidates.Count == 1 && candidates[0].IdPrimitiveType == primitive
                ? candidates[0]
                : null;
        }

        return null;
    }
}
