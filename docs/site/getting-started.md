---
title: Get started
hide:
  - navigation
  - toc
  - footer
---

<!-- markdownlint-disable-file MD033 -->

# Get started

Start with a prepared DevKit solution, or build up a Web API one feature at a time.

For questions about adoption, architecture, or licensing, see the [FAQ](faq.md).

<div class="path-fork" markdown>

<section class="path-card path-a" markdown>
<div class="path-card__art path-card__art--tree" aria-hidden="true"><span style="--i:0;width:4.6rem"></span><span style="--i:1;width:3.8rem"></span><span style="--i:2;width:3rem"></span><span style="--i:2;width:3rem"></span><span style="--i:1;width:3.8rem"></span></div>
<div class="path-card__head"><span class="path-badge" aria-hidden="true">A</span><span class="path-card__kicker">Path A / new solution</span></div>
<h3>Start from the templates</h3>

The templates generate a modular solution with application, domain, infrastructure, presentation and test projects. Use this for new applications.

<div class="path-card__cmd">dotnet new install BridgingIT.DevKit.Templates</div>
<ul class="path-card__fit">
  <li>No code yet</li>
  <li>You want the module layout from day one</li>
  <li>You want to see the full stack running</li>
</ul>
[More<span aria-hidden="true">-&gt;</span>](getting-started-template.md){ .cta-button .path-card__cta aria-label="More about starting from templates" }

</section>

<section class="path-card path-b" markdown>
<div class="path-card__art path-card__art--stack" aria-hidden="true"><span style="width:3.2rem"></span><span style="width:4rem"></span><span style="width:4.8rem"></span><span style="width:5.6rem"></span><span style="width:6.4rem"></span></div>
<div class="path-card__head"><span class="path-badge" aria-hidden="true">B</span><span class="path-card__kicker">Path B / step by step</span></div>
<h3>Build your app step by step</h3>

Start with a minimal web application or the solution you already have. Choose the features you need and build up the application step by step.

<div class="path-card__cmd">dotnet new web -n Customers.Web</div>
<ul class="path-card__fit">
  <li>A new or existing web application</li>
  <li>You need one feature, such as queueing or jobs</li>
  <li>You want to choose each feature as you go</li>
</ul>
[More<span aria-hidden="true">-&gt;</span>](getting-started-existing-app.md){ .cta-button .path-card__cta aria-label="More about building an app step by step" }

</section>

</div>

<div class="cmp" markdown>

| | <span class="path-badge is-sm is-a" aria-hidden="true">A</span>New solution | <span class="path-badge is-sm is-b" aria-hidden="true">B</span>Step by step |
| --- | --- | --- |
| Starting point | Empty directory | Minimal or existing Web API |
| First command | `dotnet new install BridgingIT.DevKit.Templates` | `dotnet new web` when starting a new app |
| Project layout | Generated module projects per layer | Start with one project; add layers when useful |
| Database | Generated module `DbContext` | Step 6 adds a SQL Server context and an initial schema. |
| Modules | Present from the start | Step 4 adds a module with appsettings configuration |
| Features | Preconfigured set | Added as you build the application |

</div>

Both paths build a web host. For a console app or a worker without HTTP, see [Non-web console application](reference/features-presentation.md#non-web-console-application).
