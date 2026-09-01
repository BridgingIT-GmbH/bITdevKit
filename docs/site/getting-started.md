---
title: Getting Started
---

# Getting Started

`bITdevKit` is a modular .NET development kit built around clean architecture, Domain-Driven Design, and modular vertical slices. It provides libraries for application, infrastructure, and operational concerns.

Use this page to choose an example and a documentation path.

## Start with the official GettingStarted project

The dedicated [`bITdevKit.Examples.GettingStarted`](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted) repository introduces the main architecture and APIs.

Use that project first if you want to:

- examine a runnable application outside the framework repository
- understand the architecture and bootstrap sequence
- find examples of commands, queries, aggregates, value objects, events, infrastructure, and presentation
- learn the core patterns before reading the feature reference

Recommended first path:

1. Open the [`bITdevKit.Examples.GettingStarted` repository](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted).
2. Read its README from top to bottom.
3. Run the example and inspect its solution structure.
4. Read the [DDD Introduction](reference/introduction-ddd-guide.md) to connect the example to the architecture.
5. Return to this site for the feature guides used by the example.

If you already know the architecture and want to create a solution, continue with [Templates](templates.md).

## Understand the architectural approach

If you are new to `bITdevKit`, start with these pages in order:

1. Read the [Overview](reference/index.md) for the full map of the public docs.
2. Continue with the [DDD Introduction](reference/introduction-ddd-guide.md) to understand the architectural mindset.
3. Read [Domain](reference/features-domain.md) to see the core tactical building blocks.
4. Continue with [Results](reference/features-results.md) to understand the kit's explicit success/failure model.
5. Read [Application Commands and Queries](reference/features-application-commands-queries.md) and [Requester and Notifier](reference/features-requester-notifier.md) to understand the application flow.
6. Finish the first pass with [Modules](reference/features-modules.md) and [Presentation Endpoints](reference/features-presentation-endpoints.md).

This sequence explains how the main libraries compose an application.

## Choose a starting track

- If you are evaluating the architectural approach, start with the [DDD Introduction](reference/introduction-ddd-guide.md), [Domain](reference/features-domain.md), [Domain Repositories](reference/features-domain-repositories.md), and [Domain Specifications](reference/features-domain-specifications.md).
- If you are building application workflows, start with [Results](reference/features-results.md), [Application Commands and Queries](reference/features-application-commands-queries.md), [Application Events](reference/features-application-events.md), and [Requester and Notifier](reference/features-requester-notifier.md).
- If you are structuring a modular monolith, start with [Modules](reference/features-modules.md), [Pipelines](reference/features-pipelines.md), and [Presentation Endpoints](reference/features-presentation-endpoints.md).
- If you are integrating operational infrastructure, start with [Messaging](reference/features-messaging.md), [Queueing](reference/features-queueing.md), [Jobs](reference/features-jobs.md), [Blob Storage](reference/features-storage-blobs.md), [Document Storage](reference/features-storage-documents.md), and [File Storage](reference/features-storage-files.md).

## Explore the example applications

The repository includes several examples that show the kit in practice:

- [GettingStarted](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted): an introductory application with one example module.
- [DoFiesta](https://github.com/bridgingIT/bITdevKit/tree/main/examples/DoFiesta): full-stack, domain-driven example with a Blazor WebAssembly frontend and ASP.NET Core API backend.
- [EventSourcingDemo](https://github.com/bridgingIT/bITdevKit/tree/main/examples/EventSourcingDemo): example for event-sourcing-oriented scenarios.
- [WeatherFiesta](https://github.com/bridgingIT/bITdevKit/tree/main/examples/WeatherFiesta): weather dashboard demonstrating ActiveEntity, service-agent abstractions, requester pipelines, scheduled ingestion, subscriptions, and operational tooling.

Start with `GettingStarted`, then read the guides for `Domain`, `Results`, `Requester and Notifier`, `Modules`, and `Presentation Endpoints`.

## What to read next

- Read [Why bITdevKit](why.md) to decide whether the devkit fits your application.
- Read [Architecture](architecture.md) for the layer map, module shape, and request flow.
- Read [Examples](examples.md) for the recommended progression through the sample applications.
- Read [Packages](packages.md) to understand the repository as grouped package families.
- Use the [Overview](reference/index.md) for the complete list of public framework topics.
- Use [Templates](templates.md) to create a solution or add modules.
- Use the example applications to trace features through working code.
