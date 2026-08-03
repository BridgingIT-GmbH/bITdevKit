// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

/// <summary>
/// Represents a persisted blob metadata row for the Entity Framework blob store provider.
/// </summary>
/// <example>
/// <code>
/// var row = new StorageBlob
/// {
///     Container = "reports",
///     Name = "2026/06/report.pdf",
///     ContentTypeMimeType = "application/pdf"
/// };
/// </code>
/// </example>
[Table("__Storage_Blobs")]
[Index(nameof(ContainerHash), nameof(NameHash), IsUnique = true)]
[Index(nameof(Container), nameof(Name))]
[Index(nameof(LeaseAcquiredUntil))]
[Index(nameof(ExpiresAt))]
public sealed class StorageBlob
{
    /// <summary>
    /// Defines the fixed maximum length for persisted raw container values in the EF provider.
    /// </summary>
    public const int MaximumContainerLength = 256;

    /// <summary>
    /// Defines the fixed maximum length for persisted raw blob names in the EF provider.
    /// </summary>
    public const int MaximumNameLength = 2048;

    /// <summary>
    /// Defines the fixed maximum length for SHA-256 lookup hashes without the algorithm prefix.
    /// </summary>
    public const int MaximumLookupHashLength = 64;

    /// <summary>
    /// Defines the fixed maximum length for provider-neutral content hashes in the <c>sha256:&lt;hex&gt;</c> format.
    /// </summary>
    public const int MaximumContentHashLength = 71;

    /// <summary>
    /// Defines the fixed maximum length for MIME type strings.
    /// </summary>
    public const int MaximumContentTypeMimeTypeLength = 256;

    /// <summary>
    /// Defines the fixed maximum length for provider ETag values.
    /// </summary>
    public const int MaximumETagLength = 256;

    /// <summary>
    /// Defines the fixed maximum length for lease identifiers and lease owner values.
    /// </summary>
    public const int MaximumLeaseValueLength = 256;

    /// <summary>
    /// Gets or sets the internal blob row identifier.
    /// </summary>
    /// <example>
    /// <code>
    /// var id = blob.Id;
    /// </code>
    /// </example>
    [Key]
    [Required]
    [MaxLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the logical top-level blob container.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.Container = "reports";
    /// </code>
    /// </example>
    [Required]
    [MaxLength(MaximumContainerLength)]
    public string Container { get; set; }

    /// <summary>
    /// Gets or sets the path-like blob name inside the container.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.Name = "2026/06/report.pdf";
    /// </code>
    /// </example>
    [Required]
    [MaxLength(MaximumNameLength)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 lookup hash for <see cref="Container" />.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.ContainerHash = HashHelper.ComputeSha256(blob.Container);
    /// </code>
    /// </example>
    [Required]
    [MaxLength(MaximumLookupHashLength)]
    public string ContainerHash { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 lookup hash for <see cref="Name" />.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.NameHash = HashHelper.ComputeSha256(blob.Name);
    /// </code>
    /// </example>
    [Required]
    [MaxLength(MaximumLookupHashLength)]
    public string NameHash { get; set; }

    /// <summary>
    /// Gets or sets the persisted blob length in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.Length = 1024;
    /// </code>
    /// </example>
    [Required]
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets the persisted MIME type string produced from the provider-neutral content type.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.ContentTypeMimeType = "application/pdf";
    /// </code>
    /// </example>
    [MaxLength(MaximumContentTypeMimeTypeLength)]
    public string ContentTypeMimeType { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral SHA-256 content hash.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.ContentHash = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    /// </code>
    /// </example>
    [MaxLength(MaximumContentHashLength)]
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the provider ETag value for conditional property updates.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.ETag = Guid.NewGuid().ToString("N");
    /// </code>
    /// </example>
    [MaxLength(MaximumETagLength)]
    public string ETag { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.CreatedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp of the last blob metadata or content update.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.LastModifiedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    [Required]
    public DateTimeOffset LastModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the UTC expiration timestamp used by blob retention sweeping.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the strongly typed custom properties restored from <see cref="PropertiesJson" />.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.Properties["source"] = "monthly-export";
    /// </code>
    /// </example>
    [NotMapped]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the JSON persistence column for <see cref="Properties" />.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.PropertiesJson = "{}";
    /// </code>
    /// </example>
    [Column("Properties")]
    public string PropertiesJson
    {
        get => this.Properties.IsNullOrEmpty()
            ? null
            : JsonSerializer.Serialize(this.Properties, DefaultJsonSerializerOptions.Create());
        set => this.Properties = value.IsNullOrEmpty()
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultJsonSerializerOptions.Create());
    }

    /// <summary>
    /// Gets or sets the internal mutation lease identifier.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.LeaseId = Guid.NewGuid().ToString("N");
    /// </code>
    /// </example>
    [MaxLength(MaximumLeaseValueLength)]
    public string LeaseId { get; set; }

    /// <summary>
    /// Gets or sets the logical worker that owns the active mutation lease.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.LeaseAcquiredBy = "worker-a";
    /// </code>
    /// </example>
    [MaxLength(MaximumLeaseValueLength)]
    public string LeaseAcquiredBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp until which the current mutation lease remains valid.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.LeaseAcquiredUntil = DateTimeOffset.UtcNow.AddMinutes(1);
    /// </code>
    /// </example>
    public DateTimeOffset? LeaseAcquiredUntil { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral optimistic concurrency token.
    /// </summary>
    /// <example>
    /// <code>
    /// blob.ConcurrencyVersion = Guid.NewGuid();
    /// </code>
    /// </example>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the content chunks owned by this blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var chunks = blob.Chunks;
    /// </code>
    /// </example>
    [InverseProperty(nameof(StorageBlobChunk.Blob))]
    public ICollection<StorageBlobChunk> Chunks { get; set; } = [];
}
