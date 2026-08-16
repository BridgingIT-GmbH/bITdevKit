// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Serializes values with contractless MessagePack options that support private members.
/// </summary>
public class MessagePackSerializer : ISerializer
{
    /// <summary>
    ///     Serializes a non-null value to a non-null output stream.
    /// </summary>
    /// <param name="value">The value to serialize. A <see langword="null"/> value is ignored.</param>
    /// <param name="output">The destination stream. A <see langword="null"/> stream is ignored.</param>
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

        MessagePack.MessagePackSerializer.Serialize(output, value, MessagePackSerializerSettings.Create());
    }

    /// <summary>
    ///     Deserializes a MessagePack stream as the specified runtime type after rewinding the stream.
    /// </summary>
    /// <param name="input">The input stream, or <see langword="null"/> to return <see langword="null"/>.</param>
    /// <param name="type">The type to deserialize.</param>
    /// <returns>The deserialized value, or <see langword="null"/> for a null or empty stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
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

        return MessagePack.MessagePackSerializer.Deserialize(type, input, MessagePackSerializerSettings.Create());
    }

    /// <summary>
    ///     Deserializes a MessagePack stream as <typeparamref name="T"/> after rewinding the stream.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="input">The input stream, or <see langword="null"/> to return the default value.</param>
    /// <returns>The deserialized value, or the default value for a null or empty stream.</returns>
    public T Deserialize<T>(Stream input)
    {
        if (input is null || input.Length == 0)
        {
            return default;
        }

        input.Position = 0;

        return MessagePack.MessagePackSerializer.Deserialize<T>(input, MessagePackSerializerSettings.Create());
    }
}
