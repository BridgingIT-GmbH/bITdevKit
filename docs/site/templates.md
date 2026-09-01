---
title: Templates
---

# Templates

`bITdevKit` provides .NET templates that create solutions and modules using the kit's architectural conventions.

The generated structure follows onion architecture and groups each module into separate layer projects.

## Available templates

[Templates package on NuGet.org](https://www.nuget.org/packages/BridgingIT.DevKit.Templates)

### bITdevKit Solution

Short name: `bdksolution`

Creates a solution with an initial module and the project structure for a `bITdevKit`-based application.

### bITdevKit Module

Short name: `bdkmodule`

Adds a module to an existing solution. The template creates separate application, domain, infrastructure, presentation, and test projects.

Generated module structure:

- `[ModuleName].Application.csproj`
- `[ModuleName].Domain.csproj`
- `[ModuleName].Infrastructure.csproj`
- `[ModuleName].Presentation.csproj`
- `[ModuleName].IntegrationTests.csproj`
- `[ModuleName].UnitTests.csproj`

## Install the templates

Prerequisite:

- .NET 9 SDK or later

Install from NuGet:

```bash
dotnet new install BridgingIT.DevKit.Templates
```

Verify installation:

```bash
dotnet new list
```

You should see entries for the `bITdevKit Solution` and `bITdevKit Module` templates.

## Create a new solution

Use the solution template to scaffold a new application:

```bash
dotnet new bdksolution --SolutionName SolutionName --ModuleName Core --allow-scripts yes -o ./projects/SolutionName
```

Parameters:

- `--SolutionName`: the name of the solution
- `--ModuleName`: the name of the initial module
- `-o`: output directory for the solution

## Add a new module

Inside an existing solution, add another module with:

```bash
dotnet new bdkmodule --ModuleName ModuleName -o src/Modules/ModuleName --allow-scripts yes
```

Parameters:

- `--ModuleName`: the name of the new module
- `-o`: output directory for the module

After generation, the template adds the new projects to the solution file automatically.

## Manual follow-up after module creation

After adding a module, register it and add its configuration.

Register the new module in `Program.cs`:

```csharp
builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<[ModuleName]Module>();
```

Add the module configuration in `appsettings.json`:

```json
"Modules": {
  "[ModuleName]": {
    "Enabled": true,
    "ConnectionStrings": {
      "Default": "ConnectionStringHere"
    }
  }
}
```

## Project structure

The solution template creates a structure like this:

```text
SolutionName/
├── src/
│   ├── Modules/
│   │   ├── ModuleName/
│   │   │   ├── ModuleName.Application/
│   │   │   ├── ModuleName.Domain/
│   │   │   ├── ModuleName.Infrastructure/
│   │   │   └── ModuleName.Presentation/
│   └── Presentation.Web.Server/
├── tests/
│   └── ModuleName/
│       ├── ModuleName.IntegrationTests/
│       └── ModuleName.UnitTests/
└── SolutionName.slnx
```

## Onboarding path

Use this sequence:

1. Start with the [GettingStarted example](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted).
2. Read the [Getting Started](getting-started.md) page on this site.
3. Use the templates to create a solution or add modules to an existing one.
4. Continue to the [Documentation](reference/index.md) for the APIs used by the generated projects.

## Update or uninstall

Update to the latest template version:

```bash
dotnet new uninstall BridgingIT.DevKit.Templates
dotnet new install BridgingIT.DevKit.Templates
```

Uninstall:

```bash
dotnet new uninstall BridgingIT.DevKit.Templates
```
