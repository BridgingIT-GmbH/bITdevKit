// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Storage;

using BridgingIT.DevKit.Application.Storage;
using static BridgingIT.DevKit.Application.Storage.FileStorageProviderFactory;

public static class FileStorageFactoryExtensions
{
    /// <summary>
    /// Configures an Azure Blob storage provider.
    /// </summary>
    /// <param name="factory">The file storage factory instance.</param>
    /// <param name="providerName">The unique name of the provider.</param>
    /// <param name="configure">Action to configure the provider using a builder.</param>
    /// <returns>The factory for method chaining.</returns>
    public static IFileStorageProviderFactory RegisterAzureBlobProvider(
        this FileStorageProviderFactory factory,
        string providerName,
        Action<FileStorageBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(configure);

        return factory.RegisterProvider(providerName, configure);
    }

    /// <summary>
    /// Configures an Azure Files storage provider.
    /// </summary>
    /// <param name="factory">The file storage factory instance.</param>
    /// <param name="providerName">The unique name of the provider.</param>
    /// <param name="configure">Action to configure the provider using a builder.</param>
    /// <returns>The factory for method chaining.</returns>
    public static IFileStorageProviderFactory RegisterAzureFilesProvider(
        this FileStorageProviderFactory factory,
        string providerName,
        Action<FileStorageBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(configure);

        return factory.RegisterProvider(providerName, configure);
    }

    /// <summary>
    /// Configures an Azure Blob storage provider.
    /// </summary>
    /// <param name="builder">The file storage builder instance.</param>
    /// <param name="locationName">The logical name for the storage location.</param>
    /// <param name="connectionString">The Azure Blob storage connection string.</param>
    /// <param name="containerName">The name of the container.</param>
    /// <returns>The builder for method chaining.</returns>
    public static FileStorageBuilder UseAzureBlob(
        this FileStorageBuilder builder,
        string locationName,
        string connectionString,
        string containerName,
        bool ensureContainer = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ProviderFactory = (sp) => new AzureBlobFileStorageProvider(locationName, connectionString, containerName, ensureContainer);
        return builder;
    }

    /// <summary>
    /// Configures an Azure Files storage provider.
    /// </summary>
    /// <param name="builder">The file storage builder instance.</param>
    /// <param name="locationName">The logical name for the storage location.</param>
    /// <param name="connectionString">The Azure Files connection string.</param>
    /// <param name="shareName">The name of the file share.</param>
    /// <returns>The builder for method chaining.</returns>
    public static FileStorageBuilder UseAzureFiles(
        this FileStorageBuilder builder,
        string locationName,
        string connectionString,
        string shareName,
        bool ensureShare = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ProviderFactory = (sp) => new AzureFilesFileStorageProvider(locationName, connectionString, shareName, ensureShare);
        return builder;
    }
}