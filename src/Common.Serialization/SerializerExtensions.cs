// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static class SerializerExtensions
{
    public static string SerializeToString<T>(this ISerializer source, T input)
    {
        if (input is null)
        {
            return null;
        }

        var bytes = source.SerializeToBytes(input);
        if (source is ITextSerializer)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    ///     Serializes the specified input to bytes.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="input">The input.</param>
    public static byte[] SerializeToBytes<T>(this ISerializer source, T input)
    {
        if (input is null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        source.Serialize(input, stream);

        return stream.ToArray();
    }

    /// <summary>
    ///     Deserializes the specified data.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="ìnput">The inout.</param>
    public static T Deserialize<T>(this ISerializer source, Stream ìnput)
    {
        return (T)source.Deserialize(ìnput, typeof(T));
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="input">The input.</param>
    public static T Deserialize<T>(this ISerializer source, byte[] input)
    {
        using var stream = new MemoryStream(input);
        return (T)source.Deserialize(stream, typeof(T));
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="input">The input.</param>
    /// <param name="type">The type.</param>
    public static object Deserialize(this ISerializer source, byte[] input, Type type)
    {
        using var stream = new MemoryStream(input);
        return source.Deserialize(stream, type);
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="input">The input.</param>
    public static T Deserialize<T>(this ISerializer source, string input)
    {
        byte[] bytes;
        if (input is null)
        {
            bytes = [];
        }
        else if (source is ITextSerializer)
        {
            bytes = Encoding.UTF8.GetBytes(input);
        }
        else
        {
            bytes = Convert.FromBase64String(input);
        }

        using var stream = new MemoryStream(bytes);
        return (T)source.Deserialize(stream, typeof(T));
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="input">The input.</param>
    /// <param name="type">The type.</param>
    public static object Deserialize(this ISerializer source, string input, Type type)
    {
        byte[] bytes;
        if (input is null)
        {
            bytes = [];
        }
        else if (source is ITextSerializer)
        {
            bytes = Encoding.UTF8.GetBytes(input);
        }
        else
        {
            bytes = Convert.FromBase64String(input);
        }

        using var stream = new MemoryStream(bytes);
        return source.Deserialize(stream, type);
    }
}