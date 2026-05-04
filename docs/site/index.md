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

<div class="signal-strip">
  <span>DDD</span>
  <span>CQRS</span>
  <span>Modular Monolith</span>
  <span>Results</span>
  <span>Messaging</span>
  <span>Queueing</span>
  <span>Templates</span>
</div>

## Choose a start path

<div class="gateway-grid">
  <a class="gateway-card" href="getting-started/">
    <h3>Learn the devkit</h3>
    <p>Start with the GettingStarted example, the DDD introduction, and the guided first-read path.</p>
  </a>
  <a class="gateway-card" href="templates/">
    <h3>Scaffold a solution</h3>
    <p>Install the templates and create a new solution or module that already follows the expected structure.</p>
  </a>
  <a class="gateway-card" href="examples/">
    <h3>Explore examples</h3>
    <p>Move from the focused GettingStarted example to broader scenarios like DoFiesta and EventSourcingDemo.</p>
  </a>
  <a class="gateway-card" href="reference/">
    <h3>Read the docs</h3>
    <p>Browse the full public documentation map by capability area and feature-specific guide.</p>
  </a>
</div>

## When bITdevKit fits best

<div class="value-grid">
  <article class="value-card">
    <h3>Modular monoliths</h3>
    <p>Projects that need clear domain boundaries and module composition without splitting into many services too early.</p>
  </article>
  <article class="value-card">
    <h3>Business-heavy applications</h3>
    <p>Systems where aggregates, policies, results, and specifications matter more than simple DTO-first CRUD code.</p>
  </article>
  <article class="value-card">
    <h3>Operationally realistic platforms</h3>
    <p>Applications that need queueing, messaging, storage, scheduling, and diagnostic control as first-class concerns.</p>
  </article>
  <article class="value-card">
    <h3>Teams that need consistency</h3>
    <p>Codebases where shared patterns for handlers, modules, repositories, and endpoints reduce architectural drift.</p>
  </article>
</div>

## Capabilities for real-world application needs

<p class="section-linkout">
  <a class="inline-link" href="reference/">Browse the full documentation overview</a>
</p>

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

## Why not just plain ASP.NET Core + MediatR + EF Core?

<div class="value-grid">
  <article class="value-card">
    <h3>More than a library stack</h3>
    <p>bITdevKit provides a coherent model for results, rules, request flow, modules, and operational infrastructure instead of leaving each project to compose its own conventions.</p>
  </article>
  <article class="value-card">
    <h3>Developer guidance with reusable defaults</h3>
    <p>Examples, templates, and aligned documentation reduce the amount of architectural assembly work needed at project start.</p>
  </article>
</div>

<p class="section-linkout">
  <a class="inline-link" href="why/">Read the full why bITdevKit page</a>
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
    <a class="inline-link" href="architecture/">See the architecture map</a>
  </div>
  <div class="architecture-stack" aria-label="Architecture layers">
    <span>Presentation</span>
    <span>Infrastructure</span>
    <span>Application</span>
    <span>Domain</span>
  </div>
</div>

## Request flow in practice

<div class="snippet-panel" markdown="1">

```csharp
app.MapPost("/customers", async (CustomerCreateModel model, IRequester requester) =>
{
    var command = new CustomerCreateCommand(model);
    var result = await requester.SendAsync(command);

    return result.MapHttpCreated();
});
```

</div>

<p class="section-linkout">
  <a class="inline-link" href="architecture/">See the request flow and layer map</a>
</p>

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
  <article class="example-card">
    <h3>EventSourcingDemo</h3>
    <p>A focused example for exploring how event-sourcing-oriented concepts fit into the broader development kit.</p>
    <a class="inline-link" href="https://github.com/bridgingIT/bITdevKit/tree/main/examples/EventSourcingDemo">Open example</a>
  </article>
</div>

<p class="section-linkout">
  <a class="inline-link" href="examples/">Browse all examples and suggested progression</a>
</p>

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

## Common early decisions

<div class="gateway-grid">
  <a class="gateway-card" href="decisions-messaging-vs-queueing/">
    <h3>Messaging vs Queueing</h3>
    <p>Choose between event fan-out and single-consumer work ownership based on runtime semantics.</p>
  </a>
  <a class="gateway-card" href="decisions-repository-vs-activeentity/">
    <h3>Repository vs ActiveEntity</h3>
    <p>Pick between a richer DDD-oriented abstraction and a simpler persistence style for faster CRUD scenarios.</p>
  </a>
  <a class="gateway-card" href="packages/">
    <h3>Package map</h3>
    <p>Understand the repository as grouped package families instead of a flat project list.</p>
  </a>
  <a class="gateway-card" href="why/">
    <h3>Why bITdevKit</h3>
    <p>See when the devkit pays off and when a simpler stack may be enough.</p>
  </a>
</div>

## Getting started

<div class="closing-panel">
  <p>
    The fastest path into bITdevKit is to start with the GettingStarted example, read the DDD
    introduction, and then move into the feature docs, templates, and example applications as the
    implementation questions become more concrete.
  </p>
  <div class="hero-actions">
    <a class="cta-button cta-primary" href="getting-started/">Start Here</a>
    <a class="cta-button cta-secondary" href="why/">Why bITdevKit</a>
    <a class="cta-button cta-secondary" href="architecture/">See Architecture</a>
    <a class="cta-button cta-secondary" href="templates/">Explore Templates</a>
    <a class="cta-button cta-ghost" href="https://github.com/bridgingIT/bITdevKit">Browse GitHub</a>
  </div>
</div>
