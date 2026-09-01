---
title: Quickstart
---

# Quickstart

Create a `bITdevKit` solution, build it, and confirm that the generated projects compile.

## Before you start

Install the .NET 9 SDK or a later version. Check the installed version:

```powershell
dotnet --version
```

## Install the templates

Install the templates from NuGet:

```powershell
dotnet new install BridgingIT.DevKit.Templates
```

Confirm that the solution and module templates are available:

```powershell
dotnet new list bITdevKit
```

The output lists `bITdevKit Solution` and `bITdevKit Module`.

## Create a solution

Create a solution named `SolutionName` with an initial `Core` module:

```powershell
dotnet new bdksolution --SolutionName SolutionName --ModuleName Core --allow-scripts yes -o ./projects/SolutionName
```

The command creates `./projects/SolutionName/SolutionName.slnx`. The generated module has application, domain, infrastructure, presentation, integration-test, and unit-test projects.

## Build the generated project

Build the solution from the directory where you ran the template command:

```powershell
dotnet build ./projects/SolutionName/SolutionName.slnx
```

A successful build ends with this result:

```text
Build succeeded.
    0 Error(s)
```

The generated solution is now ready for a domain model and an application workflow.

## Choose the next step

- Read [Architecture](architecture.md) to understand the layers and module boundaries.
- Open the [Documentation Overview](docs.md) to find the feature guides for your task.
- Trace the same patterns through the [GettingStarted example](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted).

For template parameters, module generation, and configuration, use the [Templates](templates.md) reference.
