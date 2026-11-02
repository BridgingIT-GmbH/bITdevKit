namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Defines the declared store access protocol.</summary>
/// <example><code>options.AccessKind = FileSystemStorageAccessKind.Local;</code></example>
public enum FileSystemStorageAccessKind
{
    /// <summary>Local filesystem access.</summary>
    Local,
    /// <summary>SMB access, pending qualification.</summary>
    Smb,
    /// <summary>Linux NFS access, pending qualification.</summary>
    Nfs
}

/// <summary>Defines required acknowledgement persistence.</summary>
/// <example><code>options.Durability = FileSystemStorageDurability.Durable;</code></example>
public enum FileSystemStorageDurability
{
    /// <summary>Requires native file and namespace persistence operations before acknowledgement.</summary>
    Durable,
    /// <summary>Allows recent acknowledged mutations to be lost after host failure.</summary>
    Buffered
}

/// <summary>Configures filesystem storage. Unsupported native primitives fail closed; deployment qualification remains separate.</summary>
/// <example><code>var options = new FileSystemStorageOptions { RootPath = Path.Combine(Path.GetTempPath(), "catalog") };</code></example>
public sealed class FileSystemStorageOptions
{
    /// <summary>Gets or sets the absolute provider-owned root.</summary>
    /// <example><code>options.RootPath = Path.Combine(Path.GetTempPath(), "catalog");</code></example>
    public string RootPath { get; set; }

    /// <summary>Gets or sets the declared protocol.</summary>
    /// <example><code>options.AccessKind = FileSystemStorageAccessKind.Local;</code></example>
    public FileSystemStorageAccessKind AccessKind { get; set; }

    /// <summary>Gets or sets whether missing roots may be created.</summary>
    /// <example><code>options.CreateRoot = false;</code></example>
    public bool CreateRoot { get; set; } = true;

    /// <summary>Gets or sets the interruptible authority acquisition budget.</summary>
    /// <example><code>options.LockAcquireTimeout = TimeSpan.Zero;</code></example>
    public TimeSpan LockAcquireTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the required acknowledgement mode.</summary>
    /// <example><code>options.Durability = FileSystemStorageDurability.Buffered;</code></example>
    public FileSystemStorageDurability Durability { get; set; }

    /// <summary>Gets or sets the pooled streaming buffer size.</summary>
    /// <example><code>options.TransferBufferSize = 65536;</code></example>
    public int TransferBufferSize { get; set; } = 65536;

    /// <summary>Gets or sets the maximum metadata bytes.</summary>
    /// <example><code>options.MaxManifestBytes = 1048576;</code></example>
    public int MaxManifestBytes { get; set; } = 1048576;

    /// <summary>Gets or sets the maximum metadata entries examined by a query.</summary>
    /// <example><code>options.MaxQueryMetadataEntries = 100000;</code></example>
    public int MaxQueryMetadataEntries { get; set; } = 100000;

    /// <summary>Gets or sets the maximum metadata entries examined by a sweep.</summary>
    /// <example><code>options.MaxRetentionMetadataEntries = 10000;</code></example>
    public int MaxRetentionMetadataEntries { get; set; } = 10000;

    /// <summary>Validates configuration without accessing the root.</summary>
    /// <example><code>var validation = options.Validate();</code></example>
    public Result Validate()
    {
        return string.IsNullOrWhiteSpace(this.RootPath) || !Path.IsPathFullyQualified(this.RootPath)
        || !Enum.IsDefined(this.AccessKind) || !Enum.IsDefined(this.Durability)
        || this.LockAcquireTimeout < TimeSpan.Zero || this.TransferBufferSize <= 0 || this.MaxManifestBytes <= 0
        || this.MaxQueryMetadataEntries <= 0 || this.MaxRetentionMetadataEntries <= 0
        || (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        || (OperatingSystem.IsWindows() && this.AccessKind == FileSystemStorageAccessKind.Nfs)
        ? Result.Failure(new ValidationError("Invalid filesystem storage options or unsupported host/protocol.")) : Result.Success();
    }
}