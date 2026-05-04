![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/bridgingIT/bITdevKit/github-actions.yml?style=flat)](https://github.com/bridgingIT/bITdevKit/actions/workflows/github-actions.yml)
[![NuGet](https://img.shields.io/nuget/v/BridgingIT.DevKit.Common.Utilities?style=flat-square&label=nuget%20packages)](https://www.nuget.org/packages?q=bitdevkit)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=bitdevkit&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=bitdevkit)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE)

> Empowering developers with modular components for modern application development, centered around Domain-Driven Design principles.

<!-- TOC -->

- [](#)
  - [Introduction](#introduction)
  - [Documentation](#documentation)
  - [Features](#features)
    - [Common Infrastructure](#common-infrastructure)
    - [Core Domain and Application](#core-domain-and-application)
    - [Execution, Messaging and Modularity](#execution-messaging-and-modularity)
    - [Security and Access](#security-and-access)
    - [Presentation and Host](#presentation-and-host)
    - [Storage, Scheduling and Utilities](#storage-scheduling-and-utilities)
    - [Testing and Test Utilities](#testing-and-test-utilities)
  - [Libraries used (excerpt):](#libraries-used-excerpt)
  - [Example projects](#example-projects)
  - [Performance Benchmarks](#performance-benchmarks)
  - [GitHub Pages](#github-pages)
    - [Source structure](#source-structure)
    - [Which Markdown files get into the site](#which-markdown-files-get-into-the-site)
    - [Add a new documentation page](#add-a-new-documentation-page)
    - [Local preview and build](#local-preview-and-build)
    - [Publishing through GitHub Actions](#publishing-through-github-actions)
    - [Typical workflow for docs updates](#typical-workflow-for-docs-updates)
  - [Collaboration](#collaboration)
  - [Commit Policy](#commit-policy)
  - [License](#license)

<!-- TOC -->

## Introduction

The `bITdevKit` provides modular building blocks for modern .NET applications. It is centered around
clean architecture, Domain-Driven Design, modular vertical slices and reusable infrastructure for
real-world application concerns such as requests, messaging, queueing, storage, scheduling and
presentation.

This repository contains the full source code, supporting docs and several example applications in
`./examples` that show how the framework can be composed in practice. The components are published as
[NuGet packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [CHANGELOG](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/CHANGELOG.md).

## Documentation

The public documentation site is available at
[bridgingit-gmbh.github.io/bITdevKit](https://bridgingit-gmbh.github.io/bITdevKit/).

The best entry point into the current documentation set is the
[Documentation](./docs/INDEX.md) and [Introduction to DDD in bITdevKit](./docs/introduction-ddd-guide.md).

## Features

### Common Infrastructure

- [Common Extensions](./docs/common-extensions.md)
- [Common Utilities](./docs/common-utilities.md)
- [Common Serialization](./docs/common-serialization.md)
- [Common Options Builders](./docs/common-options-builders.md)
- [Common Mapping](./docs/common-mapping.md)
- [Common Caching](./docs/common-caching.md)
- [Common Observability Tracing](./docs/common-observability-tracing.md)

### Core Domain and Application

- [Domain](./docs/features-domain.md)
- [Domain Events](./docs/features-domain-events.md)
- [Event Sourcing](./docs/features-event-sourcing.md)
- [Domain Repositories](./docs/features-domain-repositories.md)
- [Domain Specifications](./docs/features-domain-specifications.md)
- [ActiveEntity](./docs/features-domain-activeentity.md)
- [Domain Policies](./docs/features-domain-policies.md)
- [Rules](./docs/features-rules.md)
- [Results](./docs/features-results.md)
- [Application Commands and Queries](./docs/features-application-commands-queries.md)
- [Application Events](./docs/features-application-events.md)
- [DataPorter](./docs/features-application-dataporter.md)

### Execution, Messaging and Modularity

- [Requester and Notifier](./docs/features-requester-notifier.md)
- [Messaging](./docs/features-messaging.md)
- [Queueing](./docs/features-queueing.md)
- [Notifications](./docs/features-notifications.md)
- [Modules](./docs/features-modules.md)
- [Pipelines](./docs/features-pipelines.md)
- [Filtering](./docs/features-filtering.md)
- [Extensions](./docs/features-extensions.md)

### Security and Access

- [Entity Permissions](./docs/features-entitypermissions.md)
- [Fake Identity Provider](./docs/features-identityprovider.md)

### Presentation and Host

- [Presentation Endpoints](./docs/features-presentation-endpoints.md)
- [Console Commands](./docs/features-presentation-console-commands.md)
- [CORS Configuration](./docs/features-presentation-cors.md)
- [Exception Handling](./docs/features-presentation-exception-handling.md)
- [AppState](./docs/features-presentation-appstate.md)

### Storage, Scheduling and Utilities

- [StartupTasks](./docs/features-startuptasks.md)
- [JobScheduling](./docs/features-jobscheduling.md)
- [DocumentStorage](./docs/features-storage-documents.md)
- [FileStorage](./docs/features-storage-files.md)
- [Storage Monitoring](./docs/features-storage-monitoring.md)
- [Log Entries](./docs/features-log-entries.md)

### Testing and Test Utilities

- [Fake Authentication for Integration Tests](./docs/testing-fake-authentication.md)
- [Testing Common XUnit](./docs/testing-common-xunit.md)

## Libraries used (excerpt):

- [Xunit](https://github.com/xunit/xunit)
- [EnsureThat](https://github.com/danielwertheim/Ensure.That)
- [Mapster](https://github.com/MapsterMapper/Mapster)
- [FluentValidation](https://github.com/FluentValidation/FluentValidation)
- [FluentAssertions](https://github.com/fluentassertions/fluentassertions)
- [Humanizer](https://github.com/Humanizr/Humanizer)
- [Polly](https://github.com/App-vNext/Polly)
- [Scrutor](https://github.com/khellang/Scrutor)
- [Serilog](https://github.com/serilog/serilog)
- [Quartz](https://github.com/quartz-scheduler/quartz)
- [Shouldly](https://github.com/shouldly/shouldly)
- [Testcontainer](https://github.com/testcontainers)
- EntityFramework
- Azure Storage
- Azure ServiceBus
- Azure CosmosDb
- RabbitMQ

## Example projects

- [GettingStarted (Basic)](https://github.com/bridgingIT/bITdevKit.Examples.GettingStarted)
- [BookFiesta (DDD)](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.BookFiesta)
- [EventStore (CQRS)](https://github.com/bridgingit/bitdevkit/examples)
- [WeatherForecast](https://github.com/bridgingit/bitdevkit/examples)

## Performance Benchmarks

The bITDevKit includes a dedicated benchmark project using [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure the performance of core components such as the `Requester` (CQRS/MediatR alternative).

**How to run benchmarks:**

```sh
dotnet run -c Release --project benchmarks/Common.Benchmarks/Common.Benchmarks.csproj
```

- This will execute all benchmarks and generate detailed reports in the `BenchmarkDotNet.Artifacts/results/` directory (HTML, Markdown, CSV).
- The default benchmark covers the baseline request/response pipeline. You can extend the benchmarks to cover additional scenarios and compare different configurations.

**Why benchmarks?**

- Ensure high performance and low allocations for core infrastructure.
- Detect regressions and compare with other libraries (e.g., MediatR).
- Guide optimization and architectural decisions.

For more details, see the `Common.Benchmarks` project in the `benchmarks/` folder.

## GitHub Pages

The repository includes a MkDocs-based GitHub Pages site with:

- a landing page under `docs/site/`
- curated technical docs synced from `./docs`

Public site:
[https://bridgingit-gmbh.github.io/bITdevKit/](https://bridgingit-gmbh.github.io/bITdevKit/)

### Source structure

- `docs/site/`
  - the site-specific source pages such as the landing page, getting started pages, templates page, styling, and helper scripts
- `docs/`
  - the framework documentation source that is selectively imported into the public site
- `.github/pages/`
  - the generated static MkDocs output

### Which Markdown files get into the site

The sync step currently imports these files from `./docs` into the public site:

- `INDEX.md`
- `introduction-ddd-guide.md`
- `common-*.md`
- `features-*.md`
- `testing-*.md`

The following are intentionally excluded from GitHub Pages:

- `docs/adr/`
- `docs/presentations/`
- `docs/specs/`
- `src/**`

### Add a new documentation page

For a new Markdown page from `./docs` to appear in GitHub Pages:

1. Add or update the Markdown file under `./docs`.
2. Make sure its filename matches one of the currently imported patterns above.
3. If it should appear in navigation, add it to [mkdocs.yml](./mkdocs.yml) under the appropriate section.
4. Regenerate the site locally or push to `main` so GitHub Actions publishes it.

### Local preview and build

Preview the full site locally with Docker:

```powershell
pwsh -File ./docs/site/scripts/serve-pages.ps1
```

or use the VS Code Tasks for the same command.
Build the full static site locally:

```powershell
pwsh -File ./docs/site/scripts/build-pages.ps1
```

That build command performs these steps:

1. Synchronize the selected public docs from `./docs` into `docs/site/reference/`
2. Build the MkDocs site in Docker
3. Write the generated static site to `./.github/pages/`

### Publishing through GitHub Actions

The Pages workflow is defined in [pages.yml](./.github/workflows/pages.yml).

On each push to `main`, it:

1. checks out the repository
2. runs `./docs/site/scripts/build-pages.ps1`
3. verifies that `./.github/pages/index.html` was generated
4. publishes the generated site to the `gh-pages` branch

### Typical workflow for docs updates

Typical documentation workflow:

1. Edit or add Markdown under `./docs`
2. If needed, update [mkdocs.yml](./mkdocs.yml) so the page is reachable in navigation
3. Run `pwsh -File ./docs/site/scripts/serve-pages.ps1` to preview the result
4. Run `pwsh -File ./docs/site/scripts/build-pages.ps1` to generate the final static output
5. Commit and push to `main` to publish via GitHub Actions

## Collaboration

Simply create a pull request with your ideas or contact us.
Please refer to the [Contribution Guide](./CONTRIBUTION.md) for more information.

## Commit Policy

To keep history clear and searchable, contributions should use semantic commit messages based on the Conventional Commits specification.

Format:

`<type>[optional scope]: <description>`

Examples:

- `feat(modules): add startup task registration`
- `fix(jobs): prevent duplicate scheduling on startup`
- `docs(readme): clarify contribution workflow`

Policy:

- Use the Conventional Commits format for all repository contributions
- Keep the description in imperative mood and under 72 characters
- Prefer one logical change per commit
- Review `git diff --staged` before committing, or `git diff` if nothing is staged
- Check `git status --porcelain` to confirm the exact files included
- Never commit secrets or credentials

---
## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.