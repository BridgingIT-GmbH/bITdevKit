namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Reports incompatible filesystem store configuration or unsupported required primitives.</summary>
/// <example><code>if (result.HasError&lt;FileSystemStorageConfigurationError&gt;()) { /* correct configuration */ }</code></example>
public sealed class FileSystemStorageConfigurationError(string message) : StorageError(message);

/// <summary>Reports malformed managed metadata, identity mismatch, or missing committed content.</summary>
/// <example><code>if (result.HasError&lt;FileSystemStorageCorruptionError&gt;()) { /* inspect the managed record */ }</code></example>
public sealed class FileSystemStorageCorruptionError(string message) : StorageError(message);

/// <summary>Reports unavailable filesystem I/O without exposing paths or credentials.</summary>
/// <example><code>if (result.HasError&lt;FileSystemStorageUnavailableError&gt;()) { /* restore storage connectivity */ }</code></example>
public sealed class FileSystemStorageUnavailableError(string message) : StorageError(message), ITransientStorageError
{
    /// <inheritdoc />
    public bool IsTransient => true;
}

/// <summary>Reports denied access to provider-owned filesystem resources.</summary>
/// <example><code>if (result.HasError&lt;FileSystemStoragePermissionError&gt;()) { /* correct OS permissions */ }</code></example>
public sealed class FileSystemStoragePermissionError() : StorageError("Access to managed filesystem storage was denied.");

/// <summary>Reports exhausted filesystem space or quota before a mutation is published.</summary>
/// <example><code>if (result.HasError&lt;FileSystemStorageCapacityError&gt;()) { /* restore available capacity */ }</code></example>
public sealed class FileSystemStorageCapacityError() : StorageError("Managed filesystem storage capacity or quota is exhausted.");

/// <summary>Reports an attempted metadata publication larger than the configured manifest bound.</summary>
/// <example><code>if (result.HasError&lt;FileSystemStorageMetadataLimitError&gt;()) { /* reduce metadata or configure its bound */ }</code></example>
public sealed class FileSystemStorageMetadataLimitError() : StorageError("Publication metadata exceeds MaxManifestBytes.");