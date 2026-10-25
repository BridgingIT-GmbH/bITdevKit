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
  - [CI/CD](#cicd)
  - [Continuous AI](#continuous-ai)
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

The documentation site is available at
[bridgingit-gmbh.github.io/bITdevKit](https://bridgingit-gmbh.github.io/bITdevKit/).

The best entry point into the current documentation set is the
[Documentation](./docs/INDEX.md) and [Introduction to DDD in bITdevKit](./docs/introduction-ddd-guide.md).

## Features

### Common Infrastructure

- <img class="bdk-doc-icon" src="docs/assets/features/common-extensions.svg" width="20" height="20" alt=""> [Common Extensions](./docs/common-extensions.md)
- <img class="bdk-doc-icon" src="docs/assets/features/common-utilities.svg" width="20" height="20" alt=""> [Common Utilities](./docs/common-utilities.md)
- <img class="bdk-doc-icon" src="docs/assets/features/serialization.svg" width="20" height="20" alt=""> [Common Serialization](./docs/common-serialization.md)
- <img class="bdk-doc-icon" src="docs/assets/features/options-builders.svg" width="20" height="20" alt=""> [Common Options Builders](./docs/common-options-builders.md)
- <img class="bdk-doc-icon" src="docs/assets/features/mapping.svg" width="20" height="20" alt=""> [Common Mapping](./docs/common-mapping.md)
- <img class="bdk-doc-icon" src="docs/assets/features/caching.svg" width="20" height="20" alt=""> [Common Caching](./docs/common-caching.md)
- <img class="bdk-doc-icon" src="docs/assets/features/tracing.svg" width="20" height="20" alt=""> [Common Observability Tracing](./docs/common-observability-tracing.md)
- <img class="bdk-doc-icon" src="docs/assets/features/metrics.svg" width="20" height="20" alt=""> [Metrics](./docs/features-metrics.md)

### Core Domain and Application

- <img class="bdk-doc-icon" src="docs/assets/features/domain.svg" width="20" height="20" alt=""> [Domain](./docs/features-domain.md)
- <img class="bdk-doc-icon" src="docs/assets/features/domain-events.svg" width="20" height="20" alt=""> [Domain Events](./docs/features-domain-events.md)
- <img class="bdk-doc-icon" src="docs/assets/features/event-sourcing.svg" width="20" height="20" alt=""> [Event Sourcing](./docs/features-event-sourcing.md)
- <img class="bdk-doc-icon" src="docs/assets/features/repositories.svg" width="20" height="20" alt=""> [Domain Repositories](./docs/features-domain-repositories.md)
  - Includes the preview Domain-owned entity bulk-insert capability, explicit decorators, and transaction/outbox guidance.
- <img class="bdk-doc-icon" src="docs/assets/features/specifications.svg" width="20" height="20" alt=""> [Domain Specifications](./docs/features-domain-specifications.md)
- <img class="bdk-doc-icon" src="docs/assets/features/active-entity.svg" width="20" height="20" alt=""> [ActiveEntity](./docs/features-domain-activeentity.md)
- <img class="bdk-doc-icon" src="docs/assets/features/policies.svg" width="20" height="20" alt=""> [Domain Policies](./docs/features-domain-policies.md)
- <img class="bdk-doc-icon" src="docs/assets/features/rules.svg" width="20" height="20" alt=""> [Rules](./docs/features-rules.md)
- <img class="bdk-doc-icon" src="docs/assets/features/results.svg" width="20" height="20" alt=""> [Results](./docs/features-results.md)
- <img class="bdk-doc-icon" src="docs/assets/features/commands-queries.svg" width="20" height="20" alt=""> [Application Commands and Queries](./docs/features-application-commands-queries.md)
- <img class="bdk-doc-icon" src="docs/assets/features/application-events.svg" width="20" height="20" alt=""> [Application Events](./docs/features-application-events.md)
- <img class="bdk-doc-icon" src="docs/assets/features/dataporter.svg" width="20" height="20" alt=""> [DataPorter](./docs/features-application-dataporter.md)

### Execution, Messaging and Modularity

- <img class="bdk-doc-icon" src="docs/assets/features/requester.svg" width="20" height="20" alt=""> <img class="bdk-doc-icon" src="docs/assets/features/notifier.svg" width="20" height="20" alt=""> [Requester and Notifier](./docs/features-requester-notifier.md)
- <img class="bdk-doc-icon" src="docs/assets/features/messaging.svg" width="20" height="20" alt=""> [Messaging](./docs/features-messaging.md)
- <img class="bdk-doc-icon" src="docs/assets/features/queueing.svg" width="20" height="20" alt=""> [Queueing](./docs/features-queueing.md)
- <img class="bdk-doc-icon" src="docs/assets/features/broadcasting.svg" width="20" height="20" alt=""> [Broadcasting](./docs/features-broadcasting.md)
- <img class="bdk-doc-icon" src="docs/assets/features/notifications.svg" width="20" height="20" alt=""> [Notifications](./docs/features-notifications.md)
- <img class="bdk-doc-icon" src="docs/assets/features/modules.svg" width="20" height="20" alt=""> [Modules](./docs/features-modules.md)
- <img class="bdk-doc-icon" src="docs/assets/features/pipelines.svg" width="20" height="20" alt=""> [Pipelines](./docs/features-pipelines.md)
- <img class="bdk-doc-icon" src="docs/assets/features/filtering.svg" width="20" height="20" alt=""> [Filtering](./docs/features-filtering.md)
- <img class="bdk-doc-icon" src="docs/assets/features/extensions.svg" width="20" height="20" alt=""> [Extensions](./docs/features-extensions.md)

### Security and Access

- <img class="bdk-doc-icon" src="docs/assets/features/authentication.svg" width="20" height="20" alt=""> [Presentation Authentication](./docs/features-presentation-authentication.md)
- <img class="bdk-doc-icon" src="docs/assets/features/entity-permissions.svg" width="20" height="20" alt=""> [Entity Permissions](./docs/features-entitypermissions.md)
- <img class="bdk-doc-icon" src="docs/assets/features/identity-provider.svg" width="20" height="20" alt=""> [Fake Identity Provider](./docs/features-identityprovider.md)

### Presentation and Host

- <img class="bdk-doc-icon" src="docs/assets/features/host.svg" width="20" height="20" alt=""> [Presentation Host](./docs/features-presentation.md)
- <img class="bdk-doc-icon" src="docs/assets/features/configuration.svg" width="20" height="20" alt=""> [Presentation Configuration](./docs/features-presentation-configuration.md)
- <img class="bdk-doc-icon" src="docs/assets/features/logging.svg" width="20" height="20" alt=""> [Presentation Logging](./docs/features-presentation-logging.md)
- <img class="bdk-doc-icon" src="docs/assets/features/correlation-ids.svg" width="20" height="20" alt=""> [Presentation Correlation IDs](./docs/features-presentation-correlationid.md)
- <img class="bdk-doc-icon" src="docs/assets/features/endpoints.svg" width="20" height="20" alt=""> [Presentation Endpoints](./docs/features-presentation-endpoints.md)
- <img class="bdk-doc-icon" src="docs/assets/features/cli.svg" width="20" height="20" alt=""> [DevKit CLI](./docs/features-cli.md)
- <img class="bdk-doc-icon" src="docs/assets/features/mcp.svg" width="20" height="20" alt=""> [DevKit MCP](./docs/features-cli-mcp.md)
- <img class="bdk-doc-icon" src="docs/assets/features/console-commands.svg" width="20" height="20" alt=""> [Console Commands](./docs/features-presentation-console-commands.md)
- <img class="bdk-doc-icon" src="docs/assets/features/cors.svg" width="20" height="20" alt=""> [CORS Configuration](./docs/features-presentation-cors.md)
- <img class="bdk-doc-icon" src="docs/assets/features/exception-handling.svg" width="20" height="20" alt=""> [Exception Handling](./docs/features-presentation-exception-handling.md)
- <img class="bdk-doc-icon" src="docs/assets/features/app-state.svg" width="20" height="20" alt=""> [AppState](./docs/features-presentation-appstate.md)

### Storage, Scheduling and Utilities

- <img class="bdk-doc-icon" src="docs/assets/features/startup-tasks.svg" width="20" height="20" alt=""> [StartupTasks](./docs/features-startuptasks.md)
- <img class="bdk-doc-icon" src="docs/assets/features/jobs.svg" width="20" height="20" alt=""> [Jobs](./docs/features-jobs.md)
- <img class="bdk-doc-icon" src="docs/assets/features/job-scheduling.svg" width="20" height="20" alt=""> [JobScheduling](./docs/features-jobscheduling.md) (legacy Quartz-backed feature)
- <img class="bdk-doc-icon" src="docs/assets/features/storage-documents.svg" width="20" height="20" alt=""> [DocumentStorage](./docs/features-storage-documents.md) - typed documents with concurrency, integrity, transforms, expiration/retention, and managed provider resources
- <img class="bdk-doc-icon" src="docs/assets/features/storage-files.svg" width="20" height="20" alt=""> [FileStorage](./docs/features-storage-files.md)
- <img class="bdk-doc-icon" src="docs/assets/features/storage-monitoring.svg" width="20" height="20" alt=""> [Storage Monitoring](./docs/features-storage-monitoring.md)
- <img class="bdk-doc-icon" src="docs/assets/features/log-entries.svg" width="20" height="20" alt=""> [Log Entries](./docs/features-log-entries.md)

### Testing and Test Utilities

- <img class="bdk-doc-icon" src="docs/assets/features/fake-authentication.svg" width="20" height="20" alt=""> [Fake Authentication for Integration Tests](./docs/testing-fake-authentication.md)
- <img class="bdk-doc-icon" src="docs/assets/features/testing.svg" width="20" height="20" alt=""> [Testing Common XUnit](./docs/testing-common-xunit.md)

## Libraries used (excerpt):

- [Xunit](https://github.com/xunit/xunit)
- [EnsureThat](https://github.com/danielwertheim/Ensure.That)
- [Mapster](https://github.com/MapsterMapper/Mapster)
- [FluentValidation](https://github.com/FluentValidation/FluentValidation)
- [FluentAssertions](https://github.com/fluentassertions/fluentassertions)
- [Humanizer](https://github.com/Humanizr/Humanizer)
- [Microsoft.Extensions.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/) (Polly)
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
- [WeatherFiesta](https://github.com/bridgingIT/bITdevKit/tree/main/examples/WeatherFiesta)

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

## CI/CD

Builds, tests and releases run on GitHub Actions. Details for each pipeline are in the
[workflows README](./.github/workflows/README.md).

| Workflow | Runs | Purpose |
| --- | --- | --- |
| [CI](./.github/workflows/github-actions.yml) | every push | calculates the version with MinVer, builds, runs the unit tests, and on `main` and `releases/dotnet9` can pack and push the NuGet packages |
| [Pages](./.github/workflows/pages.yml) | push to `main` | builds and publishes the documentation site, see [GitHub Pages](#github-pages) |
| [CodeQL](./.github/workflows/codeql-analysis.yml) | push and pull requests to `main`, daily | code scanning |

Dependency updates are configured in [dependabot.yml](./.github/dependabot.yml).

## Continuous AI

Recurring, repetitive repository chores are handled by agentic workflows, an approach GitHub Next calls
[Continuous AI](https://githubnext.com/projects/continuous-ai/): AI automation for collaboration, next to
CI/CD and not a replacement for it. The workflows are written in Markdown, compiled with the
[GitHub Agentic Workflows](https://github.github.com/gh-aw/) CLI (`gh aw compile`) into regular Actions
workflows, and run Copilot in a sandbox.

| Workflow | Runs | Result |
| --- | --- | --- |
| [Weekly Repo Status](./.github/workflows/weekly-repo-status.md) | Mondays | status report issue for maintainers |
| [Weekly Package Update](./.github/workflows/weekly-package-update.md) | Mondays | draft PR that applies the [update-packages](./.agents/skills/update-packages/SKILL.md) skill, only when build and unit tests are green |
| [CI Fault Analysis](./.github/workflows/ci-fault-analysis.md) | failed CI on `main` or `releases/*` | issue with the likely root cause |
| [Continuous Docs](./.github/workflows/continuous-docs.md) | Wednesdays | draft PR with fixes that keep `docs/` and the mkdocs site in line with the code |
| [Continuous Test Improvement](./.github/workflows/continuous-test-improvement.md) | Tuesdays and Fridays | draft PR with a few new unit tests for one under-covered class |

Guardrails:

- Agents run read-only. Issues and pull requests are created through [safe outputs](https://github.github.com/gh-aw/reference/safe-outputs/), and pull requests are drafts that a human reviews and merges.
- Each run is capped in AI credits and time. Usage is billed to the organization Copilot plan, see [cost management](https://github.github.com/gh-aw/reference/cost-management/) and check spend with `gh aw logs`.
- The Markdown file is the source. Edit it, run `gh aw compile`, and commit the generated `.lock.yml` with it.

Further reading: [Automate repository tasks with GitHub Agentic Workflows](https://github.blog/ai-and-ml/automate-repository-tasks-with-github-agentic-workflows/).

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

MkDocs is run through Docker. You do not need a local Python or MkDocs installation; you only need Docker Desktop or another Docker engine.

Preview the full site locally:

```powershell
pwsh -File ./docs/site/scripts/serve-pages.ps1
```

The preview script synchronizes the public docs into `docs/site/reference/`, then starts the `squidfunk/mkdocs-material:9` container and serves the site at [http://localhost:8000](http://localhost:8000).

Build the full static site locally:

```powershell
pwsh -File ./docs/site/scripts/build-pages.ps1
```

That build command performs these steps:

1. Synchronize the selected public docs from `./docs` into `docs/site/reference/`
2. Build the MkDocs site in Docker with `squidfunk/mkdocs-material:9`
3. Write the generated static site to `./.github/pages/`

For troubleshooting, the build script is equivalent to:

```powershell
pwsh -File ./docs/site/scripts/sync-docs.ps1
docker run --rm -v "${PWD}:/docs" squidfunk/mkdocs-material:9 build --clean
```

Successful output should include `Documentation built` and generate `./.github/pages/index.html`. Docker may print informational warnings from Material for MkDocs; link warnings should be treated as documentation issues and fixed before publishing.

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
