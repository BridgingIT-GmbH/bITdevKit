---
title: Getting Started
---

# Getting Started

`bITdevKit` is a modular .NET development kit built around clean architecture, Domain-Driven Design,
modular vertical slices, and reusable building blocks for real-world application concerns.

This page is the fastest way to get oriented and choose a useful starting point in the kit.

## Start with the official GettingStarted project

The primary onboarding entry point for `bITdevKit` is the dedicated
[`bITdevKit.Examples.GettingStarted`](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted)
repository.

Use that project first if you want to:

- see the devkit in a focused, end-to-end application instead of a large framework repository
- understand the intended architecture and bootstrap sequence
- explore concrete code examples for commands, queries, aggregates, value objects, events, infrastructure, and presentation
- learn the core patterns in a guided way before moving into the broader docs set

Recommended first path:

1. Open the [`bITdevKit.Examples.GettingStarted` repository](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted).
2. Read its README from top to bottom.
3. Run the example and inspect its solution structure.
4. Read the [DDD Introduction](reference/introduction-ddd-guide.md) to connect the example to the kit's architectural mindset.
5. Come back to this site for the deeper framework docs behind the concepts used there.

If you already know the architectural approach and want to start your own solution quickly, continue
with the [Templates](templates.md) page next.

## Understand the architectural approach

If you are new to `bITdevKit`, start with these pages in order:

1. Read the [Overview](reference/index.md) for the full map of the public docs.
2. Continue with the [DDD Introduction](reference/introduction-ddd-guide.md) to understand the architectural mindset.
3. Read [Domain](reference/features-domain.md) to see the core tactical building blocks.
4. Continue with [Results](reference/features-results.md) to understand the kit's explicit success/failure model.
5. Read [Application Commands and Queries](reference/features-application-commands-queries.md) and [Requester and Notifier](reference/features-requester-notifier.md) to understand the application flow.
6. Finish the first pass with [Modules](reference/features-modules.md) and [Presentation Endpoints](reference/features-presentation-endpoints.md).

That sequence gives you the shortest path to understanding how the kit is intended to be composed.

## Choose a starting track

- If you are evaluating the architectural approach, start with the [DDD Introduction](reference/introduction-ddd-guide.md), [Domain](reference/features-domain.md), [Domain Repositories](reference/features-domain-repositories.md), and [Domain Specifications](reference/features-domain-specifications.md).
- If you are building application workflows, start with [Results](reference/features-results.md), [Application Commands and Queries](reference/features-application-commands-queries.md), [Application Events](reference/features-application-events.md), and [Requester and Notifier](reference/features-requester-notifier.md).
- If you are structuring a modular monolith, start with [Modules](reference/features-modules.md), [Pipelines](reference/features-pipelines.md), and [Presentation Endpoints](reference/features-presentation-endpoints.md).
- If you are integrating operational infrastructure, start with [Messaging](reference/features-messaging.md), [Queueing](reference/features-queueing.md), [JobScheduling](reference/features-jobscheduling.md), [DocumentStorage](reference/features-storage-documents.md), and [FileStorage](reference/features-storage-files.md).

## Explore the example applications

The repository includes several examples that show the kit in practice:

- [GettingStarted](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted): the canonical onboarding project and the recommended first hands-on entry point.
- [DoFiesta](https://github.com/bridgingIT/bITdevKit/tree/main/examples/DoFiesta): broader example application using the devkit in a realistic host setup.
- [EventSourcingDemo](https://github.com/bridgingIT/bITdevKit/tree/main/examples/EventSourcingDemo): example for event-sourcing-oriented scenarios.
- [WeatherForecast](https://github.com/bridgingIT/bITdevKit/tree/main/examples/WeatherForecast): lightweight example for focused experimentation.

If you want the shortest practical route, start with `GettingStarted` and read it together with the
docs for `Domain`, `Results`, `Requester and Notifier`, `Modules`, and `Presentation Endpoints`.

## What to read next

- Read [Why bITdevKit](why.md) for the positioning and fit of the devkit.
- Read [Architecture](architecture.md) for the layer map, module shape, and request flow.
- Read [Examples](examples.md) for the recommended progression through the sample applications.
- Read [Packages](packages.md) to understand the repository as grouped package families.
- Use the [Overview](reference/index.md) when you want the complete list of public framework topics.
- Use the [Templates](templates.md) page when you want to scaffold your own solution or add modules.
- Move into the example applications once you want to see how the pieces fit together in code.
