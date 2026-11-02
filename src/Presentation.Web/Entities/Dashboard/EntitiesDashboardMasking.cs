// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Applies masking and hiding on the server before anything is serialized to the client. Masked values never leave the server,
/// hidden properties are omitted everywhere.
/// </summary>
internal static class EntitiesDashboardMasking
{
    /// <summary>
    /// Projects an entity instance to a dictionary of display values keyed by property name: the id as text, scalars as invariant
    /// values, enumerations by key, value objects as masked JSON text, masked properties as their mask text; hidden and
    /// unsupported properties are omitted.
    /// </summary>
    /// <param name="entity">The entity descriptor.</param>
    /// <param name="instance">The entity instance.</param>
    /// <returns>The projected row.</returns>
    public static IDictionary<string, object> Project(EntitiesDashboardEntity entity, object instance)
    {
        var row = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var property in entity.Properties)
        {
            if (property.IsHidden || property.EditorKind == EntitiesDashboardEditorKind.Unsupported)
            {
                continue;
            }

            row[property.Name] = ProjectValue(property, instance);
        }

        return row;
    }

    /// <summary>Projects one property value for display.</summary>
    /// <param name="property">The descriptor.</param>
    /// <param name="instance">The owning instance.</param>
    /// <returns>The display value, the mask text for masked properties, or null.</returns>
    public static object ProjectValue(EntityPropertyDescriptor property, object instance)
    {
        if (property.IsMasked)
        {
            return property.MaskText;
        }

        var value = property.Getter?.Invoke(instance);
        if (value == null)
        {
            return null;
        }

        return property.EditorKind switch
        {
            EntitiesDashboardEditorKind.Json => MaskJson(property, value),
            EntitiesDashboardEditorKind.Number when !property.IsTypedId => value,
            EntitiesDashboardEditorKind.Boolean => value,
            _ => property.IsId || property.IsTypedId ? EntitiesDashboardIdConverter.Format(value) : EntitiesDashboardValueConverter.Format(value)
        };
    }

    /// <summary>Serializes a value-object (or scalar collection) to JSON with masked members replaced and hidden members removed.</summary>
    /// <param name="property">The parent property descriptor.</param>
    /// <param name="value">The stored value.</param>
    /// <returns>The masked JSON text, or null for null.</returns>
    public static string MaskJson(EntityPropertyDescriptor property, object value)
    {
        if (value == null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(value, value.GetType(), EntitiesDashboardJson.Options);
        if (node is JsonObject json)
        {
            foreach (var member in property.Members.Where(m => m.IsMasked || m.IsHidden))
            {
                var key = FindKey(json, member.Name);
                if (key == null)
                {
                    continue;
                }

                if (member.IsHidden)
                {
                    json.Remove(key);
                }
                else
                {
                    json[key] = member.MaskText;
                }
            }
        }

        return node?.ToJsonString(EntitiesDashboardJson.Options);
    }

    /// <summary>
    /// Merges submitted value-object JSON with the stored value: masked and hidden members are restored from the stored value
    /// unless a replacement was explicitly submitted for the member.
    /// </summary>
    /// <param name="property">The parent property descriptor.</param>
    /// <param name="submittedJson">The JSON submitted by the client.</param>
    /// <param name="stored">The stored value, or null.</param>
    /// <param name="replacements">Explicit member replacements keyed by member path (<c>Parent.Member</c>), or null.</param>
    /// <returns>The merged JSON text; the submitted text when it is not a JSON object.</returns>
    public static string MergeValueObjectJson(
        EntityPropertyDescriptor property,
        string submittedJson,
        object stored,
        IReadOnlyDictionary<string, string> replacements)
    {
        if (string.IsNullOrWhiteSpace(submittedJson))
        {
            return submittedJson;
        }

        JsonNode submitted;
        try
        {
            submitted = JsonNode.Parse(submittedJson, documentOptions: new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (JsonException)
        {
            return submittedJson;
        }

        if (submitted is not JsonObject json)
        {
            return submittedJson;
        }

        var storedJson = stored == null
            ? null
            : JsonSerializer.SerializeToNode(stored, stored.GetType(), EntitiesDashboardJson.Options) as JsonObject;

        foreach (var member in property.Members.Where(m => m.IsMasked || m.IsHidden))
        {
            var key = FindKey(json, member.Name);
            if (key != null)
            {
                json.Remove(key);
            }

            if (member.IsMasked && replacements != null && replacements.TryGetValue(member.Path, out var text))
            {
                json[JsonNamingPolicy.CamelCase.ConvertName(member.Name)] = ParseReplacement(text);

                continue;
            }

            var storedKey = storedJson == null ? null : FindKey(storedJson, member.Name);
            if (storedKey != null)
            {
                json[storedKey] = storedJson[storedKey]?.DeepClone();
            }
        }

        return json.ToJsonString(EntitiesDashboardJson.Options);
    }

    /// <summary>Returns the visible (not hidden) descriptors in display order.</summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The visible descriptors, excluding unsupported kinds.</returns>
    public static IReadOnlyList<EntityPropertyDescriptor> GetVisibleProperties(EntitiesDashboardEntity entity) =>
        entity.Properties.Where(p => !p.IsHidden && p.EditorKind != EntitiesDashboardEditorKind.Unsupported).ToList();

    private static JsonNode ParseReplacement(string text)
    {
        if (text == null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text) ?? JsonValue.Create(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }

    private static string FindKey(JsonObject json, string name) =>
        json.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
}
