// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// The body of a create or update request. Values are invariant text (or JSON text for value objects); null means null.
/// </summary>
/// <param name="Entity">The entity key.</param>
/// <param name="Source">The source name, or null for the default source.</param>
/// <param name="Id">The id text (update), or null.</param>
/// <param name="Values">The submitted values keyed by property name.</param>
/// <param name="Replace">The masked property names the client explicitly replaces.</param>
/// <param name="MemberReplacements">Explicit replacements of masked value-object members keyed by <c>Parent.Member</c>.</param>
/// <param name="ConcurrencyVersion">The concurrency version the client loaded, or null.</param>
internal sealed record EntitiesDashboardWriteRequest(
    string Entity,
    string Source,
    string Id,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlySet<string> Replace,
    IReadOnlyDictionary<string, string> MemberReplacements,
    string ConcurrencyVersion)
{
    /// <summary>Parses the request from a JSON body.</summary>
    /// <param name="body">The JSON body.</param>
    /// <returns>The request, or null when the body has the wrong shape.</returns>
    public static EntitiesDashboardWriteRequest From(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var values = ReadMap(body, "values");
        var members = ReadMap(body, "memberReplacements");
        var replace = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (body.TryGetProperty("replace", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.String))
            {
                replace.Add(item.GetString());
            }
        }

        return new EntitiesDashboardWriteRequest(
            ReadText(body, "entity"),
            ReadText(body, "source"),
            ReadText(body, "id"),
            values,
            replace,
            members,
            ReadText(body, "concurrencyVersion"));
    }

    /// <summary>Reads a scalar JSON value as text; objects and arrays keep their JSON text.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The text, or null for JSON null.</returns>
    public static string AsText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => element.GetString(),
        _ => element.GetRawText()
    };

    private static string ReadText(JsonElement body, string name) =>
        body.TryGetProperty(name, out var value) ? AsText(value) : null;

    private static Dictionary<string, string> ReadMap(JsonElement body, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (body.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                map[property.Name] = AsText(property.Value);
            }
        }

        return map;
    }
}

/// <summary>One property change computed while applying a write request.</summary>
/// <param name="Property">The changed property.</param>
/// <param name="Before">The stored value (typed), or null.</param>
/// <param name="After">The new value (typed), or null.</param>
internal sealed record EntitiesDashboardChange(EntityPropertyDescriptor Property, object Before, object After);

/// <summary>
/// Builds entity instances from submitted values and applies submitted values to stored instances. Only descriptor-known,
/// writable and visible properties are touched; masked properties only change on an explicit replace.
/// </summary>
internal static class EntitiesDashboardEntityMaterializer
{
    /// <summary>Creates a new instance and applies all submitted values.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="request">The request.</param>
    /// <param name="generateId">False to leave a blank id at its default so the store (for example ActiveEntity upsert) treats the record as new and generates the id.</param>
    /// <returns>The new instance or a failed result with one validation error per invalid field.</returns>
    public static Result<object> Create(EntitiesDashboardEntity entity, EntitiesDashboardWriteRequest request, bool generateId = true)
    {
        if (entity.Creator == null)
        {
            return Result<object>.Failure("Create is not available for this entity.", new ValidationError("Create is not available for this entity.", "entity", null));
        }

        var instance = entity.Creator();
        var applied = Apply(entity, instance, request, true);
        if (applied.IsFailure)
        {
            return Result<object>.Failure(applied.Messages, applied.Errors);
        }

        SetNewId(entity, instance, request, generateId);

        return Result<object>.Success(instance);
    }

    /// <summary>Applies the submitted values to a stored instance and returns only the properties that actually changed.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="instance">The stored instance (modified in memory).</param>
    /// <param name="request">The request.</param>
    /// <returns>The changes or a failed result with one validation error per invalid field.</returns>
    public static Result<IReadOnlyList<EntitiesDashboardChange>> Update(EntitiesDashboardEntity entity, object instance, EntitiesDashboardWriteRequest request) =>
        Apply(entity, instance, request, false);

    private static Result<IReadOnlyList<EntitiesDashboardChange>> Apply(
        EntitiesDashboardEntity entity,
        object instance,
        EntitiesDashboardWriteRequest request,
        bool isCreate)
    {
        var changes = new List<EntitiesDashboardChange>();
        var errors = new List<IResultError>();

        foreach (var property in entity.Properties.Where(p => p.IsWritable && !p.IsHidden && !p.IsId))
        {
            if (!request.Values.TryGetValue(property.Name, out var text))
            {
                continue;
            }

            // masked values are only replaced when the client explicitly asks for it
            if (property.IsMasked && !request.Replace.Contains(property.Name))
            {
                continue;
            }

            var before = property.Getter(instance);
            var parsed = ParseValue(property, text, before, request);
            if (parsed.IsFailure)
            {
                errors.AddRange(parsed.Errors);

                continue;
            }

            if (!isCreate && AreEqual(property, before, parsed.Value))
            {
                continue;
            }

            try
            {
                property.Setter(instance, parsed.Value);
            }
            catch (Exception)
            {
                errors.Add(new ValidationError("The value was rejected.", property.Name, null));

                continue;
            }

            changes.Add(new EntitiesDashboardChange(property, before, parsed.Value));
        }

        return errors.Count == 0
            ? Result<IReadOnlyList<EntitiesDashboardChange>>.Success(changes)
            : Result<IReadOnlyList<EntitiesDashboardChange>>.Failure(errors.Select(e => e.Message), errors);
    }

    private static void SetNewId(EntitiesDashboardEntity entity, object instance, EntitiesDashboardWriteRequest request, bool generateId)
    {
        var id = entity.IdProperty;
        if (id?.Setter == null)
        {
            return;
        }

        var text = request.Values.TryGetValue(id.Name, out var submitted) ? submitted : null;
        var value = string.IsNullOrWhiteSpace(text)
            ? (generateId ? EntitiesDashboardIdConverter.NewGuidIfEmpty(entity.IdType) : null)
            : EntitiesDashboardIdConverter.Parse(entity.IdType, text) is { IsSuccess: true } parsed ? parsed.Value : null;

        if (value != null)
        {
            id.Setter(instance, value);
        }
    }

    private static Result<object> ParseValue(EntityPropertyDescriptor property, string text, object stored, EntitiesDashboardWriteRequest request)
    {
        if (property.EditorKind == EntitiesDashboardEditorKind.Json)
        {
            var json = property.IsMasked
                ? text
                : EntitiesDashboardMasking.MergeValueObjectJson(property, text, stored, request.MemberReplacements);

            return string.IsNullOrWhiteSpace(json) && !property.IsNullable
                ? Required(property)
                : EntitiesDashboardJson.Deserialize(property.ClrType, json, property.Name);
        }

        if (text == null || (text.Length == 0 && property.UnderlyingType != typeof(string)))
        {
            if (property.UnderlyingType == typeof(string) || property.IsNullable)
            {
                return Result<object>.Success(null);
            }

            return Required(property);
        }

        return EntitiesDashboardValueConverter.Parse(property.ClrType, text, property.Name);
    }

    private static bool AreEqual(EntityPropertyDescriptor property, object before, object after)
    {
        if (before == null || after == null)
        {
            return before == null && after == null;
        }

        if (property.EditorKind == EntitiesDashboardEditorKind.Json)
        {
            return string.Equals(
                JsonSerializer.Serialize(before, before.GetType(), EntitiesDashboardJson.Options),
                JsonSerializer.Serialize(after, after.GetType(), EntitiesDashboardJson.Options),
                StringComparison.Ordinal);
        }

        return Equals(before, after)
            || string.Equals(EntitiesDashboardValueConverter.Format(before), EntitiesDashboardValueConverter.Format(after), StringComparison.Ordinal);
    }

    private static Result<object> Required(EntityPropertyDescriptor property) =>
        Result<object>.Failure("A value is required.", new ValidationError("A value is required.", property.Name, null));
}
