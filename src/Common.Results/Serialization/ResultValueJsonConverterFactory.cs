// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Factory for creating a JSON converter for Result{T} objects
/// </summary>
public sealed class ResultValueJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// Determines whether the given type can be converted by this converter.
    /// </summary>
    /// <param name="typeToConvert">The type to check for conversion capability.</param>
    /// <returns>True if the type can be converted, false otherwise.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        return typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);
    }

    /// <summary>
    /// Creates a JSON converter for the specified generic Result{T} type.
    /// </summary>
    /// <param name="typeToConvert">The type of the object to convert.</param>
    /// <param name="options">Options to control the conversion behavior.</param>
    /// <return>A JSON converter for the specified generic Result{T} type.</return>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultValueJsonConverter<>).MakeGenericType(valueType);

        return (JsonConverter)Activator.CreateInstance(converterType);
    }
}