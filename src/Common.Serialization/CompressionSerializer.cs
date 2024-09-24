// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.IO.Compression;

/// <summary>
///     Initializes a new instance of the <see cref="CompressionSerializer" /> class.
/// </summary>
public class CompressionSerializer(ISerializer inner) : ISerializer
{
    private readonly ISerializer inner = inner;

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

        using var compress = new DeflateStream(output, CompressionMode.Compress, true);
        this.inner.Serialize(value, compress);
    }

    /// <summary>
    ///     Deserializes the specified input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="type">The type.</param>
    public object Deserialize(Stream input, Type type)
    {
        if (input is null)
        {
            return null;
        }

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        using var decompress = new DeflateStream(input, CompressionMode.Decompress, true);
        return this.inner.Deserialize(decompress, type);
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

        using var decompress = new DeflateStream(input, CompressionMode.Decompress, true);
        return this.inner.Deserialize<T>(decompress);
    }
}