---
title: Packages
---

# Packages

The repository contains many packages, but they are easier to understand when grouped by role rather
than read as a flat list.

## Core foundation

These packages provide shared building blocks used throughout the kit:

- `Common.Abstractions`
- `Common.Results`
- `Common.Rules`
- `Common.Extensions`
- `Common.Mapping`
- `Common.Options`
- `Common.Serialization`
- `Common.Utilities`
- `Common.Caching`
- `Common.Modules`

## Domain and DDD

These packages support domain modeling and event-driven domain behavior:

- `Domain`
- `Domain.CodeGen`
- `Domain.Mediator`
- `Domain.Outbox`
- `Domain.EventSourcing`
- `Domain.EventSourcing.Mediator`
- `Domain.EventSourcing.Outbox`

## Application flow

These packages shape command/query handling, messaging, queueing, notifications, and storage-facing
application abstractions:

- `Application.Commands`
- `Application.Queries`
- `Application.Messaging`
- `Application.Queueing`
- `Application.Notifications`
- `Application.JobScheduling`
- `Application.Storage`
- `Application.DataPorter`
- `Application.Identity`
- `Application.Utilities`

## Infrastructure providers

These packages implement transport and persistence choices:

- `Infrastructure.EntityFramework`
- `Infrastructure.EntityFramework.SqlServer`
- `Infrastructure.EntityFramework.Sqlite`
- `Infrastructure.EntityFramework.Postgres`
- `Infrastructure.EntityFramework.Cosmos`
- `Infrastructure.Azure.ServiceBus`
- `Infrastructure.Azure.Storage`
- `Infrastructure.Azure.Cosmos`
- `Infrastructure.RabbitMQ`
- `Infrastructure.LiteDB`

## Presentation and host-facing packages

These packages expose the kit through web, configuration, logging, and feature-specific presentation
layers:

- `Presentation`
- `Presentation.Configuration`
- `Presentation.Serilog`
- `Presentation.Web`
- `Presentation.Web.EntityFramework`
- `Presentation.Web.JobScheduling`
- `Presentation.Web.Messaging`
- `Presentation.Web.Notifications`
- `Presentation.Web.Queueing`
- `Presentation.Web.Storage`

## Testing and generation support

These packages mainly help with source generation, testing, or supporting infrastructure:

- `Common.Utilities.CodeGen`
- `Common.Utilities.Tracing`
- `Common.Utilities.Xunit`
- `Domain.CodeGen`
- `Presentation.Web.Client`
- `Infrastructure.Windows`

## How to read the package map

The most useful mental model is:

1. Start with the developer-facing concepts in [Getting Started](getting-started.md).
2. Learn the architectural roles on the [Architecture](architecture.md) page.
3. Use the public [Documentation](reference/index.md) pages for feature-level detail.
4. Treat the package list as an implementation map, not the primary onboarding experience.
