namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Registers filesystem providers through existing named storage builders.</summary>
/// <example><code>builder.WithFileSystemClient("catalog", root);</code></example>
public static class FileSystemStorageServiceCollectionExtensions
{
    /// <summary>Registers a named filesystem blob client.</summary>
    /// <example><code>builder.WithFileSystemClient("catalog", root, o =&gt; o.Durability = FileSystemStorageDurability.Buffered);</code></example>
    public static BlobStorageBuilderContext WithFileSystemClient(this BlobStorageBuilderContext builder, string name, string rootPath, Action<FileSystemStorageOptions> configureFileSystem = null, Action<BlobStoreOptions> configure = null, ServiceLifetime? lifetime = null)
    {
        var options = new FileSystemStorageOptions
        {
            RootPath = rootPath
        };
        configureFileSystem?.Invoke(options);
        var descriptor = new FileSystemBlobStoreProvider(options);

        return builder.RegisterClient(name, (sp, blobOptions) => new FileSystemBlobStoreProvider(options, blobOptions, sp.GetService<BridgingIT.DevKit.Common.IContinuationTokenProtector>()),
            configure, "FileSystem", descriptor.Capabilities, lifetime);
    }

    /// <summary>Registers a typed filesystem document client.</summary>
    /// <example><code>builder.WithFileSystemClient&lt;CatalogItem&gt;(root);</code></example>
    public static DocumentStorageBuilderContext WithFileSystemClient<T>(this DocumentStorageBuilderContext builder, string rootPath, Action<FileSystemStorageOptions> configureFileSystem = null, DocumentStoreOptions documentStoreOptions = null, string name = "default", bool isDefault = true, ServiceLifetime? lifetime = null)
        where T : class, new()
    {
        var options = new FileSystemStorageOptions
        {
            RootPath = rootPath
        };
        configureFileSystem?.Invoke(options);
        var descriptor = new FileSystemDocumentStoreProvider(options, documentStoreOptions);

        return builder.RegisterProvider<T>(_ => new FileSystemDocumentStoreProvider(options, documentStoreOptions), "FileSystem", capabilities: descriptor.Capabilities,
            documentStoreOptions: documentStoreOptions, name: name, isDefault: isDefault, lifetime: lifetime);
    }
}