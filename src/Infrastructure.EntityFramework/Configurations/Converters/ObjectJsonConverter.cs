// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Represents object json converter.
/// </summary>
/// <typeparam name="TObject">The object type.</typeparam>
public class ObjectJsonConverter<TObject> : ValueConverter<TObject, string>
    where TObject : class
{
    /// <summary>
    /// Initializes a new instance of the <c>ObjectJsonConverter</c> class.
    /// </summary>
    public ObjectJsonConverter()
        : base(v => JsonSerializer.Serialize(v,
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
            v => JsonSerializer.Deserialize<TObject>(v, (JsonSerializerOptions)null))
    { }
}
