---
title: Documentation Overview
---

# Documentation Overview

<!-- markdownlint-disable-file MD033 -->

Start with the task in front of you. Each path links to the guides that explain the relevant APIs, runtime behavior, and trade-offs.

<div class="docs-path-grid">
  <a href="#model-a-domain"><span>01</span>Model a domain</a>
  <a href="#handle-requests-and-workflows"><span>02</span>Handle requests and workflows</a>
  <a href="#integrate-storage-and-messaging"><span>03</span>Integrate storage and messaging</a>
  <a href="#operate-and-diagnose-an-application"><span>04</span>Operate and diagnose an application</a>
  <a class="docs-path-agent" href="#work-with-ai-agents"><span>05</span>Work with AI agents</a>
</div>

## Model a domain

Use the domain libraries for behavior, invariants, persistence boundaries, and business decisions.

- [Domain](reference/features-domain.md) covers aggregates, entities, value objects, and typed identifiers.
- [Rules](reference/features-rules.md) and [Domain Policies](reference/features-domain-policies.md) model decisions and invariant checks.
- [Domain Specifications](reference/features-domain-specifications.md) encapsulate predicates and query intent.
- [Domain Repositories](reference/features-domain-repositories.md) and [ActiveEntity](reference/features-domain-activeentity.md) provide two persistence models.
- [Domain Events](reference/features-domain-events.md) and [Domain Change History](reference/features-domain-change-history.md) capture state transitions.

## Handle requests and workflows

Use the application libraries to dispatch work, return explicit outcomes, and coordinate multi-step processes.

- [Results](reference/features-results.md) defines the success and failure model used across the kit.
- [Application Commands and Queries](reference/features-application-commands-queries.md) defines request contracts and handlers.
- [Requester and Notifier](reference/features-requester-notifier.md) dispatches requests and notifications through pipeline behaviors.
- [Pipelines](reference/features-pipelines.md) coordinates observable in-process steps.
- [Orchestrations](reference/features-orchestrations.md) handles durable workflows with signals, timers, and explicit state.

## Integrate storage and messaging

Choose an integration by the data shape and delivery behavior your application needs.

- [Messaging](reference/features-messaging.md) delivers messages to subscribers. [Queueing](reference/features-queueing.md) assigns work to one consumer.
- [Blob Storage](reference/features-storage-blobs.md) stores named binary content. [Document Storage](reference/features-storage-documents.md) stores documents and metadata.
- [FileStorage](reference/features-storage-files.md) provides file-oriented providers and operations.
- [Jobs](reference/features-jobs.md) and [Job Scheduling](reference/features-jobscheduling.md) run background work now or on a schedule.
- [Modules](reference/features-modules.md) composes feature registrations and keeps module boundaries explicit.

## Operate and diagnose an application

Use the host and tooling features to expose endpoints, inspect runtime state, and find failures.

- [Presentation](reference/features-presentation.md) configures the host and its feature builders.
- [Developer Dashboards](developer-dashboards.md) exposes local pages for health, logs, metrics, jobs, queues, and other registered features.
- [Log Entries](reference/features-log-entries.md), [Metrics](reference/features-metrics.md), and [Correlation IDs](reference/features-presentation-correlationid.md) connect requests to runtime evidence.
- [Startup Tasks](reference/features-startuptasks.md) runs ordered work during application startup.
- [CLI](reference/features-cli.md) and [Console Commands](reference/features-presentation-console-commands.md) provide local operational commands.

## Work with AI agents

The `bdk mcp` command connects MCP-capable coding agents to official DevKit documentation and local runtime diagnostics.

Start with [AI Agent Support](agent-support.md) for the possibilities, installation, host registration, client configuration, and first self-test. Continue with the [AI Agent Support Guide](agent-support-guide.md) for the tool catalog, coding workflow, prompt library, project-owned diagnostics, and safety model.

Use the [MCP Reference](reference/features-cli-mcp.md) for the command and tool contract. Use [MCP Client Configuration](reference/features-cli-mcp-clients.md) for supported client files.

## Browse the complete reference

- Open the [Complete Feature Index](reference/index.md) when you know the capability name.
- Open the [API Reference](api/index.md) when you need a namespace, type, or member signature.
- Use site search to find a symbol or phrase across both the authored pages and the feature guides.
