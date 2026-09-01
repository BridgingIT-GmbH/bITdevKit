---
title: bITdevKit
hide:
  - toc
---

<!-- markdownlint-disable-file MD033 -->

<section class="hero-panel">
  <div class="hero-copy">
    <p class="eyebrow">MODULAR .NET DEVELOPMENT KIT</p>
    <h4>Build modular .NET applications with Domain-Driven Design.</h4>
    <p class="hero-lead">bITdevKit provides domain types, request handling, messaging, queueing, storage, scheduling, and presentation components for clean-architecture applications.</p>
  </div>
  <div class="hero-brand">
    <img class="brand-light" src="assets/images/bITDevKit_Icon.svg" alt="bITdevKit logo" />
    <img class="brand-dark" src="assets/images/bITDevKit_Icon.svg" alt="bITdevKit logo" />
  </div>
</section>

<div class="hero-actions">
  <a class="cta-button cta-primary" href="getting-started/">Get started</a>
  <a class="cta-button cta-secondary" href="agent-support/">Use AI agents</a>
  <a class="cta-button cta-secondary" href="templates/">Use templates</a>
  <a class="cta-button cta-secondary" href="reference/">Explore docs</a>
  <a class="cta-button cta-ghost" href="https://github.com/bridgingIT/bITdevKit">View source</a>
</div>

<!-- <div class="signal-strip">
  <span>DDD</span>
  <span>CQRS</span>
  <span>Modular Monolith</span>
  <span>Results</span>
  <span>Messaging</span>
  <span>Queueing</span>
  <span>Templates</span>
</div> -->

## Choose a start path

<div class="gateway-grid">
  <a class="gateway-card" href="getting-started/">
    <h3>Learn the devkit</h3>
    <p>Start with the GettingStarted example, the DDD introduction, and the recommended reading order.</p>
  </a>
  <a class="gateway-card" href="templates/">
    <h3>Scaffold a solution</h3>
    <p>Install the templates and create a solution or module with the standard project structure.</p>
  </a>
  <a class="gateway-card" href="agent-support/">
    <h3>Work with AI agents</h3>
    <p>Use the `bdk mcp` server to expose DevKit documentation and runtime diagnostics to coding agents.</p>
  </a>
  <a class="gateway-card" href="developer-dashboards/">
    <h3>Inspect running hosts</h3>
    <p>Open the developer dashboard for health, metrics, logs, jobs, queueing, identity, console commands, and MCP state.</p>
  </a>
  <a class="gateway-card" href="examples/">
    <h3>Explore examples</h3>
    <p>Compare the introductory GettingStarted application with DoFiesta and EventSourcingDemo.</p>
  </a>
  <a class="gateway-card" href="reference/">
    <h3>Read the docs</h3>
    <p>Browse the documentation map by capability area and feature-specific guide.</p>
  </a>
</div>

<!-- ## When to use bITdevKit

<div class="value-grid">
  <article class="value-card">
    <h3>Modular monoliths</h3>
    <p>Projects that need clear domain boundaries and module composition without splitting into many services too early.</p>
  </article>
  <article class="value-card">
    <h3>Business-heavy applications</h3>
    <p>Systems that use aggregates, policies, results, and specifications instead of DTO-first CRUD code.</p>
  </article>
  <article class="value-card">
    <h3>Operational requirements</h3>
    <p>Applications that need queueing, messaging, storage, scheduling, and diagnostic controls.</p>
  </article>
  <article class="value-card">
    <h3>Shared conventions</h3>
    <p>Codebases that use shared patterns for handlers, modules, repositories, and endpoints.</p>
  </article>
</div> -->

## Capabilities

<p class="section-linkout">
  <a class="inline-link" href="reference/">Browse the full documentation overview</a>
</p>

<div class="capability-grid">
  <a class="capability-card" href="reference/features-domain/">
    <h3>Domain</h3>
    <p>Aggregates, value objects, typed IDs, specifications, policies, and event-driven domain modeling.</p>
  </a>
  <a class="capability-card" href="reference/features-application-commands-queries/">
    <h3>Application</h3>
    <p>Commands, queries, handlers, mapping, and application orchestration.</p>
  </a>
  <a class="capability-card" href="reference/features-requester-notifier/">
    <h3>Requester &amp; Notifier</h3>
    <p>In-process dispatching for request/response and publish/subscribe flows with reusable pipeline behaviors.</p>
  </a>
  <a class="capability-card" href="reference/features-messaging/">
    <h3>Messaging</h3>
    <p>Asynchronous messaging, transports, and outbox-backed delivery.</p>
  </a>
  <a class="capability-card" href="reference/features-queueing/">
    <h3>Queueing</h3>
    <p>Single-consumer work processing, retries, archiving, and broker abstractions.</p>
  </a>
  <a class="capability-card" href="reference/features-pipelines/">
    <h3>Pipelines</h3>
    <p>Observable, multi-step workflows for in-process execution.</p>
  </a>
  <a class="capability-card" href="reference/features-orchestrations/">
    <h3>Orchestrations</h3>
    <p>Durable workflows with explicit states, activities, signals, timers, and operational endpoints.</p>
  </a>
  <a class="capability-card" href="reference/features-storage-blobs/">
    <h3>Storage</h3>
    <p>Blob, document, and file storage abstractions with monitoring, behaviors, and provider implementations.</p>
  </a>
  <a class="capability-card" href="reference/features-jobs/">
    <h3>Jobs</h3>
    <p>Durable scheduling with triggers, batches, history, maintenance jobs, and source-level integrations.</p>
  </a>
  <a class="capability-card" href="reference/features-presentation-endpoints/">
    <h3>Presentation</h3>
    <p>Minimal API endpoints, console commands, dashboards, CORS, exception handling, and Blazor application state.</p>
  </a>
  <a class="capability-card" href="developer-dashboards/">
    <h3>Dashboards</h3>
    <p>Local pages for runtime health, metrics, retained logs, jobs, queueing, and MCP sessions.</p>
  </a>
  <a class="capability-card" href="agent-support/">
    <h3>AI and agents</h3>
    <p>Local MCP support for agents that read DevKit documentation and inspect a running application.</p>
  </a>
</div>

## Compare with ASP.NET Core, MediatR, and EF Core

<div class="value-grid">
  <article class="value-card">
    <h3>Shared application model</h3>
    <p>bITdevKit defines shared APIs for results, rules, request flow, modules, and operational infrastructure.</p>
  </article>
  <article class="value-card">
    <h3>Project setup</h3>
    <p>Templates, examples, and related documentation define the initial structure and show how to extend it.</p>
  </article>
</div>

<p class="section-linkout">
  <a class="inline-link" href="why/">Read the full why bITdevKit page</a>
</p>

## Architecture

<div class="architecture-panel">
  <div>
    <p class="section-kicker">ARCHITECTURE</p>
    <h3>Clean architecture, modular vertical slices, and DDD.</h3>
    <p>Layer references flow inward from presentation and infrastructure to application and domain. Modules group related domain, application, infrastructure, and presentation code.</p>
    <a class="inline-link" href="architecture/">See the architecture map</a>
  </div>
  <div class="architecture-stack" aria-label="Architecture layers">
    <span>Presentation</span>
    <span>Infrastructure</span>
    <span>Application</span>
    <span>Domain</span>
  </div>
</div>

## Example applications

<div class="examples-grid">
  <article class="example-card">
    <h3>GettingStarted</h3>
    <p>An introductory application that demonstrates the solution structure, bootstrap sequence, and core patterns.</p>
    <a class="inline-link" href="https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted">Open example</a>
  </article>
  <article class="example-card">
    <h3>DoFiesta</h3>
    <p>A full-stack application that demonstrates operations, messaging, scheduling, and UI integration.</p>
    <a class="inline-link" href="https://github.com/bridgingIT/bITdevKit/tree/main/examples/DoFiesta">Open example</a>
  </article>
  <article class="example-card">
    <h3>EventSourcingDemo</h3>
    <p>An application that demonstrates event-sourced aggregates and persistence.</p>
    <a class="inline-link" href="https://github.com/bridgingIT/bITdevKit/tree/main/examples/EventSourcingDemo">Open example</a>
  </article>
</div>

<p class="section-linkout">
  <a class="inline-link" href="examples/">Explore the examples</a>
</p>

## Templates for solutions and modules

<div class="architecture-panel">
  <div>
    <p class="section-kicker">SCAFFOLDING</p>
    <h3>Generate the standard solution structure.</h3>
    <p>`bITdevKit` provides .NET templates that create a solution or add modules using the kit's architectural conventions.</p>
    <a class="inline-link" href="templates/">Explore the templates</a>
  </div>
  <div class="architecture-stack" aria-label="Template outputs">
    <span>Solution</span>
    <span>Module</span>
    <span>Tests</span>
  </div>
</div>

<!-- ## Common decisions

<div class="gateway-grid">
  <a class="gateway-card" href="decisions-messaging-vs-queueing/">
    <h3>Messaging or Queueing</h3>
    <p>Choose between event fan-out and single-consumer work ownership based on runtime semantics.</p>
  </a>
  <a class="gateway-card" href="decisions-repository-vs-activeentity/">
    <h3>Repository or ActiveEntity</h3>
    <p>Compare an injected persistence dependency with entity-scoped methods backed by a configured provider.</p>
  </a>
  <a class="gateway-card" href="packages/">
    <h3>Package map</h3>
    <p>Understand the repository as grouped package families instead of a flat project list.</p>
  </a>
  <a class="gateway-card" href="why/">
    <h3>Why bITdevKit</h3>
    <p>Compare the devkit with a smaller application stack.</p>
  </a>
</div> -->

<!-- ## Getting started

<div class="closing-panel">
  <p>Start with the GettingStarted example and the DDD introduction. Then use the feature guides, templates, and example applications while implementing the application.</p>
  <div class="hero-actions">
    <a class="cta-button cta-primary" href="getting-started/">Start here</a>
    <a class="cta-button cta-secondary" href="why/">Why bITdevKit</a>
    <a class="cta-button cta-secondary" href="architecture/">See architecture</a>
    <a class="cta-button cta-secondary" href="templates/">Explore templates</a>
    <a class="cta-button cta-ghost" href="https://github.com/bridgingIT/bITdevKit">Browse GitHub</a>
  </div>
</div> -->
