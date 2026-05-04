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
  - [Feature Highlights](#feature-highlights)
    - [Core domain and application](#core-domain-and-application)
    - [Execution, messaging and modularity](#execution-messaging-and-modularity)
    - [Presentation and host](#presentation-and-host)
    - [Storage, scheduling and operations](#storage-scheduling-and-operations)
    - [Common building blocks](#common-building-blocks)
  - [Libraries used (excerpt):](#libraries-used-excerpt)
  - [Example projects](#example-projects)
  - [Performance Benchmarks](#performance-benchmarks)
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

The best entry point into the current documentation set is the
[Documentation Index](./docs/INDEX.md).

Recommended starting path:

- [Domain](./docs/features-domain.md)
- [Results](./docs/features-results.md)
- [Requester and Notifier](./docs/features-requester-notifier.md)
- [Modules](./docs/features-modules.md)
- [Presentation Endpoints](./docs/features-presentation-endpoints.md)

Additional entry points:

- [Introduction to DDD in bITdevKit](./docs/introduction-ddd-guide.md)
- [Testing Common XUnit](./docs/testing-common-xunit.md)
- [Fake Authentication for Integration Tests](./docs/testing-fake-authentication.md)

## Feature Highlights

### Core domain and application

- [Domain](./docs/features-domain.md)
- [Domain Events](./docs/features-domain-events.md)
- [Domain Repositories](./docs/features-domain-repositories.md)
- [Domain Specifications](./docs/features-domain-specifications.md)
- [Results](./docs/features-results.md)
- [Application Commands and Queries](./docs/features-application-commands-queries.md)
- [Application Events](./docs/features-application-events.md)

### Execution, messaging and modularity

- [Requester and Notifier](./docs/features-requester-notifier.md)
- [Messaging](./docs/features-messaging.md)
- [Queueing](./docs/features-queueing.md)
- [Notifications](./docs/features-notifications.md)
- [Modules](./docs/features-modules.md)
- [Pipelines](./docs/features-pipelines.md)
- [Filtering](./docs/features-filtering.md)

### Presentation and host

- [Presentation Endpoints](./docs/features-presentation-endpoints.md)
- [Console Commands](./docs/features-presentation-console-commands.md)
- [CORS Configuration](./docs/features-presentation-cors.md)
- [Exception Handling](./docs/features-presentation-exception-handling.md)
- [AppState](./docs/features-presentation-appstate.md)

### Storage, scheduling and operations

- [StartupTasks](./docs/features-startuptasks.md)
- [JobScheduling](./docs/features-jobscheduling.md)
- [DocumentStorage](./docs/features-storage-documents.md)
- [FileStorage](./docs/features-storage-files.md)
- [Storage Monitoring](./docs/features-storage-monitoring.md)
- [Log Entries](./docs/features-log-entries.md)

### Common building blocks

- [Common Extensions](./docs/common-extensions.md)
- [Common Utilities](./docs/common-utilities.md)
- [Common Serialization](./docs/common-serialization.md)
- [Common Options Builders](./docs/common-options-builders.md)
- [Common Mapping](./docs/common-mapping.md)
- [Common Caching](./docs/common-caching.md)
- [Common Observability Tracing](./docs/common-observability-tracing.md)

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
- [DinnerFiesta](https://github.com/bridgingit/bitdevkit/examples)
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
