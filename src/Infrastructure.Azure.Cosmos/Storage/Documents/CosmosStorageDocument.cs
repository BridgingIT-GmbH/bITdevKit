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

/// <summary>
/// Represents cosmos storage document.
/// </summary>
public class CosmosStorageDocument
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [Required]
    [MaxLength(1024)]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the partition key.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the row key.
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string RowKey { get; set; }

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public byte[] Content { get; set; }

    /// <summary>
    /// Gets or sets the content hash.
    /// </summary>
    [MaxLength(80)]
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the stored content hash.
    /// </summary>
    [MaxLength(80)]
    public string StoredContentHash { get; set; }

    /// <summary>
    /// Gets or sets the expires at.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the ttl.
    /// </summary>
    public int Ttl { get; set; } = -1;

    /// <summary>
    /// Gets or sets the transform metadata json.
    /// </summary>
    public string TransformMetadataJson { get; set; }

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Stores the properties json.
    /// </summary>
    [Column("Properties")]
    public string PropertiesJson
    {
        get => EncodeBag(this.Properties);
        set => this.Properties = DecodeBag(value);
    }

    /// <summary>
    /// Gets or sets the e tag.
    /// </summary>
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
