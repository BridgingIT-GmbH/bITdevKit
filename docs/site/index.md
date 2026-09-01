---
title: bITdevKit
hide:
  - toc
---

<!-- markdownlint-disable-file MD033 -->

<section class="hero-panel">
  <div class="hero-copy">
    <p class="eyebrow">MODULAR .NET DEVELOPMENT KIT</p>
    <h1>bITdevKit</h1>
    <p class="hero-statement">Build modular .NET applications with Domain-Driven Design.</p>
    <p class="hero-lead">Use shared domain types, request handling, messaging, storage, scheduling, and host tooling without rebuilding the same application infrastructure.</p>
    <div class="hero-actions">
      <a class="cta-button cta-primary" href="getting-started/">Create a project</a>
      <a class="cta-button cta-secondary" href="docs/">Browse docs</a>
      <a class="cta-button cta-ghost" href="api/">API reference</a>
    </div>
  </div>
  <div class="hero-brand">
    <img src="assets/images/bITDevKit_Icon.svg" alt="" />
  </div>
</section>

## Start in two commands

Install the templates, then create a solution with its first module.

```powershell
dotnet new install BridgingIT.DevKit.Templates
dotnet new bdksolution --SolutionName SolutionName --ModuleName Core --allow-scripts yes -o ./projects/SolutionName
```

The template creates `./projects/SolutionName/SolutionName.slnx` with application, domain, infrastructure, presentation, and test projects.

<p class="section-linkout"><a class="inline-link" href="getting-started/">Continue with the Quickstart</a></p>

## Find the part you need

<div class="capability-grid capability-grid--home">
  <a class="capability-card" href="docs/#model-a-domain">
    <h3>Domain modeling</h3>
    <p>Aggregates, value objects, rules, policies, specifications, repositories, and domain events.</p>
  </a>
  <a class="capability-card" href="docs/#handle-requests-and-workflows">
    <h3>Application flow</h3>
    <p>Commands, queries, results, requester pipelines, events, and durable orchestrations.</p>
  </a>
  <a class="capability-card" href="docs/#integrate-storage-and-messaging">
    <h3>Infrastructure integration</h3>
    <p>Messaging, queueing, files, blobs, documents, databases, and scheduled work.</p>
  </a>
  <a class="capability-card" href="docs/#operate-and-diagnose-an-application">
    <h3>Operations and tooling</h3>
    <p>Dashboards, health, logs, metrics, startup tasks, CLI commands, and MCP diagnostics.</p>
  </a>
</div>

## Give your coding agent DevKit context

<aside class="agent-callout">
  <div>
    <p class="section-kicker">AI AGENT SUPPORT</p>
    <h3>Put the docs and a running application in the same conversation.</h3>
    <p><code>bdk mcp</code> lets MCP-capable coding agents search the official DevKit documentation and inspect diagnostics exposed by a local DevKit host.</p>
  </div>
  <a class="cta-button cta-agent" href="agent-support/">Set up AI Agent Support</a>
</aside>

## Continue from working code

<nav class="next-step-strip" aria-label="Next steps">
  <a href="https://github.com/BridgingIT-GmbH/bITdevKit.Examples.GettingStarted">Open GettingStarted</a>
  <a href="examples/">Explore all examples</a>
  <a href="why/">See when bITdevKit fits</a>
</nav>
