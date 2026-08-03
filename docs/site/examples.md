---
title: Examples
---

# Examples

The examples are the best way to see how the building blocks come together in real code.

## Primary onboarding example

### GettingStarted

Repository:
[bITdevKit.Examples.GettingStarted](https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted)

Best for:

- first contact with the devkit
- understanding the intended project shape
- following the concepts from docs into a focused runnable example

## Repository examples

### DoFiesta

Path:
[`examples/DoFiesta`](https://github.com/bridgingIT/bITdevKit/tree/main/examples/DoFiesta)

Best for:

- a full-stack application with a Blazor WebAssembly frontend and ASP.NET Core API backend
- a stronger Domain-Driven Design example across Domain, Application, Infrastructure, and Presentation layers
- following aggregates, value objects, domain events, specifications, rules, commands, and queries in context
- exploring generated API clients, persistence, file attachments, messaging, jobs, and operational integrations

Run it from the repository root:

```bash
dotnet run --project examples/DoFiesta/DoFiesta.Presentation.Web.Server
```

For its layer structure and development notes, read the
[`DoFiesta-README.md`](https://github.com/bridgingIT/bITdevKit/blob/main/examples/DoFiesta/DoFiesta-README.md).

### EventSourcingDemo

Path:
[`examples/EventSourcingDemo`](https://github.com/bridgingIT/bITdevKit/tree/main/examples/EventSourcingDemo)

Best for:

- event-sourcing-oriented exploration
- understanding how event-driven persistence concepts fit into the devkit

Run it from the repository root:

```bash
dotnet run --project examples/EventSourcingDemo/EventSourcingDemo.Presentation.Web
```

For its short overview and REST request guidance, read the
[`EventSourcingDemo-README.md`](https://github.com/bridgingIT/bITdevKit/blob/main/examples/EventSourcingDemo/EventSourcingDemo-README.md).

### WeatherFiesta

Path:
[`examples/WeatherFiesta`](https://github.com/bridgingIT/bITdevKit/tree/main/examples/WeatherFiesta)

Best for:

- seeing ActiveEntity and modular vertical slices in a realistic weather dashboard
- learning service-agent abstractions through the Open-Meteo integration
- following commands, queries, requester pipelines, scheduled ingestion, and data export end to end
- exploring subscription-gated features, developer dashboards, console commands, and MCP integration

Run it from the repository root:

```bash
dotnet run --project examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server
```

For its architecture, API, configuration, and testing guide, read the
[`WeatherFiesta-README.md`](https://github.com/bridgingIT/bITdevKit/blob/main/examples/WeatherFiesta/WeatherFiesta-README.md).
