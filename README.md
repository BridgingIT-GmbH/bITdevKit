![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/bridgingIT/bITdevKit/github-actions.yml?style=flat)](https://github.com/bridgingIT/bITdevKit/actions/workflows/github-actions.yml)
[![NuGet](https://img.shields.io/nuget/v/BridgingIT.DevKit.Common.Utilities?style=flat-square&label=nuget%20packages)](https://www.nuget.org/packages?q=bitdevkit)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=bitdevkit&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=bitdevkit)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE)

> Empowering developers with modular components for modern application development, centered around
> Domain-Driven Design principles.

<!-- TOC -->

* [Introduction:](#introduction)
* [Features:](#features)
* [Libraries used](#libraries-used-excerpt)
* [Example projects](#example-projects)
* [Collaboration](#collaboration)

<!-- TOC -->

## Introduction

::: mermaid
sequenceDiagram
    Christie->>Josh: Hello Josh, how are you?
    Josh-->>Christie: Great!
    Christie->>Josh: See you later!
:::


Our goal is to empower developers by offering modular components that can be easily integrated into
your projects. Whether you're working with repositories, commands, queries, or other components, the
bITDevKit provides flexible solutions that can adapt to your specific needs.

This repository includes the complete source code for the bITDevKit, along with a variety of sample
applications located in the ./examples folder within the solution. These samples serve as practical
demonstrations of how to leverage the capabilities of the bITDevKit in real-world scenarios. All
components are available
as [nuget packages](https://www.nuget.org/packages?q=bitDevKit&packagetype=&prerel=true&sortby=relevance).

For the latest updates and release notes, please refer to
the [RELEASES](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/RELEASES.md).

Join us in advancing the world of software development with the bITDevKit!

## Features:

- [Commands](./docs/features-commands.md) & [Queries](./docs/features-queries.md)
- [Domain Model](./docs/features-domain-models.md)
- Domain Events
- Domain Specifications
- [Domain Repositories](./docs/features-domain-repositories.md)
- Domain TypedIds
- Domain Policies & Rules
- Domain EventSourcing
- [Modules](./docs/features-modules.md)
- [Filtering](./docs/features-filtering.md)

  Addresses the challenges of data querying in modern applications by providing a unified,
  type-safe, and flexible solution for filtering, sorting, and pagination through API requests.
- [Results](./docs/features-results.md)

  Tackles the challenges of inconsistent error handling and outcome management in applications. It
  introduces a standardized, type-safe Result pattern for explicit success/failure handling and
  streamlining outcomes with functional extensions.
- [Rules](./docs/features-rules.md)

  Provides a flexible and extensible way to define and enforce business rules in your application at
  several layers. It allows to encapsulate and manage rules in a single place, making them easy to
  maintain, test and apply across your domain.
- [Messaging](./docs/features-messaging.md)
- Queuing (TODO)
- [JobScheduling](./docs/features-jobscheduling.md)
- [StartupTasks](./docs/features-startuptasks.md)
- [DocumentStorage](./docs/features-documentstorage.md)

## Libraries used (excerpt):

- [Xunit](https://github.com/xunit/xunit)
- [MediatR](https://github.com/jbogard/MediatR)
- [EnsureThat.Core](https://github.com/danielwertheim/Ensure.That)
- [AutoMapper](https://github.com/AutoMapper/AutoMapper)
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
- EntityFramework Core
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
- [Shop](https://github.com/bridgingit/bitdevkit/examples)

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
Please refer to the [CONTRIBUTING](./CONTRIBUTION.md) guidelines for more information.

--- 
## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.