---
title: Get started
hide:
  - navigation
  - toc
  - footer
---

<!-- markdownlint-disable-file MD033 -->

# Get started

Start a new solution from the templates, or add DevKit to an ASP.NET Core app you already have.

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
<div class="path-card__head"><span class="path-badge" aria-hidden="true">B</span><span class="path-card__kicker">Path B / existing app</span></div>
<h3>Add DevKit to your app</h3>

Add DevKit to a solution you already have. Choose the features you need and introduce them gradually, while continuing to build on your existing application.

<div class="path-card__cmd">dotnet add package BridgingIT.DevKit.Presentation.Web</div>
<ul class="path-card__fit">
  <li>An ASP.NET Core app already in development or production</li>
  <li>You need one feature, such as queueing or jobs</li>
  <li>You can't restructure the solution</li>
</ul>
[More<span aria-hidden="true">-&gt;</span>](getting-started-existing-app.md){ .cta-button .path-card__cta aria-label="More about adding DevKit to an existing app" }

</section>

</div>

<div class="cmp" markdown>

| | <span class="path-badge is-sm is-a" aria-hidden="true">A</span>New solution | <span class="path-badge is-sm is-b" aria-hidden="true">B</span>Existing app |
| --- | --- | --- |
| Starting point | Empty directory | Running ASP.NET Core application |
| First command | `dotnet new install BridgingIT.DevKit.Templates` | `dotnet add package BridgingIT.DevKit.Presentation.Web` |
| Project layout | Generated module projects per layer | Unchanged |
| Database | Generated module `DbContext` | Basics step 8 registers the application's `DbContext` through DevKit. It implements one interface per durable feature. |
| Modules | Present from the start | Basics step 7 adds one module to the existing project |
| Features | Preconfigured set | Added one recipe at a time, after the basics |

</div>

Both paths build a web host. For a console app or a worker without HTTP, see [Non-web console application](reference/features-presentation.md#non-web-console-application).
