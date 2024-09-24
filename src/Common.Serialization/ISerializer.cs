// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Defines methods for serializing and deserializing objects.
/// </summary>
public interface ISerializer
{
    /// <summary>
    ///     Serializes the specified object value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="output">The output.</param>
    void Serialize(object value, Stream output);

    /// <summary>
    ///     Deserializes the specified input stream.
    /// </summary>
    /// <param name="input">The input stream containing serialized data.</param>
    /// <param name="type">The type of the object to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    object Deserialize(Stream input, Type type);

    /// <summary>
    ///     Deserializes the specified input stream into an object of the specified type.
    /// </summary>
    /// <param name="input">The input stream.</param>
    /// <returns>The deserialized object.</returns>
    T Deserialize<T>(Stream input);
}