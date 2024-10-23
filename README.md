![bITDevKit](https://raw.githubusercontent.com/bridgingIT/bITdevKit/main/bITDevKit_Logo.png)
=====================================
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/bridgingIT/bITdevKit/github-actions.yml?style=flat)](https://github.com/bridgingIT/bITdevKit/actions/workflows/github-actions.yml)
[![NuGet](https://img.shields.io/nuget/v/BridgingIT.DevKit.Common.Utilities?style=flat-square&label=nuget%20packages)](https://www.nuget.org/packages?q=bitdevkit)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE)

Empowering developers with modular components for modern application development, centered around
Domain-Driven Design principles.

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

<!-- TOC -->
* [Supported patterns, elements:](#supported-patterns-elements)
* [Features (excerpt):](#features-excerpt)
* [Libraries used (excerpt):](#libraries-used-excerpt)
* [Example projects](#example-projects)
* [Collaboration](#collaboration)
<!-- TOC -->

Supported patterns, elements:
--------------------------------

- Entity
- AggregateRoot
- ValueObjects
  - TypedId
- DomainEvents
- DomainRules/Policies
- Repository
- Specifications
- Commands/Queries
- Outbox
  - DomainEvents
  - Messaging
- Decorator (Behavior)

Features (excerpt):
-------------------------------------

- [Results](./docs/features-results.md)
- [Commands](./docs/features-commands.md) & [Queries](./docs/features-queries.md)
- [Domain Model](./docs/features-domain-models.md)
- [Domain Repositories](./docs/features-domain-repositories.md)
- [Modules](./docs/features-modules.md)
- [Filtering](./docs/features-filtering.md)
- [Messaging](./docs/features-messaging.md)
- [JobScheduling](./docs/features-jobscheduling.md)
- [StartupTasks](./docs/features-startuptasks.md)
- [DocumentStorage](./docs/features-documentstorage.md)
- EventStore (CQRS)
- Caching
- Queuing (TODO)

Libraries used (excerpt):
-------------------------------------

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

Example projects
-----------------

- [GettingStarted](https://github.com/bridgingIT/bITdevKit.Examples.GettingStarted)
- [EventStore](https://github.com/bridgingit/bitdevkit/examples)
- [DinnerFiesta](https://github.com/bridgingit/bitdevkit/examples)
- [WeatherForecast](https://github.com/bridgingit/bitdevkit/examples)
- [Shop](https://github.com/bridgingit/bitdevkit/examples)

Collaboration
---------
Simply create a pull request with your ideas or contact us.