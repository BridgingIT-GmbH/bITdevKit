// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.Configuration;

using Hosting;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureAppConfiguration(this IHostBuilder builder, string environment = null)
    {
        return builder.ConfigureAppConfiguration((ctx, c) =>
        {
            c.AddJsonFileConfigurationProvider(environment ?? ctx.HostingEnvironment.EnvironmentName);
            c.AddAzureKeyVaultProvider(environment ??
                ctx.HostingEnvironment.EnvironmentName); // enable/configure in appsettings.json
            c.AddAzureAppConfigurationProvider(environment ??
                ctx.HostingEnvironment.EnvironmentName); // enable/configure in appsettings.json
            c.AddEnvironmentVariablesProvider();
        });
    }
}