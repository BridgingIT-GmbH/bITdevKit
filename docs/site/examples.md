---
title: Examples
---

<img class="bdk-doc-icon" src="assets/features/source-generation.svg" width="32" height="32" alt="" align="right">

# Examples

Use these applications to trace `bITdevKit` features through working code.

## Primary onboarding example

### GettingStarted

The [bITdevKit.Examples.GettingStarted](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted) repository contains the introductory application.

Use it to:

- examine the generated project structure
- learn the bootstrap sequence
- connect the documentation to a runnable example

## Repository examples

### DoFiesta

Source: [`examples/DoFiesta`](https://github.com/bridgingIT/bITdevKit/tree/main/examples/DoFiesta)

Use it to:

- run a full-stack application with a Blazor WebAssembly frontend and ASP.NET Core API backend
- trace Domain-Driven Design patterns across Domain, Application, Infrastructure, and Presentation layers
- follow aggregates, value objects, domain events, specifications, rules, commands, and queries in context
- inspect generated API clients, persistence, file attachments, messaging, jobs, and operational integrations

Run it from the repository root:

```bash
dotnet run --project examples/DoFiesta/DoFiesta.Presentation.Web.Server
```

For layer responsibilities and request handling, see [Architecture](architecture.md) and [Application Commands and Queries](reference/features-application-commands-queries.md).

### EventSourcingDemo

Source: [`examples/EventSourcingDemo`](https://github.com/bridgingIT/bITdevKit/tree/main/examples/EventSourcingDemo)

Use it to:

- examine event-sourced aggregates and persistence
- understand how event sourcing integrates with other devkit features

Run it from the repository root:

```bash
dotnet run --project examples/EventSourcingDemo/EventSourcingDemo.Presentation.Web
```

For persistence and API contracts, see [Event Sourcing](reference/features-event-sourcing.md) and [Endpoints](reference/features-presentation-endpoints.md).

### WeatherFiesta

Source: [`examples/WeatherFiesta`](https://github.com/bridgingIT/bITdevKit/tree/main/examples/WeatherFiesta)

Use it to:

- examine ActiveEntity and modular vertical slices in a weather dashboard
- learn service-agent abstractions through the Open-Meteo integration
- trace commands, queries, requester pipelines, scheduled ingestion, and data export
- inspect subscription-gated features, developer dashboards, console commands, and MCP integration

Run it from the repository root:

```bash
dotnet run --project examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server
```

For architecture, host configuration, and test support, see [Architecture](architecture.md), [Configuration](reference/features-presentation-configuration.md), and [Testing Common XUnit](reference/testing-common-xunit.md).
