---
title: Templates
hide:
  - footer
---

<!-- markdownlint-disable-file MD033 -->

!!! path-a "Path A: new solution"

    [**01** Walkthrough](getting-started-template.md)
    [**02** Templates reference](templates.md){ aria-current="page" }
    <span class="switch">Have an app already? Use [Path B: existing app](getting-started-existing-app.md).</span>
    { .path-pager }

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

- .NET 10 SDK or later

Install from NuGet:

```bash
dotnet new install BridgingIT.DevKit.Templates
```

Verify installation:

```bash
dotnet new list
```

!!! verify "Check it"

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

<div class="solution-tree" role="img" aria-label="Solution template structure">
  <div><span class="br"></span><span class="dir">SolutionName/</span></div>
  <div><span class="br">├─ </span><span class="dir">src/Modules/ModuleName/</span></div>
  <div><span class="br">│  ├─ </span><span class="dir">ModuleName.Application</span><span class="fill"></span><span class="layer layer-application">application</span></div>
  <div><span class="br">│  ├─ </span><span class="dir">ModuleName.Domain</span><span class="fill"></span><span class="layer layer-domain">domain</span></div>
  <div><span class="br">│  ├─ </span><span class="dir">ModuleName.Infrastructure</span><span class="fill"></span><span class="layer layer-infrastructure">infrastructure</span></div>
  <div><span class="br">│  └─ </span><span class="dir">ModuleName.Presentation</span><span class="fill"></span><span class="layer layer-presentation">presentation</span></div>
  <div><span class="br">├─ </span><span class="dir">src/Presentation.Web.Server</span><span class="fill"></span><span class="layer layer-host">host</span></div>
  <div><span class="br">├─ </span><span class="dir">tests/ModuleName/</span></div>
  <div><span class="br">│  ├─ </span><span class="dir">ModuleName.IntegrationTests</span><span class="fill"></span><span class="layer layer-tests">tests</span></div>
  <div><span class="br">│  └─ </span><span class="dir">ModuleName.UnitTests</span><span class="fill"></span><span class="layer layer-tests">tests</span></div>
  <div><span class="br">└─ </span><span class="dir">SolutionName.slnx</span></div>
</div>

## Onboarding path

Use this sequence:

<ol class="flow-strip">
  <li><a href="../getting-started-template/"><b>01</b><span>Read the walkthrough</span></a></li>
  <li><a href="#install-the-templates"><b>02</b><span>Install templates</span><code>dotnet new install</code></a></li>
  <li><a href="#create-a-new-solution"><b>03</b><span>Create a solution or module</span><code>dotnet new bdksolution</code></a></li>
  <li><a href="../reference/"><b>04</b><span>Continue to the docs</span><code>reference/</code></a></li>
</ol>

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
