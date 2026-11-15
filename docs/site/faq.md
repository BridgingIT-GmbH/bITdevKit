---
title: FAQ
hide:
  - footer
---

# FAQ { data-feature-icons="false" }

Answers to common questions about choosing bITdevKit, adding it to your application, and working with its features.

## About bITdevKit { data-feature-icons="false" }

### What is bITdevKit? { data-feature-icons="false" }

bITdevKit is a modular .NET toolkit for business applications. It provides reusable libraries for domain modeling, request handling, persistence, messaging, background work, and developer tooling. Choose the parts your application needs and compose them through shared APIs and conventions.

### Why use bITdevKit? { data-feature-icons="false" }

It gives your team a starting point for infrastructure and application patterns that otherwise need separate design and integration. Shared results, validation, request pipelines, and operational tools help keep features consistent as the application grows. See [why bITdevKit](why.md) for its strengths and trade-offs.

### What kinds of applications is it suited to? { data-feature-icons="false" }

Applications with business rules, several feature areas, or work beyond HTTP requests, such as messaging, storage, and scheduled tasks. Its module model supports teams building a modular monolith. A small CRUD service can adopt individual features when needed. See [when to use it](why.md#when-to-use-it).

## Getting started { data-feature-icons="false" }

### Do I need to use the whole toolkit? { data-feature-icons="false" }

No. Select the packages and features you need. You can begin with a small addition and introduce more capabilities as requirements change. The [package map](packages.md) and [feature recipes](getting-started-existing-app-features.md) show the available parts and their dependencies.

### Can I add DevKit to an existing application? { data-feature-icons="false" }

Yes. Introduce DevKit gradually while continuing to work on your existing application. The [existing application guide](getting-started-existing-app.md) walks through the changes, with a check and an AI prompt for each step.

### Do I have to start from a template? { data-feature-icons="false" }

No. The templates provide a prepared solution and an initial module for a new application. Existing applications can add DevKit directly. [Get started](getting-started.md) links to both approaches.

### Do I need to reorganize my solution first? { data-feature-icons="false" }

No. The existing application guide keeps the current project and introduces features within it. Separate layer projects and additional modules can follow when the application benefits from them. See the [architecture guide](architecture.md) for the intended boundaries.

### Which .NET version do I need? { data-feature-icons="false" }

The current runtime packages target .NET 10. Use the .NET 10 SDK for the getting started guides and check the target framework of the packages you add. The [template walkthrough](getting-started-template.md) covers the initial setup.

## Architecture and integration { data-feature-icons="false" }

### Do I need to adopt Domain-Driven Design first? { data-feature-icons="false" }

No. You can use infrastructure features before changing your domain model. Aggregates, value objects, business rules, and domain events are available when you need to model richer business behavior. The [DDD introduction](reference/introduction-ddd-guide.md) explains those concepts.

### Does DevKit replace ASP.NET Core or Entity Framework Core? { data-feature-icons="false" }

DevKit builds on them. Its web host, endpoint composition, and persistence integrations extend the .NET stack with shared conventions and services. Your application still owns its routes, data model, and business behavior. See [Presentation](reference/features-presentation.md) and [Domain Repositories](reference/features-domain-repositories.md).

### Is DevKit only for modular monoliths? { data-feature-icons="false" }

The module model is designed for a modular monolith, where one host composes features with clear boundaries. The libraries and messaging transports can also be used in separate services. Deployment boundaries and communication between services remain application design decisions. See [Architecture](architecture.md) and [Messaging](reference/features-messaging.md).

### Which databases can I use? { data-feature-icons="false" }

The Entity Framework Core integrations include SQL Server, PostgreSQL, and SQLite. Select the provider for your application and check the feature's provider capabilities. The [repository guide](reference/features-domain-repositories.md) covers persistence behavior and provider-specific features.

### Does DevKit require a particular user interface? { data-feature-icons="false" }

No. You can use its backend features with the frontend your application needs. The [Developer Dashboards](developer-dashboards.md) provide pages for inspecting the host and its features, independently of your application's user interface.

## Development and operations { data-feature-icons="false" }

### How can I inspect what the application is doing? { data-feature-icons="false" }

The [Developer Dashboards](developer-dashboards.md) bring together the diagnostics exposed by registered features. Depending on your setup, you can inspect health, logs, metrics, requests, jobs, queues, and workflows. The available pages reflect the features you enable.

### Can my coding agent work with DevKit? { data-feature-icons="false" }

Yes. bdk MCP gives compatible agents access to documentation, implementation guidance, and API reference tools. With a running DevKit host, agents can also inspect the runtime. The solution template already installs and configures bdk MCP. See [AI Agent Support](agent-support.md) for setup and capabilities.

### Do I need a running application to use bdk MCP? { data-feature-icons="false" }

Documentation, guidance, and API tools work without a running application. Runtime tools need a running DevKit host that exposes the relevant features. This lets an agent consult the guides before making the first application change. See the [MCP reference](reference/features-cli-mcp.md).

### How do I update DevKit in my application? { data-feature-icons="false" }

Update your NuGet references and keep DevKit packages on the same release version. Review the [release notes](https://github.com/bridgingIT/bITdevKit/blob/main/CHANGELOG.md), restore packages, build the solution, and run your application's tests. The templates create new solutions; package updates maintain an existing one.

## Licensing and help { data-feature-icons="false" }

### Is bITdevKit free to use in commercial applications? { data-feature-icons="false" }

Yes. bITdevKit is open source under the [MIT License](https://github.com/bridgingIT/bITdevKit/blob/main/LICENSE), which permits commercial use and modification under its terms.

### Where can I get the source code? { data-feature-icons="false" }

The toolkit's source code is available in the [bITdevKit repository](https://github.com/bridgingIT/bITdevKit). The [package map](packages.md) explains how the projects are organized, and the [API reference](api/index.md) documents public types and members.

### Where can I find help or report a problem? { data-feature-icons="false" }

Use the [documentation overview](docs.md) to find a feature guide, or the [decision guides](guides.md) to compare approaches. Report reproducible problems through [repository issues](https://github.com/bridgingIT/bITdevKit/issues), including your DevKit version, the affected feature, and the steps to reproduce the behavior.
