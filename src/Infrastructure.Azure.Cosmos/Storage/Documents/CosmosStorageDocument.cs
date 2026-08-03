// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Common;
using Newtonsoft.Json;

public class CosmosStorageDocument
{
    public string Id { get; set; }

    [Required]
    [MaxLength(1024)]
    public string Type { get; set; }

    [Required]
    [MaxLength(512)]
    public string PartitionKey { get; set; }

    [Required]
    [MaxLength(512)]
    public string RowKey { get; set; }

    public byte[] Content { get; set; }

    [MaxLength(80)]
    public string ContentHash { get; set; }

    [MaxLength(80)]
    public string StoredContentHash { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public int Ttl { get; set; } = -1;

    public string TransformMetadataJson { get; set; }

    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTimeOffset? UpdatedDate { get; set; }

    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    [Column("Properties")]
    public string PropertiesJson
    {
        get => EncodeBag(this.Properties);
        set => this.Properties = DecodeBag(value);
    }

    [JsonProperty(PropertyName = "_etag")]
    [JsonPropertyName("_etag")]
    public string ETag { get; set; }

    private static string EncodeBag(IDictionary<string, object> values) => values.IsNullOrEmpty()
        ? null
        : System.Text.Json.JsonSerializer.Serialize(values.ToDictionary(value => value.Key, value => PropertyBagScalarCodec.Encode(value.Value)));

    private static IDictionary<string, object> DecodeBag(string value) => value.IsNullOrEmpty()
        ? new Dictionary<string, object>()
        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(value)
            .ToDictionary(item => item.Key, item => PropertyBagScalarCodec.Decode(item.Value));
}
