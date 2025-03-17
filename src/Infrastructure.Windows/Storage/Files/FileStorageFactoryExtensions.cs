// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Windows.Storage;

using BridgingIT.DevKit.Application.Storage;
using static BridgingIT.DevKit.Application.Storage.FileStorageFactory;

public static class FileStorageFactoryExtensions
{
    /// <summary>
    /// Configures a network file storage provider with credentials.
    /// </summary>
    /// <param name="factory">The file storage factory instance.</param>
    /// <param name="providerName">The unique name of the provider.</param>
    /// <param name="configure">Action to configure the provider using a builder.</param>
    /// <returns>The factory for method chaining.</returns>
    public static IFileStorageFactory RegisterNetworkFileStorageProvider(
        this FileStorageFactory factory,
        string providerName,
        Action<FileStorageBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(configure);

        return factory.RegisterProvider(providerName, configure);
    }

    /// <summary>
    /// Configures a network file storage provider with credentials.
    /// </summary>
    /// <param name="builder">The file storage builder instance.</param>
    /// <param name="locationName">The logical name for the storage location.</param>
    /// <param name="rootPath">The UNC path to the network share (e.g., \\server\share).</param>
    /// <param name="username">The username for authentication (e.g., domain\username).</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="domain">The domain for authentication (optional, defaults to the local machine).</param>
    /// <returns>The builder for method chaining.</returns>
    public static FileStorageBuilder UseWindowsNetwork(
        this FileStorageBuilder builder,
        string locationName,
        string rootPath,
        string username,
        string password,
        string domain = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ProviderFactory = (sp) => new NetworkFileStorageProvider(locationName, rootPath, username, password, domain);
        return builder;
    }
}