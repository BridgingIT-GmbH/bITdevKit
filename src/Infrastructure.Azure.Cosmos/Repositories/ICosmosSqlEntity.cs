// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Text.Json.Serialization;
using Newtonsoft.Json;

public interface ICosmosSqlEntity
{
    [JsonProperty(PropertyName = "id")]
    [JsonPropertyName("id")]
    string Id { get; set; } // maps to id
}