---
title: Why bITdevKit
---

# Why bITdevKit

`bITdevKit` provides libraries and templates for modular .NET applications. Its APIs cover domain modeling, application flow, infrastructure integration, and operational behavior.

## What the devkit adds

### Architectural consistency

The libraries follow clean architecture, modular vertical slices, and DDD-oriented modeling. Layer and module boundaries separate domain, application, infrastructure, and presentation code.

### Explicit runtime behavior

Results, rules, pipelines, requester/notifier flows, and queue and messaging abstractions use a shared application model.

### Operational features

The libraries provide durable queueing, outbox-backed messaging, job scheduling, document and file storage, and operational endpoints.

### Two ways to start

The templates generate the solution and module structure for a new application. An existing ASP.NET Core app keeps its projects. It swaps one line in `Program.cs` for the DevKit host builder, then adds features one at a time. Each feature is a separate package with its own registration, so a team can ship logging and request correlation first and add queueing or jobs later. [Get started](getting-started.md) compares both paths. The examples show how to configure and use the generated projects.

## When to use it

Use `bITdevKit` when a codebase needs:

- a modular monolith with clear boundaries
- DDD-style domain modeling instead of DTO-first CRUD design
- explicit request and result flows across the application layer
- messaging, queueing, storage, or scheduling in addition to HTTP and EF Core
- consistent patterns that multiple developers or teams can follow

## When a smaller stack fits

A smaller set of libraries may be sufficient for:

- very small single-purpose services
- short-lived prototypes with almost no domain logic
- simple CRUD applications where a plain ASP.NET Core setup is enough

## Compare with ASP.NET Core, MediatR, and EF Core

ASP.NET Core, MediatR, and EF Core provide the web, request-dispatch, and persistence foundations. `bITdevKit` adds shared APIs and conventions around those foundations.

| Concern | Plain stack | bITdevKit |
| --- | --- | --- |
| Application flow | Defined by each project | Requester/notifier, results, rules, and pipelines |
| Domain modeling | Project-defined types and conventions | Aggregates, typed IDs, policies, and specifications |
| Modular boundaries | Project conventions | Module registration and composition APIs |
| Queueing and messaging | Separate library choices and configuration | Related abstractions, transports, and operational controls |
| Operational endpoints | Project-defined endpoints | Dashboard, health, and control endpoints |
| Project setup | Project-defined structure | Templates, examples, and related documentation |
