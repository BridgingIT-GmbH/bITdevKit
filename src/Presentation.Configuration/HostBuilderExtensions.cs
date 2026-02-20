// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.Configuration;

using Hosting;

public static class HostBuilderExtensions
{
    /// <summary>
    /// Configures the application to use JSON file, Azure Key Vault, Azure App Configuration and environment variable
    /// providers for loading configuration settings.
    /// </summary>
    /// <remarks>This method adds multiple configuration providers to the host builder, enabling support for
    /// environment-specific settings, secure secrets from Azure Key Vault, centralized configuration from Azure App
    /// Configuration, and environment variables. Providers are enabled or configured based on settings in
    /// appsettings.json and the specified environment.</remarks>
    /// <param name="builder">The host builder to configure with additional configuration providers.</param>
    /// <param name="environment">The environment name to use when loading configuration sources. If null, the current hosting environment name is
    /// used.</param>
    /// <returns>The host builder with the specified configuration providers added.</returns>
    public static IHostBuilder ConfigureAppConfiguration(this IHostBuilder builder, string environment = null)
    {
        return builder.ConfigureAppConfiguration((ctx, c) =>
        {
            c.AddJsonFileConfigurationProvider(environment ?? ctx.HostingEnvironment.EnvironmentName);
            c.AddAzureKeyVaultProvider(environment ?? ctx.HostingEnvironment.EnvironmentName); // enable/configure in appsettings.json
            c.AddAzureAppConfigurationProvider(environment ?? ctx.HostingEnvironment.EnvironmentName); // enable/configure in appsettings.json
            c.AddEnvironmentVariablesProvider();
        });
    }
}