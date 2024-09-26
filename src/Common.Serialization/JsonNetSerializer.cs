// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

// TODO: get rid of Newtonsoft dependency

public class JsonNetSerializer(JsonSerializerSettings settings = null) : ISerializer, ITextSerializer
{
    private readonly JsonSerializer serializer =
        JsonSerializer.Create(settings ?? DefaultJsonNetSerializerSettings.Create());

    /// <summary>
    ///     Serializes the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="output">The output.</param>
    public void Serialize(object value, Stream output)
    {
        if (value is null)
        {
            return;
        }

        if (output is null)
        {
            return;
        }

        using var writer = new JsonTextWriter(new StreamWriter(output, Encoding.UTF8, 1024, true));
        writer.AutoCompleteOnClose = false;
        writer.CloseOutput = false;
        this.serializer.Serialize(writer, value, value.GetType());
        writer.Flush();
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="type">The type.</param>
    public object Deserialize(Stream input, Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException("Type cannot be null when deserializing", nameof(type));
        }

        if (input is null)
        {
            return null;
        }

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        using var sr = new StreamReader(input, Encoding.UTF8, true, 1024, true);
        using var reader = new JsonTextReader(sr);

        return this.serializer.Deserialize(reader, type);
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="input">The input.</param>
    public T Deserialize<T>(Stream input)
    {
        if (input is null)
        {
            return default;
        }

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        using var sr = new StreamReader(input, Encoding.UTF8, true, 1024, true);
        using var reader = new JsonTextReader(sr);

        return this.serializer.Deserialize<T>(reader);
    }
}

public class JsonNetPrivateResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        if (!property.Writable)
        {
            var propertyInfo = member as PropertyInfo;
            property.Writable = propertyInfo?.GetSetMethod(true) is not null;
        }

        return property;
    }
}