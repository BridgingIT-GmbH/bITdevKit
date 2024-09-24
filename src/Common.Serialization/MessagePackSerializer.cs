// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class MessagePackSerializer : ISerializer
{
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