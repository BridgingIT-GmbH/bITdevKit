---
title: bITdevKit
hide:
  - toc
---

<section class="hero-panel">
  <div class="hero-copy">
    <p class="eyebrow">MODULAR .NET DEVELOPMENT KIT</p>
    <h4>Empowering developers with modular components for modern application development, centered around Domain-Driven Design principles.</h4>
    <p class="hero-lead">
      bITdevKit brings together domain modeling, requests, messaging, queueing, storage, scheduling,
      and presentation patterns into a practical toolkit for real-world systems built with clean
      architecture and DDD.
    </p>
  </div>
  <div class="hero-brand">
    <img class="brand-light" src="assets/images/bITDevKit_Icon.svg" alt="bITdevKit logo" />
    <img class="brand-dark" src="assets/images/bITDevKit_Icon.svg" alt="bITdevKit logo" />
  </div>
</section>

<div class="hero-actions">
  <a class="cta-button cta-primary" href="getting-started/">Get Started</a>
  <a class="cta-button cta-secondary" href="templates/">Use Templates</a>
  <a class="cta-button cta-secondary" href="reference/">Explore Docs</a>
  <a class="cta-button cta-ghost" href="https://github.com/bridgingIT/bITdevKit">View Source</a>
</div>

## Capabilities for real-world application needs

<div class="capability-grid">
  <a class="capability-card" href="reference/features-domain/">
    <h3>Domain</h3>
    <p>Aggregates, value objects, typed ids, specifications, policies and event-driven domain modeling.</p>
  </a>
  <a class="capability-card" href="reference/features-application-commands-queries/">
    <h3>Application</h3>
    <p>Commands, queries, handlers, mapping and explicit application orchestration with clear boundaries.</p>
  </a>
  <a class="capability-card" href="reference/features-requester-notifier/">
    <h3>Requester &amp; Notifier</h3>
    <p>In-process dispatching for request/response and publish/subscribe flows with reusable pipeline behaviors.</p>
  </a>
  <a class="capability-card" href="reference/features-messaging/">
    <h3>Messaging</h3>
    <p>Durable asynchronous messaging and outbox-backed delivery for decoupled application communication.</p>
  </a>
  <a class="capability-card" href="reference/features-queueing/">
    <h3>Queueing</h3>
    <p>Single-consumer work processing with operational visibility, retry/archive control and broker abstractions.</p>
  </a>
  <a class="capability-card" href="reference/features-pipelines/">
    <h3>Pipelines</h3>
    <p>Structured, observable multi-step workflows for in-process execution with low-friction defaults.</p>
  </a>
  <a class="capability-card" href="reference/features-storage-documents/">
    <h3>Storage</h3>
    <p>Document and file storage abstractions, monitoring, behaviors and provider-based extensibility.</p>
  </a>
  <a class="capability-card" href="reference/features-jobscheduling/">
    <h3>Scheduling</h3>
    <p>Startup tasks, background jobs and operational scheduling capabilities for hosted applications.</p>
  </a>
  <a class="capability-card" href="reference/features-presentation-endpoints/">
    <h3>Presentation</h3>
    <p>Minimal API endpoints, console commands, CORS, exception handling and Blazor application state support.</p>
  </a>
</div>

<p class="section-linkout">
  <a class="inline-link" href="reference/">Browse the full documentation overview</a>
</p>

## Designed for modular, maintainable .NET systems

<div class="architecture-panel">
  <div>
    <p class="section-kicker">ARCHITECTURE</p>
    <h3>Clean architecture, modular vertical slices and DDD by default.</h3>
    <p>
      bITdevKit is shaped around clear boundaries between domain, application, infrastructure and
      presentation. It supports modular systems where cross-cutting building blocks stay reusable
      while business logic remains explicit and testable.
    </p>
    <a class="inline-link" href="reference/introduction-ddd-guide/">Read the architecture-oriented introduction</a>
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
    <p>The canonical onboarding project for learning the devkit through a focused end-to-end example before building your own solution.</p>
    <a class="inline-link" href="https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted">Open example</a>
  </article>
  <article class="example-card">
    <h3>DoFiesta</h3>
    <p>A richer example application used throughout the repository to show operational, messaging, scheduling and UI integration scenarios.</p>
    <a class="inline-link" href="https://github.com/bridgingIT/bITdevKit/tree/main/examples/DoFiesta">Open example</a>
  </article>
</div>

## Templates for new solutions and modules

<div class="architecture-panel">
  <div>
    <p class="section-kicker">SCAFFOLDING</p>
    <h3>Start from a working structure instead of assembling it by hand.</h3>
    <p>
      `bITdevKit` ships with .NET templates that can scaffold a full solution or add new modules
      using the kit's architectural conventions. That makes it easier to move from learning to a
      real project without rebuilding the same structure manually.
    </p>
    <a class="inline-link" href="templates/">Explore the templates</a>
  </div>
  <div class="architecture-stack" aria-label="Template outputs">
    <span>Solution</span>
    <span>Module</span>
    <span>Tests</span>
    <span>Presentation</span>
  </div>
</div>

## Getting started

<div class="closing-panel">
  <p>
    The fastest path into bITdevKit is to start with the curated docs, follow one or two feature
    areas and then move into the repository and examples for implementation detail.
  </p>
  <div class="hero-actions">
    <a class="cta-button cta-primary" href="reference/">Start with the Docs</a>
    <a class="cta-button cta-secondary" href="templates/">Explore Templates</a>
    <a class="cta-button cta-secondary" href="https://github.com/bridgingIT/bITdevKit">Browse GitHub</a>
  </div>
</div>
