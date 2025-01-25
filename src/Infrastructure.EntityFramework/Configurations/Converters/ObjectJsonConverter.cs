// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class ObjectJsonConverter<TObject> : ValueConverter<TObject, string>
    where TObject : class
{
    public ObjectJsonConverter()
        : base(v => JsonSerializer.Serialize(v,
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
            v => JsonSerializer.Deserialize<TObject>(v, (JsonSerializerOptions)null))
    { }
}