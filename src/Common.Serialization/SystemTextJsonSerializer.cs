﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

public class SystemTextJsonSerializer(JsonSerializerOptions options = null) : ISerializer, ITextSerializer
{
    private readonly JsonSerializerOptions options = options ?? DefaultSystemTextJsonSerializerOptions.Create();

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

        JsonSerializer.Serialize(output, value, this.options);
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

        if (input is null || input.Length == 0)
        {
            return null;
        }

        input.Position = 0;

        return JsonSerializer.Deserialize(input, type, this.options);
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="input">The input.</param>
    public T Deserialize<T>(Stream input)
    {
        if (input is null || input.Length == 0)
        {
            return default;
        }

        input.Position = 0;

        return JsonSerializer.Deserialize<T>(input, this.options);
    }
}