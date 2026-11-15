---
title: Create a solution from templates
hide:
  - footer
---

<!-- markdownlint-disable-file MD033 -->

!!! path-a "Path A: new solution"

    [**01** Walkthrough](getting-started-template.md){ aria-current="page" }
    [**02** Templates reference](templates.md)
    <span class="switch">Have an app already? Use [Path B: existing app](getting-started-existing-app.md).</span>
    { .path-pager }

# Create a solution from templates

Create a `bITdevKit` solution, build it, and confirm that the generated projects compile.

<ol class="flow-strip">
  <li><a href="#step-1"><b>01</b><span>Check the SDK</span><code>dotnet --version</code></a></li>
  <li><a href="#step-2"><b>02</b><span>Install templates</span><code>dotnet new install</code></a></li>
  <li><a href="#step-3"><b>03</b><span>Create a solution</span><code>dotnet new bdksolution</code></a></li>
  <li><a href="#step-4"><b>04</b><span>Build</span><code>dotnet build</code></a></li>
</ol>

## Before you start { #step-1 .tutorial-step data-step="01" data-toc-label="01 Before you start" }

Install the .NET 10 SDK or a later version. Check the installed version:

```powershell
dotnet --version
```

## Install the templates { #step-2 .tutorial-step data-step="02" data-toc-label="02 Install the templates" }

Install the templates from NuGet:

```powershell
dotnet new install BridgingIT.DevKit.Templates
```

Confirm that the solution and module templates are available:

```powershell
dotnet new list bITdevKit
```

The output lists `bITdevKit Solution` and `bITdevKit Module`.

## Create a solution { #step-3 .tutorial-step data-step="03" data-toc-label="03 Create a solution" }

Create a solution named `SolutionName` with an initial `Core` module:

```powershell
dotnet new bdksolution --SolutionName SolutionName --ModuleName Core --allow-scripts yes -o ./projects/SolutionName
```

The command creates `./projects/SolutionName/SolutionName.slnx`. The generated module has application, domain, infrastructure, presentation, integration-test, and unit-test projects.

The solution template already installs and configures bdk MCP. Your agent can use the DevKit documentation, guidance, and API tools without a separate setup. See [AI Agent Support](agent-support.md).

<div class="solution-tree" role="img" aria-label="Generated solution tree">
  <div><span class="br"></span><span class="dir">projects/SolutionName/</span></div>
  <div><span class="br">├─ </span><span class="dir">SolutionName.slnx</span></div>
  <div><span class="br">├─ </span><span class="dir">src/Modules/Core/</span></div>
  <div><span class="br">│  ├─ </span><span class="dir">Core.Application</span><span class="fill"></span><span class="layer layer-application">application</span></div>
  <div><span class="br">│  ├─ </span><span class="dir">Core.Domain</span><span class="fill"></span><span class="layer layer-domain">domain</span></div>
  <div><span class="br">│  ├─ </span><span class="dir">Core.Infrastructure</span><span class="fill"></span><span class="layer layer-infrastructure">infrastructure</span></div>
  <div><span class="br">│  └─ </span><span class="dir">Core.Presentation</span><span class="fill"></span><span class="layer layer-presentation">presentation</span></div>
  <div><span class="br">├─ </span><span class="dir">src/Presentation.Web.Server</span><span class="fill"></span><span class="layer layer-host">host</span></div>
  <div><span class="br">└─ </span><span class="dir">tests/Modules/Core/</span></div>
  <div><span class="br">   ├─ </span><span class="dir">Core.Benchmarks</span><span class="fill"></span><span class="layer layer-tests">benchmarks</span></div>
  <div><span class="br">   ├─ </span><span class="dir">Core.IntegrationTests</span><span class="fill"></span><span class="layer layer-tests">tests</span></div>
  <div><span class="br">   └─ </span><span class="dir">Core.UnitTests</span><span class="fill"></span><span class="layer layer-tests">tests</span></div>
</div>

## Build the generated project { #step-4 .tutorial-step data-step="04" data-toc-label="04 Build" }

Build the solution from the directory where you ran the template command:

```powershell
dotnet build ./projects/SolutionName/SolutionName.slnx
```

A successful build ends with this result:

!!! verify "Check it"

    ```text
    Build succeeded.
        0 Error(s)
    ```

The generated solution is now ready for a domain model and an application workflow.

## Choose the next step { data-toc-label="Next step" }

<nav class="next-step-strip" aria-label="Next steps">
  <a href="../architecture/"><small>understand the layers</small>Architecture</a>
  <a href="../docs/"><small>find a feature guide</small>Documentation overview</a>
  <a href="../guides/"><small>decide between options</small>Guides</a>
  <a href="../getting-started-existing-app-features/"><small>explains each generated registration</small>Add features, Path B</a>
  <a href="https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted"><small>trace the same patterns</small>GettingStarted example</a>
</nav>

For template parameters, module generation, and configuration, use the [Templates](templates.md) reference.
