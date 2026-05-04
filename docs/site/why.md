---
title: Why bITdevKit
---

# Why bITdevKit

`bITdevKit` is for teams that want more than a collection of isolated libraries. It provides a
coherent development kit for modular .NET applications, with opinionated patterns for modeling,
application flow, infrastructure integration, and operational behavior.

## What the devkit adds

### Architectural consistency

The kit is shaped around clean architecture, modular vertical slices, and DDD-oriented modeling.
That makes it easier to grow a codebase without losing boundaries between domain, application,
infrastructure, and presentation.

### Explicit runtime behavior

Results, rules, pipelines, requester/notifier flows, and queue/message abstractions are designed to
work together instead of being assembled ad hoc.

### Operational realism

The kit includes support for real-world concerns such as durable queueing, outbox-backed messaging,
job scheduling, document/file storage, and operational endpoints.

### Faster project setup

The template and example story reduces the amount of repetitive structural work required to start a
new project or add a new module.

## When it fits well

`bITdevKit` is a strong fit when the codebase needs:

- a modular monolith with clear boundaries
- DDD-style domain modeling instead of DTO-first CRUD design
- explicit request and result flows across the application layer
- infrastructure that goes beyond basic HTTP and EF Core
- consistent patterns that multiple developers or teams can follow

## When it may be too much

The kit may be heavier than necessary for:

- very small single-purpose services
- short-lived prototypes with almost no domain logic
- simple CRUD applications where a plain ASP.NET Core setup is enough

## Why not just plain ASP.NET Core + MediatR + EF Core?

That stack is perfectly valid. `bITdevKit` becomes useful when the team wants the surrounding
structure to be more intentional and more repeatable.

| Concern | Plain stack | bITdevKit |
|---|---|---|
| Application flow | Typically assembled project by project | Built around requester/notifier, results, rules, and pipelines |
| Domain modeling | Depends on team discipline | Strongly supported through aggregates, typed ids, policies, and specifications |
| Modular boundaries | Manual conventions | First-class module composition patterns |
| Queueing and messaging | Usually added separately | Integrated concepts with related operational support |
| Operational endpoints | Typically custom | Reusable patterns for inspection and control |
| Onboarding | Team-specific | Templates, examples, and aligned docs |

## Good next pages

- [Architecture](architecture.md)
- [Packages](packages.md)
- [Examples](examples.md)
- [Messaging vs Queueing](decisions-messaging-vs-queueing.md)
- [Repository vs ActiveEntity](decisions-repository-vs-activeentity.md)