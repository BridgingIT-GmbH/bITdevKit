# Presentation Configuration

> Compose JSON, Azure, and environment-variable configuration providers in the devkit host order.

[TOC]

## Overview

Presentation configuration adds one standard provider sequence to web and generic hosts. It loads devkit JSON settings, can connect to Azure Key Vault and Azure App Configuration, and applies environment variables last.

The feature wraps the normal `Microsoft.Extensions.Configuration` providers. The resulting values remain available through `IConfiguration` and the .NET options pattern.

## Challenges

A modular application can have settings from the host, module JSON files, a secret store, centralized configuration, and deployment environment variables. Each host must add those sources in a deliberate order because later providers replace earlier values for the same key.

Azure provider setup also needs bootstrap values before it can connect. The application must first read the vault name, endpoint, connection string, and managed identity client ID from an earlier source.

## Solution

`ConfigureAppConfiguration(...)` and the devkit builder's `AddConfiguration()` method add the providers in this order:

1. base JSON files;
2. environment-specific JSON files;
3. Azure Key Vault when configured;
4. Azure App Configuration when configured;
5. environment variables.

The lower-level extension methods let a host add only the provider that it needs. Azure providers build the configuration collected so far to read their bootstrap settings.

## Key Features

- One provider order for web and generic devkit hosts
- Base and environment-specific JSON discovery
- Module settings files with names such as `Catalog.appsettings.json`
- Azure Key Vault through `DefaultAzureCredential`
- Optional user-assigned managed identity for Key Vault
- Azure App Configuration through a connection string or managed identity endpoint
- Environment-variable overrides
- Fluent `AddConfiguration()` and `WithConfiguration()` aliases
- Provider-specific registration methods for custom host composition

## Architecture

The public entry points are:

- `AddConfiguration()` for `IDevKitApplicationBuilder` and `IDevKitHostApplicationBuilder`;
- `ConfigureAppConfiguration(...)` for `IHostBuilder`;
- `AddJsonFileConfigurationProvider(...)`;
- `AddAzureKeyVaultProvider(...)`;
- `AddAzureAppConfigurationProvider(...)`;
- `AddEnvironmentVariablesProvider(...)`.

The web devkit builder delegates to its stored `IHostBuilder`. The generic devkit builder adds the same providers directly to its `IConfigurationBuilder`.

Provider precedence follows registration order. Environment variables have the highest precedence among providers added by this feature.

## Use Cases

Use Presentation configuration when a host follows the devkit convention for module JSON files and optional Azure configuration. It gives web and worker hosts the same provider sequence.

Use the individual provider extensions when the host needs only part of that sequence. Use the underlying .NET configuration APIs when the application needs another provider, custom selection rules, key rewriting, reload behavior, or an explicit file order.

Do not put Azure App Configuration connection strings, managed identity credentials, or Key Vault secrets in committed JSON files. Supply bootstrap credentials through the deployment environment or another protected source.

## Basic Usage

Add the standard providers to a devkit web host:

```csharp
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;

var builder = DevKitWebApplication.CreateBuilder(args)
	.AddConfiguration();

var app = builder.Build();

app.MapGet("/configuration-check", () => Results.Ok(new
{
	Environment = builder.Environment.EnvironmentName,
	Region = builder.Configuration["Application:Region"]
}));

app.Run();
```

Set an override before starting the process:

```powershell
$env:Application__Region = 'eu-central'
dotnet run
```

`GET /configuration-check` returns `Region` as `eu-central`. The double underscore maps the environment variable to the `Application:Region` configuration key.

## Provider order

The combined registration calls the providers in this order:

```csharp
configuration
	.AddJsonFileConfigurationProvider(environment)
	.AddAzureKeyVaultProvider(environment)
	.AddAzureAppConfigurationProvider(environment)
	.AddEnvironmentVariablesProvider();
```

A later provider replaces an earlier value for the same key. For example, an environment variable replaces the value loaded from JSON, Key Vault, or Azure App Configuration.

Within the JSON provider, base files load before files for the selected environment. The implementation enumerates matching files from the parent directory of `AppContext.BaseDirectory`. It does not sort matches, mark files as optional, or enable reload-on-change. Do not depend on precedence between two base files or between two environment files that contain the same key.

## JSON file discovery

`AddJsonFileConfigurationProvider(environment)` loads files with these patterns:

- `*appsettings.json`;
- `*appsettings.{environment}.json` when an environment is supplied.

Names that end with `.appsettings.json` or `.appsettings.{environment}.json` are logged as module settings files. The naming check changes only the log message. All matching files use the normal JSON configuration provider.

The caller is responsible for placing required files in the directory searched at runtime. Missing files do not appear in the enumeration. A file that is found but cannot be loaded causes the underlying JSON provider to fail.

## Azure Key Vault

Key Vault registration reads these bootstrap keys:

```json
{
	"AzureKeyVault": {
		"Enabled": true,
		"Name": "inventory-production",
		"ManagedIdentityClientId": "00000000-0000-0000-0000-000000000000"
	}
}
```

`AzureKeyVault:Name` enables the provider unless `AzureKeyVault:Enabled` is `false`. The provider connects to `https://{name}.vault.azure.net/`.

When `ManagedIdentityClientId` is empty, the provider uses `DefaultAzureCredential`. When it is set, the provider passes it to `DefaultAzureCredentialOptions.ManagedIdentityClientId` for a user-assigned managed identity.

The feature does not configure secret-name transformation or refresh behavior. Those remain the defaults of the Azure Key Vault configuration provider.

## Azure App Configuration

Azure App Configuration supports two connection modes. A connection string takes precedence over an endpoint.

Connection-string mode reads:

```json
{
	"AzureAppConfig": {
		"Enabled": true,
		"ConnectionString": "<supply through a protected source>"
	}
}
```

Managed-identity mode reads:

```json
{
	"AzureAppConfig": {
		"Enabled": true,
		"Endpoint": "https://inventory.azconfig.io",
		"ManagedIdentityClientId": "00000000-0000-0000-0000-000000000000"
	}
}
```

Endpoint mode creates a `ManagedIdentityCredential` for the supplied user-assigned client ID. Supply `AzureAppConfig:ManagedIdentityClientId` with this mode.

The provider is skipped when both connection values are empty or when `AzureAppConfig:Enabled` is `false`. The feature does not configure selectors, labels, refresh registration, or feature flags.

## Host registration variants

Use the fluent devkit host API for a web application:

```csharp
var builder = DevKitWebApplication.CreateBuilder(args)
	.AddConfiguration(environment: "Development");
```

The same method works on `DevKitApplication` for a generic host:

```csharp
var builder = DevKitApplication.CreateBuilder(args)
	.AddConfiguration();
```

For a standard `WebApplicationBuilder`, configure its generic host:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureAppConfiguration();
```

`WithConfiguration(...)` is an alias for `AddConfiguration(...)` on both devkit builder types.

## Environment variables

The combined registration calls `AddEnvironmentVariablesProvider()` without a prefix. It reads all environment variables using the normal .NET key mapping.

For a bounded prefix, call the provider directly:

```csharp
builder.Configuration.AddEnvironmentVariablesProvider("INVENTORY_");
```

With that prefix, `INVENTORY_Application__Region` maps to `Application:Region`.

## Related documentation

- [Presentation Host](./features-presentation.md) covers the devkit host builders and starter extensions.
- [Modules](./features-modules.md) covers module registration with the composed configuration.
- [Common Options Builders](./common-options-builders.md) covers devkit option-builder conventions.
