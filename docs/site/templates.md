---
title: Templates
---

# Templates

`bITdevKit` provides .NET templates to help you scaffold new solutions and modules using the kit's
architectural conventions.

These templates are the fastest way to start a new codebase that already follows the intended
structure around onion architecture, modular vertical slices, and a clear separation of concerns.

## Available templates

[Templates package on NuGet.org](https://www.nuget.org/packages/BridgingIT.DevKit.Templates)

### bITdevKit Solution

Short name: `bdksolution`

Creates a complete solution with an initial module and the basic project structure for a
`bITdevKit`-based application.

### bITdevKit Module

Short name: `bdkmodule`

Adds a new functional module to an existing solution. Each module follows the onion architecture
pattern with separate projects for the major concerns.

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

After adding a new module, a few manual changes are still required.

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

## Recommended onboarding path

The best practical sequence is:

1. Start with the [GettingStarted example](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted).
2. Read the [Getting Started](getting-started.md) page on this site.
3. Use the templates when you are ready to scaffold your own solution or add modules to an existing one.
4. Continue into the [Documentation](reference/index.md) for the deeper framework concepts behind the generated structure.

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
