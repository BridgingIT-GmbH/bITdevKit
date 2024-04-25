// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.IO;
using BridgingIT.DevKit.Common;
using global::Azure.Identity;
using Serilog;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddEnvironmentVariablesProvider(this IConfigurationBuilder builder, string prefix = null)
    {
        return builder.AddEnvironmentVariables(prefix);
    }

    public static IConfigurationBuilder AddJsonFileConfigurationProvider(this IConfigurationBuilder builder, string environment = null)
    {
        foreach (var file in GetFiles("*appsettings.json"))
        {
            if (file.EndsWith(".appsettings.json", StringComparison.OrdinalIgnoreCase)) // detect module config
            {
                Log.Logger.Information("{LogKey} settings (module={ModuleName}, file={ModuleSettingsFile}), env={HostingEnvironment})", ModuleConstants.LogKey, file.SliceFromLast("\\").SliceTill(".").ToLowerInvariant(), file.SliceFromLast("\\"), environment);
            }
            else
            {
                Log.Logger.Information("{LogKey} settings (file={SettingsFile}, env={HostingEnvironment})", ModuleConstants.LogKey, file.SliceFromLast("\\"), environment);
            }

            builder.AddJsonFile(file);
        }

        if (!environment.IsNullOrEmpty())
        {
            foreach (var file in GetFiles($"*appsettings.{environment}.json"))
            {
                if (file.EndsWith($".appsettings.{environment}.json", StringComparison.OrdinalIgnoreCase)) // detect module config
                {
                    Log.Logger.Information("{LogKey} settings (module={ModuleName}, file={ModuleSettingsFile}), env={HostingEnvironment})", ModuleConstants.LogKey, file.SliceFromLast("\\").SliceTill(".").ToLowerInvariant(), file.SliceFromLast("\\"), environment);
                }
                else
                {
                    Log.Logger.Information("{LogKey} settings (file={SettingsFile}, env={HostingEnvironment})", ModuleConstants.LogKey, file.SliceFromLast("\\"), environment);
                }

                builder.AddJsonFile(file);
            }
        }

        return builder;

        static IEnumerable<string> GetFiles(string pattern)
                => Directory.EnumerateFiles(Directory.GetParent(AppContext.BaseDirectory).FullName, pattern);
    }

    public static IConfigurationBuilder AddAzureKeyVaultProvider(this IConfigurationBuilder builder, string environment = null)
    {
        // add azure keyvault provider
        var tmpCfg = builder.Build(); // must materialize the config files first
        var vaultName = tmpCfg["AzureKeyVault:Name"];

        if (!string.IsNullOrEmpty(vaultName) && !tmpCfg["AzureKeyVault:Enabled"].SafeEquals("False"))
        {
            Log.Logger.Information("{LogKey} settings (azureKeyVault={AzureKeyVaultName}, env={HostingEnvironment})", ModuleConstants.LogKey, vaultName, environment);

            var managedIdentityClientId = tmpCfg["AzureKeyVault:ManagedIdentityClientId"];
            if (string.IsNullOrEmpty(managedIdentityClientId))
            {
                builder.AddAzureKeyVault(
                    new Uri($"https://{vaultName}.vault.azure.net/"),
                    new DefaultAzureCredential()); // https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-6.0#use-managed-identities-for-azure-resources
            }
            else
            {
                builder.AddAzureKeyVault(new Uri($"https://{vaultName}.vault.azure.net/"),
                    new DefaultAzureCredential(
                        new DefaultAzureCredentialOptions
                        {
                            ManagedIdentityClientId = managedIdentityClientId
                        }));
            }
        }

        return builder;
    }

    public static IConfigurationBuilder AddAzureAppConfigurationProvider(this IConfigurationBuilder builder, string environment = null)
    {
        var tmpCfg = builder.Build(); // must materialize the config files first
        // add azure app configuration provider
        var azureAppConfigConnectionString = tmpCfg["AzureAppConfig:ConnectionString"];
        var azureAppConfigEndpoint = tmpCfg["AzureAppConfig:Endpoint"];
        if (!string.IsNullOrEmpty(azureAppConfigConnectionString) && !tmpCfg["AzureAppConfig:Enabled"].SafeEquals("False"))
        {
            // https://learn.microsoft.com/en-us/azure/azure-app-configuration/quickstart-aspnet-core-app?tabs=core6x
            Log.Logger.Information("{LogKey} settings (azureAppConfiguration={AzureAppConfigEnabled}, env={HostingEnvironment})", ModuleConstants.LogKey, true, environment);

            builder.AddAzureAppConfiguration(azureAppConfigConnectionString);
        }
        else if (!string.IsNullOrEmpty(azureAppConfigEndpoint) && !tmpCfg["AzureAppConfig:Enabled"].SafeEquals("False"))
        {
            // https://learn.microsoft.com/en-us/azure/azure-app-configuration/howto-integrate-azure-managed-service-identity?tabs=core6x&pivots=framework-dotnet
            var managedIdentityClientId = tmpCfg["AzureAppConfig:ManagedIdentityClientId"];

            Log.Logger.Information("{LogKey} settings (azureAppConfiguration={AzureAppConfigEnabled}, env={HostingEnvironment})", ModuleConstants.LogKey, true, environment);

            builder.AddAzureAppConfiguration(o =>
                o.Connect(new Uri(azureAppConfigEndpoint),
                string.IsNullOrEmpty(managedIdentityClientId) ? new ManagedIdentityCredential() : new ManagedIdentityCredential(managedIdentityClientId)));
        }

        return builder;
    }
}