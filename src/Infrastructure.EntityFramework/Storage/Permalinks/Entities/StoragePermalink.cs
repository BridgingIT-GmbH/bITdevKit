// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Represents one persisted Storage Permalink Registry entry.
/// </summary>
/// <example>
/// <code>
/// var entry = new StoragePermalink { Id = permalinkId.Value };
/// </code>
/// </example>
[Table("__Storage_Permalinks")]
[Index(nameof(ActiveLocationHash), IsUnique = true)]
[Index(nameof(LocationHash), nameof(DeletedAt))]
[Index(nameof(StorageKind), nameof(RegistrationName), nameof(DeletedAt))]
public sealed class StoragePermalink
{
    /// <summary>
    /// Gets or sets the stable permalink identifier.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.Id = StoragePermalinkId.New().Value;
    /// </code>
    /// </example>
    [Key, Required, MaxLength(43)]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the storage resource kind.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.StorageKind = (int)StorageResourceKind.Blob;
    /// </code>
    /// </example>
    [Required]
    public int StorageKind { get; set; }

    /// <summary>
    /// Gets or sets the normalized configured storage registration name.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.RegistrationName = "default";
    /// </code>
    /// </example>
    [Required, MaxLength(256)]
    public string RegistrationName { get; set; }

    /// <summary>
    /// Gets or sets the container or partition scope.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.Scope = "reports";
    /// </code>
    /// </example>
    [Required, MaxLength(2048)]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current blob name, document row key, or file path.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.Path = "2026/report.pdf";
    /// </code>
    /// </example>
    [Required, MaxLength(4096)]
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the current canonical location.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.LocationHash = location.ComputeHash();
    /// </code>
    /// </example>
    [Required, MaxLength(64)]
    public string LocationHash { get; set; }

    /// <summary>
    /// Gets or sets the unique active-location key or a tombstone-specific value.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.ActiveLocationHash = location.ComputeHash();
    /// </code>
    /// </example>
    [Required, MaxLength(128)]
    public string ActiveLocationHash { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.CreatedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the latest registry mutation timestamp.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.UpdatedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the mapped storage location last changed.
    /// </summary>
    /// <remarks>
    /// This timestamp orders asynchronous storage notifications independently from permalink maintenance changes.
    /// </remarks>
    /// <example>
    /// <code>
    /// entry.StorageChangedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    [Required]
    public DateTimeOffset StorageChangedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional permalink expiration.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the registry mapping was tombstoned.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.DeletedAt = DateTimeOffset.UtcNow;
    /// </code>
    /// </example>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this row exists only to order asynchronous storage changes.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.IsSynchronizationTombstone = true;
    /// </code>
    /// </example>
    [Required]
    public bool IsSynchronizationTombstone { get; set; }

    /// <summary>
    /// Gets or sets whether a synchronization tombstone covers every File Storage descendant.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.IsPrefixTombstone = true;
    /// </code>
    /// </example>
    [Required]
    public bool IsPrefixTombstone { get; set; }

    /// <summary>
    /// Gets or sets the optimistic-concurrency version.
    /// </summary>
    /// <example>
    /// <code>
    /// entry.ConcurrencyVersion = Guid.NewGuid();
    /// </code>
    /// </example>
    [Required, ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();
}
