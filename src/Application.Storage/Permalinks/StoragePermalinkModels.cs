// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;

/// <summary>
/// Identifies a storage resource that can be reached through a permalink.
/// </summary>
/// <example>
/// <code>
/// var kind = StorageResourceKind.Blob;
/// </code>
/// </example>
public enum StorageResourceKind
{
    /// <summary>
    /// A Blob Storage resource.
    /// </summary>
    Blob = 0,

    /// <summary>
    /// A Document Storage resource.
    /// </summary>
    Document = 1,

    /// <summary>
    /// A File Storage resource.
    /// </summary>
    File = 2
}

/// <summary>
/// Represents the lifecycle state of one stored permalink.
/// </summary>
/// <example>
/// <code>
/// if (entry.Status == StoragePermalinkStatus.Active) { }
/// </code>
/// </example>
public enum StoragePermalinkStatus
{
    /// <summary>
    /// The permalink can resolve its resource.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The permalink has reached its configured expiration.
    /// </summary>
    Expired = 1,

    /// <summary>
    /// The permalink was explicitly or implicitly deleted.
    /// </summary>
    Deleted = 2
}

/// <summary>
/// Contains a cryptographically random, URL-safe permalink identifier.
/// </summary>
/// <example>
/// <code>
/// var id = StoragePermalinkId.New();
/// </code>
/// </example>
public readonly record struct StoragePermalinkId
{
    /// <summary>
    /// The canonical identifier length.
    /// </summary>
    public const int Length = 43;

    /// <summary>
    /// Creates a validated permalink identifier.
    /// </summary>
    /// <param name="value">
    /// The canonical Base64Url value.
    /// </param>
    /// <exception cref="ArgumentException">The value is not a canonical 256-bit permalink identifier.</exception>
    /// <example>
    /// <code>
    /// var id = new StoragePermalinkId(value);
    /// </code>
    /// </example>
    public StoragePermalinkId(string value)
    {
        if (!TryParse(value, out var parsed))
        {
            throw new ArgumentException("A permalink identifier must be a canonical 256-bit Base64Url value.", nameof(value));
        }

        this.Value = parsed.Value;
    }

    private StoragePermalinkId(string value, bool _) => this.Value = value;

    /// <summary>
    /// Gets the canonical Base64Url value.
    /// </summary>
    /// <example>
    /// <code>
    /// Console.WriteLine(id.Value);
    /// </code>
    /// </example>
    public string Value { get; }

    /// <summary>
    /// Creates a new cryptographically random permalink identifier.
    /// </summary>
    /// <returns>
    /// A new 256-bit identifier.
    /// </returns>
    /// <example>
    /// <code>
    /// var id = StoragePermalinkId.New();
    /// </code>
    /// </example>
    public static StoragePermalinkId New() => new(Base64UrlHelper.Encode(RandomNumberGenerator.GetBytes(32)), true);

    /// <summary>
    /// Attempts to parse a canonical permalink identifier.
    /// </summary>
    /// <param name="value">
    /// The candidate value.
    /// </param>
    /// <param name="result">
    /// The parsed identifier when successful.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the value is valid.
    /// </returns>
    /// <example>
    /// <code>
    /// if (StoragePermalinkId.TryParse(value, out var id)) { }
    /// </code>
    /// </example>
    public static bool TryParse(string value, out StoragePermalinkId result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != Length)
        {
            return false;
        }

        try
        {
            var bytes = Base64UrlHelper.Decode(value);
            if (bytes.Length != 32)
            {
                return false;
            }

            result = new StoragePermalinkId(value, true);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => this.Value ?? string.Empty;
}

/// <summary>
/// Describes the current provider-neutral location of a permalink resource.
/// </summary>
/// <example>
/// <code>
/// var location = StorageResourceLocation.ForBlob("reports", new BlobKey("pdf", "2026/report.pdf"));
/// </code>
/// </example>
public sealed record StorageResourceLocation
{
    /// <summary>
    /// Gets the storage resource kind.
    /// </summary>
    public required StorageResourceKind Kind { get; init; }

    /// <summary>
    /// Gets the normalized configured client or provider registration name.
    /// </summary>
    public required string RegistrationName { get; init; }

    /// <summary>
    /// Gets the container, partition key, or empty file scope.
    /// </summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// Gets the blob name, document row key, or file path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Creates a Blob Storage location.
    /// </summary>
    public static StorageResourceLocation ForBlob(string registrationName, BlobKey key) =>
        Create(StorageResourceKind.Blob, registrationName, key?.Container, key?.Name);

    /// <summary>
    /// Creates a Document Storage location.
    /// </summary>
    public static StorageResourceLocation ForDocument(string clientId, DocumentKey key) =>
        Create(StorageResourceKind.Document, clientId, key.PartitionKey, key.RowKey);

    /// <summary>
    /// Creates a File Storage location.
    /// </summary>
    public static StorageResourceLocation ForFile(string providerName, string path) =>
        Create(StorageResourceKind.File, providerName, string.Empty, path?.Replace('\\', '/'));

    /// <summary>
    /// Returns the stable canonical location representation.
    /// </summary>
    public string ToCanonicalString() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"{(int)this.Kind}:{Part(this.RegistrationName)}:{Part(this.Scope)}:{Part(this.Path)}");

    /// <summary>
    /// Returns the SHA-256 hash of the canonical location.
    /// </summary>
    public string ComputeHash() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(this.ToCanonicalString())));

    private static StorageResourceLocation Create(StorageResourceKind kind, string registrationName, string scope, string path)
    {
        if (string.IsNullOrWhiteSpace(registrationName))
        {
            throw new ArgumentException("A storage registration name is required.", nameof(registrationName));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A storage resource path or key is required.", nameof(path));
        }

        return new StorageResourceLocation
        {
            Kind = kind,
            RegistrationName = registrationName.Trim().ToLowerInvariant(),
            Scope = scope?.Trim() ?? string.Empty,
            Path = path.TrimStart('/')
        };
    }

    private static string Part(string value) => $"{value?.Length ?? 0}:{value}";
}

/// <summary>
/// Represents one persisted permalink and its current resource location.
/// </summary>
/// <example>
/// <code>
/// Console.WriteLine(entry.Id);
/// </code>
/// </example>
public sealed record StoragePermalinkEntry
{
    /// <summary>
    /// Gets the stable permalink identifier.
    /// </summary>
    public required StoragePermalinkId Id { get; init; }

    /// <summary>
    /// Gets the current resource location.
    /// </summary>
    public required StorageResourceLocation Location { get; init; }

    /// <summary>
    /// Gets when the permalink was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets when the registry entry was last changed.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the optional permalink expiration.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets when the permalink was deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; init; }

    /// <summary>
    /// Gets the optimistic-concurrency entity tag.
    /// </summary>
    public string ETag { get; init; }

    /// <summary>
    /// Gets the current lifecycle status.
    /// </summary>
    public StoragePermalinkStatus Status { get; init; }
}

/// <summary>
/// Configures creation of a permalink for an existing resource.
/// </summary>
/// <example>
/// <code>
/// var options = new StoragePermalinkCreateOptions { ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) };
/// </code>
/// </example>
public sealed record StoragePermalinkCreateOptions
{
    /// <summary>
    /// Gets the optional initial expiration, applied only when a new ID is created.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Filters and pages permalink registry entries.
/// </summary>
/// <example>
/// <code>
/// var query = new StoragePermalinkQuery { Take = 25, Status = StoragePermalinkStatus.Expired };
/// </code>
/// </example>
public sealed record StoragePermalinkQuery
{
    /// <summary>
    /// Gets an optional exact permalink identifier.
    /// </summary>
    public StoragePermalinkId? Id { get; init; }

    /// <summary>
    /// Gets an optional storage-kind filter.
    /// </summary>
    public StorageResourceKind? Kind { get; init; }

    /// <summary>
    /// Gets an optional registration-name filter.
    /// </summary>
    public string RegistrationName { get; init; }

    /// <summary>
    /// Gets an optional case-insensitive location substring.
    /// </summary>
    public string LocationContains { get; init; }

    /// <summary>
    /// Gets an optional status filter. Deleted entries are excluded when omitted.
    /// </summary>
    public StoragePermalinkStatus? Status { get; init; }

    /// <summary>
    /// Gets the requested page size from 1 through 500.
    /// </summary>
    public int Take { get; init; } = 25;

    /// <summary>
    /// Gets the opaque continuation token.
    /// </summary>
    public string ContinuationToken { get; init; }
}

/// <summary>
/// Contains one bounded page of permalink entries.
/// </summary>
/// <example>
/// <code>
/// foreach (var entry in page.Items) { }
/// </code>
/// </example>
public sealed record StoragePermalinkPage
{
    /// <summary>
    /// Gets the page entries.
    /// </summary>
    public IReadOnlyList<StoragePermalinkEntry> Items { get; init; } = [];

    /// <summary>
    /// Gets the next-page token, or <see langword="null" />.
    /// </summary>
    public string ContinuationToken { get; init; }
}

/// <summary>
/// Configures an expiration replacement for an existing permalink.
/// </summary>
/// <example>
/// <code>
/// var update = new StoragePermalinkExpirationUpdate { ExpiresAt = null, IfMatchETag = entry.ETag };
/// </code>
/// </example>
public sealed record StoragePermalinkExpirationUpdate
{
    /// <summary>
    /// Gets the replacement expiration; null clears expiration.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets an optional optimistic-concurrency entity tag.
    /// </summary>
    public string IfMatchETag { get; init; }
}

/// <summary>
/// Configures deletion of an existing permalink.
/// </summary>
/// <example>
/// <code>
/// var options = new StoragePermalinkDeleteOptions { IfMatchETag = entry.ETag };
/// </code>
/// </example>
public sealed record StoragePermalinkDeleteOptions
{
    /// <summary>
    /// Gets an optional optimistic-concurrency entity tag.
    /// </summary>
    public string IfMatchETag { get; init; }
}
