// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbProfilingStorageProvider
{
    private static JsonSerializerOptions CreateStorageJsonOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Kind != JsonTypeInfoKind.Object || info.Type.Namespace != typeof(ProfilingNode).Namespace) return;
            // These identifiers and correlations are hidden from response JSON, but belong in complete persisted graphs.
            foreach (var property in info.Properties.ToArray())
            {
                if (property.AttributeProvider is not PropertyInfo member || member.GetCustomAttribute<JsonIgnoreAttribute>() is null) continue;
                var stored = info.CreateJsonPropertyInfo(property.PropertyType, property.Name);
                stored.Get = member.GetValue;
                if (member.SetMethod is not null) stored.Set = member.SetValue;
                info.Properties.Remove(property);
                info.Properties.Add(stored);
            }
        });
        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, TypeInfoResolver = resolver };
        options.Converters.Add(new IdentityConverter<ProfilingNodeIdentity>(id => id.Id, id => id.Key, (id, key) => new(id, key)));
        options.Converters.Add(new IdentityConverter<RuntimeProfilingSessionIdentity>(id => id.Id, id => id.Key, (id, key) => new(id, key)));
        options.Converters.Add(new IdentityConverter<RuntimeProfilingSnapshotIdentity>(id => id.Id, id => id.Key, (id, key) => new(id, key)));
        return options;
    }

    private sealed class IdentityConverter<T>(Func<T, Guid> identity, Func<T, string> key, Func<Guid, string, T> create) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return create(document.RootElement.GetProperty("Id").GetGuid(), document.RootElement.GetProperty("Key").GetString());
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Id", identity(value));
            writer.WriteString("Key", key(value));
            writer.WriteEndObject();
        }
    }
}
