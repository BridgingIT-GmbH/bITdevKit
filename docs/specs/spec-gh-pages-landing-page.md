---
status: draft
---

# Design Specification: GitHub Pages Site for bITdevKit

> This document defines the target GitHub Pages experience for bITdevKit: a polished public landing page combined with an extensive, curated technical documentation site. It describes the intended end state. Implementation can later be split into phases such as v1 and vNext, but those phases are intentionally not defined here.

[TOC]

## 1. Introduction

bITdevKit should have a public website at `https://bridgingit-gmbh.github.io/bITdevKit/` that serves two purposes at the same time:

- act as the public front door for the project
- provide a rich, searchable technical documentation experience

The site is not meant to be a raw rendering of the repository and it is not meant to be a documentation-only shell. It should present bITdevKit like a serious .NET framework/toolkit while still remaining lightweight, maintainable, and Markdown-first.

The resulting site should combine:

- a product-style landing page
- curated feature and architecture documentation
- strong navigation into relevant repository resources
- a design that feels intentional and modern rather than like a stock docs theme

---

## 2. Goals

The GitHub Pages site shall:

- replace the current `404` at the public Pages URL
- present bITdevKit as a modular .NET development kit/framework
- help first-time visitors quickly understand what the project is, what problems it solves, and why it is useful
- provide a polished landing page with strong calls to action
- provide extensive technical documentation for the public feature set
- keep documentation discoverable through clear navigation and search
- remain maintainable by using Markdown as the primary content authoring format
- build locally (Docker) without requiring Python to be installed directly on the developer machine
- build and publish automatically through GitHub Actions after code is pushed to GitHub remote

---

## 3. Non-Goals

The site shall NOT:

- publish `src/**` Markdown files
- publish `docs/specs/**`
- publish `docs/adr/**`
- publish `docs/presentations/**`
- depend on Azure DevOps for docs build or docs deployment
- require developers to install Python, `pip`, or MkDocs directly on their host machine
- mirror the entire repository structure one-to-one
- behave as a general company marketing site for BridgingIT GmbH

---

## 4. Audience

The site primarily serves:

- .NET developers evaluating or working with bITdevKit
- architects and technical leads assessing architectural fit
- contributors and adopters looking for feature guidance
- developers searching for concrete entry points into the framework

The tone should therefore be:

- technical
- clear
- practical
- confident
- concise

The tone should not be:

- sales-heavy
- generic
- buzzword-driven
- enterprise brochure-like

---

## 5. Site Scope

### 5.1 Public site content

The public GitHub Pages site shall contain:

- a homepage at `/`
- landing and overview pages authored specifically for the public site
- curated technical documentation pages for the framework
- shared assets such as logo, icons, images, and custom CSS

### 5.2 Excluded content

The following repository content shall not be part of the public MkDocs site:

- `docs/specs/`
- `docs/adr/`
- `docs/presentations/`
- `src/**`

These areas may still exist in the repository for internal design, engineering, or presentation work, but they are explicitly outside the public Pages information architecture.

---

## 6. Site Generation Approach

### 6.1 Generator choice

The site shall be generated with MkDocs using Material for MkDocs as the base theme.

MkDocs is chosen because it provides:

- Markdown-first authoring
- clean static-site generation
- search and navigation
- easy GitHub Pages hosting
- strong extensibility for custom homepage styling and branded presentation

### 6.2 Source model

The public site shall use a dedicated MkDocs content source directory separate from the repository’s internal `docs/` structure.

Recommended structure:

```text
/mkdocs.yml
/docs/site/
  index.md
  getting-started/
  architecture/
  features/
  assets/
    images/
    stylesheets/
```

`docs/site/` is the only MkDocs input directory.

Existing reusable branding assets from `docs/assets/` may be copied, referenced, or curated into the public site asset structure as part of implementation.

### 6.3 Content ownership model

The public site content shall be curated rather than directly exposing all repository Markdown files.

That means:

- landing-page content is authored specifically for the site
- public documentation pages are selected, adapted, or synchronized from suitable repository docs
- internal or low-signal documentation remains outside the public site

The public site is therefore a curated documentation product, not just a renderer over arbitrary repository files.

### 6.4 README as input source

The root `README.md` may and should be used as an input source for the public site.

However, it shall not be treated as publish-ready source content without curation.

The intended rule is:

- `README.md` provides useful positioning, orientation, links, and project-summary material
- homepage and getting-started content may be derived from that material
- the public site may reuse, adapt, condense, or restructure README content
- the README must also be updated so that it reflects the current public site and current documentation structure

The README is therefore both:

- an input to the Pages experience
- a document that must be modernized and synchronized as part of the overall public documentation effort

---

## 7. Build and Deployment Model

### 7.1 Local build and preview

Local build and preview shall be Docker-based only.

The canonical build tool shall be the official `squidfunk/mkdocs-material` container image.

The local workflow shall support:

- local preview/serve
- local production build

without requiring a Python installation on the host machine.

### 7.2 Azure DevOps

Azure DevOps shall not be used for GitHub Pages build or deployment.

The main project may continue to use Azure DevOps for other CI/CD concerns, but the public GitHub Pages site shall be built and published only from GitHub-side automation.

### 7.3 GitHub Actions

GitHub Actions shall be the only automation responsible for Pages build and deployment.

The workflow shall:

- trigger on pushes to the GitHub default branch
- optionally support manual dispatch
- build the site inside Docker
- publish the generated static output to the `gh-pages` branch

### 7.4 GitHub Pages publishing mode

GitHub Pages shall use branch-based publishing from:

- branch: `gh-pages`
- folder: `/ (root)`

The GitHub Actions workflow shall be responsible for keeping `gh-pages` up to date with the generated static output.

---

## 8. Information Architecture

The site shall have two major layers:

1. a public landing and overview experience
2. a deep technical docs experience

### 8.1 Top-level navigation

The top-level navigation should be visitor-oriented and product-oriented.

Recommended top-level navigation:

- Home
- Why
- Features
- Architecture
- Getting Started
- Docs
- GitHub
- NuGet

The exact labels may vary slightly during implementation, but the structure shall preserve both landing content and deep docs access.

### 8.2 Docs navigation

Once a user enters the documentation area, the site shall provide a documentation-style navigation model with:

- left-side or section navigation
- page hierarchy grouped by topic
- search across public documentation pages

The docs experience shall feel like a real framework documentation site, not just a set of disconnected pages.

### 8.3 Canonical public routes

The public site should expose stable, human-readable routes for the major public areas.

Recommended canonical route families:

- `/` for the landing page
- `/getting-started/` for first-use guidance
- `/architecture/` for architecture overview and conceptual framing
- `/features/` for the main public framework feature documentation
- `/testing/` for testing-related public guides
- `/docs/` for the broader curated documentation hub when a hub page is useful

The exact internal source file names may differ, but the public routing model should remain stable and visitor-friendly.

### 8.4 Primary user journeys

The site shall support at least these user journeys:

1. Evaluate the framework
   - enter at homepage
   - understand value proposition
   - inspect architecture and capability overview
   - follow into getting started or features
2. Learn a concept
   - enter at docs or search
   - open a feature or concept page
   - follow cross-links to adjacent concepts
3. Find implementation guidance
   - enter through a feature page
   - read usage-oriented guidance
   - jump to source code in GitHub when deeper detail is needed
4. Start contributing or trial adoption
   - enter from README or GitHub
   - use homepage and getting-started entry points
   - move into curated docs rather than browsing repository folders manually

---

## 9. Homepage Information Architecture

The homepage shall act as the main public entry point for bITdevKit.

### 9.1 Exact homepage section order

The homepage should be implemented in this order:

1. Top navigation
2. Hero
3. Trust and quick-signal strip
4. Why bITdevKit
5. Capability overview
6. Architecture overview
7. Example applications
8. Documentation gateway
9. Closing call to action
10. Footer

The page should read as one continuous product story:

- what the kit is
- why it matters
- what it contains
- how it is structured
- where to go next

### 9.2 Top navigation

The homepage top navigation should use exact visitor-facing labels or very close equivalents:

- Home
- Why
- Features
- Architecture
- Getting Started
- Docs
- GitHub
- NuGet

Top navigation behavior should be:

- `Home` scrolls to top or routes to `/`
- `Why` jumps to the homepage value section
- `Features` jumps to the homepage capability section or routes to `/features/`
- `Architecture` routes to `/architecture/`
- `Getting Started` routes to `/getting-started/`
- `Docs` routes to the public docs hub or primary docs landing page
- `GitHub` opens the repository
- `NuGet` opens the package listing/search target

A theme toggle for light/dark mode should be visible in the top navigation or persistent page chrome.

### 9.3 Hero

The hero shall be the strongest piece of homepage messaging and should immediately answer what bITdevKit is.

The hero should contain:

- an eyebrow or short context line such as `.NET Development Kit` or `Modular Building Blocks for .NET`
- a primary headline
- a supporting paragraph
- a primary CTA row
- a secondary quick-link row
- a subtle visual treatment that feels technical and deliberate

Recommended primary headline direction:

- `Build modular .NET applications with reusable architectural building blocks.`

Alternative acceptable headline direction:

- `A modular .NET development kit for clean architecture, DDD, and real-world application building.`

Recommended supporting copy direction:

- explain that bITdevKit helps teams compose application architecture from reusable building blocks for domain modeling, requests, messaging, queueing, storage, scheduling, and presentation concerns
- emphasize maintainability, consistency, and development speed
- avoid vague claims like “next-generation” or “revolutionary”

The hero shall use these CTA targets:

- primary CTA: `Get Started` -> `/getting-started/`
- secondary CTA: `Explore Docs` -> public docs landing page
- tertiary CTA: `View on GitHub` -> repository root

Optional additional CTA:

- `Browse Packages` -> NuGet search/listing target

The hero may include a small secondary badge or quick-link row for:

- `MIT Licensed`
- `NuGet Packages`
- `Examples Included`
- `GitHub Source`

These should be factual trust signals, not decorative clutter.

### 9.4 Trust and quick-signal strip

Immediately below the hero, the homepage should provide a compact strip of factual project signals.

This strip should highlight a small number of concrete properties such as:

- modular architecture
- DDD-oriented building blocks
- open source / MIT
- NuGet packages
- examples included

If metrics or badges are shown, they should be limited to signals that are already available and stable, such as:

- GitHub repository
- GitHub Actions status
- NuGet package availability

This area should not attempt to imitate customer-logo walls unless there is real, curated proof data to support it.

### 9.5 Why bITdevKit

This section shall explain the framework’s practical reason for existing.

It should answer three questions:

1. What problems does it solve?
2. What does it standardize?
3. Why would a team adopt it instead of assembling everything ad hoc?

Recommended structure:

- short section headline such as `Why bITdevKit`
- one concise lead paragraph
- three to four value cards or columns

Recommended value themes:

- `Architectural consistency`
- `Reusable cross-cutting building blocks`
- `Faster application composition`
- `Clear boundaries for maintainable systems`

Each value block should connect directly to real capabilities already present in the framework rather than generic benefits.

### 9.6 Capability overview

This section shall be the main homepage bridge into the public docs.

It should use a grid of cards with:

- capability name
- one short explanatory sentence
- one docs link

The homepage shall present these capability cards:

- `Domain`
  - link target: domain/core concepts area
- `Application`
  - link target: commands, queries, and application events area
- `Requester & Notifier`
  - link target: requester/notifier docs
- `Messaging`
  - link target: messaging docs
- `Queueing`
  - link target: queueing docs
- `Pipelines`
  - link target: pipelines docs
- `Storage`
  - link target: document/file/storage monitoring docs
- `Scheduling`
  - link target: startup tasks and job scheduling docs
- `Presentation`
  - link target: endpoints, CORS, exception handling, app state docs
- `Common Building Blocks`
  - link target: common/shared guides area

Card copy should be short and concrete. It should describe what a visitor can do with the capability, not just restate the category title.

The card grid should be scannable on desktop and stack cleanly on mobile.

### 9.7 Architecture overview

This section shall explain the framework shape in a visually digestible way.

It should communicate:

- onion / clean architecture
- modular vertical slices
- domain/application/infrastructure/presentation boundaries
- reusable conventions across modules

Recommended content structure:

- section headline such as `Designed for modular, maintainable .NET systems`
- short explanatory paragraph
- simplified architecture diagram or layered visual block
- one CTA to `/architecture/`

This section should not attempt to reproduce a full architecture document. Its purpose is orientation and motivation.

### 9.8 Example applications

This section shall show that the framework is grounded in real usage and examples.

It should highlight the example applications already present in the repository where relevant:

- `DoFiesta`
- `DinnerFiesta`
- `WeatherForecast`

For each example block, the homepage should provide:

- example name
- one short sentence describing its role
- a link to the relevant GitHub location or public docs page if one exists later

This section may also mention the broader role of examples:

- learning the framework
- seeing integration patterns
- understanding real composition of modules and infrastructure

### 9.9 Documentation gateway

This section shall serve visitors who are ready to move from overview into learning.

It should contain a small set of prominent entry cards or buttons for:

- `Getting Started`
- `Core Concepts`
- `Feature Guides`
- `Architecture`
- `Testing`
- `Source Code`

Recommended behavior:

- each entry point has a one-line explanation
- each entry point routes to a stable public path or intentional external GitHub destination
- this section should feel like a curated launchpad, not a raw file index

### 9.10 Closing call to action

The homepage should end with a strong, simple next-step section.

Recommended closing CTA hierarchy:

- primary: `Start with the Docs`
- secondary: `View Source on GitHub`
- optional tertiary: `Browse NuGet Packages`

Closing copy should reinforce that bITdevKit is both:

- a practical framework/toolkit
- an openly inspectable codebase with documentation and examples

### 9.11 Footer

The footer should be functional and concise.

It should include:

- project name
- GitHub link
- NuGet link
- README link
- license link
- optional changelog link

The footer should not introduce entirely new navigation concepts that were not already present higher on the page.

### 9.12 Homepage content rules

The homepage copy shall:

- stay concise
- avoid long prose blocks
- avoid enterprise-sales clichés
- use technically meaningful wording
- prefer action-oriented CTAs
- reflect the framework’s actual capabilities as represented in the repository docs

The homepage shall not:

- duplicate full documentation page content
- expose internal design/spec language
- rely on unverified claims, customer proof, or invented ecosystem metrics

### 9.13 Homepage acceptance expectations

The homepage should let a first-time visitor answer these questions within one screenful and a short scroll:

- What is bITdevKit?
- Who is it for?
- What major capabilities does it include?
- How is it architected?
- Where do I go next?

---

## 10. Public Documentation Scope

The public documentation area shall be extensive, but curated.

### 10.1 Included public doc categories

The public site documentation should include curated content derived from or based on:

- feature guides from `docs/features-*.md`
- common/shared guides from `docs/common-*.md`
- testing guides from `docs/testing-*.md`
- introductory content such as `docs/introduction-ddd-guide.md`
- additional site-authored overview or getting-started pages as needed

### 10.2 Excluded categories

The following documentation categories shall remain excluded from the public site:

- design specifications in `docs/specs/`
- ADR content in `docs/adr/`
- presentation/deck content in `docs/presentations/`
- package-level Markdown in `src/**`

### 10.3 Documentation structure

The public docs should be organized into clear reader journeys rather than file-system-shaped buckets.

Recommended public docs groupings:

- Getting Started
- Core Concepts
- Feature Guides
- Architecture
- Testing
- Reference Links

### 10.3.1 Proposed public docs map

The public documentation structure should be derived from the current repository documentation set as follows.

**Getting Started**

- site-authored getting-started overview page
- curated orientation derived from `README.md`
- curated orientation derived from `docs/INDEX.md`
- optional use of `docs/introduction-ddd-guide.md` where appropriate

**Core Concepts**

- Domain from `docs/features-domain.md`
- Results from `docs/features-results.md`
- Requester and Notifier from `docs/features-requester-notifier.md`
- Modules from `docs/features-modules.md`
- Presentation Endpoints from `docs/features-presentation-endpoints.md`

These should form the default conceptual learning path for new readers.

**Common Building Blocks**

- `docs/common-extensions.md`
- `docs/common-utilities.md`
- `docs/common-serialization.md`
- `docs/common-options-builders.md`
- `docs/common-mapping.md`
- `docs/common-caching.md`
- `docs/common-observability-tracing.md`
- `docs/features-utilities.md`

**Domain and Application**

- `docs/features-domain.md`
- `docs/features-domain-events.md`
- `docs/features-event-sourcing.md`
- `docs/features-domain-repositories.md`
- `docs/features-domain-specifications.md`
- `docs/features-domain-activeentity.md`
- `docs/features-domain-policies.md`
- `docs/features-rules.md`
- `docs/features-results.md`
- `docs/features-application-commands-queries.md`
- `docs/features-application-events.md`
- `docs/features-application-dataporter.md`

**Execution, Messaging, and Modularity**

- `docs/features-requester-notifier.md`
- `docs/features-messaging.md`
- `docs/features-queueing.md`
- `docs/features-notifications.md`
- `docs/features-modules.md`
- `docs/features-pipelines.md`
- `docs/features-filtering.md`
- `docs/features-extensions.md`

**Security and Access**

- `docs/features-entitypermissions.md`
- `docs/features-identityprovider.md`

**Presentation and Host**

- `docs/features-presentation-endpoints.md`
- `docs/features-presentation-console-commands.md`
- `docs/features-presentation-cors.md`
- `docs/features-presentation-exception-handling.md`
- `docs/features-presentation-appstate.md`

**Storage, Scheduling, and Operations**

- `docs/features-startuptasks.md`
- `docs/features-jobscheduling.md`
- `docs/features-storage-documents.md`
- `docs/features-storage-files.md`
- `docs/features-storage-monitoring.md`
- `docs/features-log-entries.md`

**Testing**

- `docs/testing-fake-authentication.md`
- `docs/testing-common-xunit.md`

This mapping is the intended public documentation inventory unless a later documentation review explicitly removes or combines pages.

### 10.3.2 Documentation entry hierarchy

The public docs should expose a layered reading hierarchy:

1. homepage overview
2. getting-started and architecture entry points
3. grouped feature/category indexes
4. individual deep-dive pages

Users should not need to start from an alphabetical list of files.

### 10.3.3 Cross-linking expectations

Documentation pages shall cross-link intentionally between:

- conceptual prerequisites
- adjacent features
- testing guidance
- source code references

Cross-linking should support learning flow, not just file discoverability.

### 10.4 Documentation behavior

The public documentation area shall:

- use consistent navigation
- support search
- use readable headings and cross-links
- avoid broken repo-local filesystem links
- link to source code through GitHub URLs where source references are useful

---

## 11. Content Source Policy

The site shall distinguish between:

- public documentation content suitable for GitHub Pages
- internal or engineering-facing repository content not suitable for public Pages

### 11.1 Public content policy

A page is suitable for the public site when it:

- explains a public framework concept, feature, or usage pattern
- helps readers evaluate or adopt the framework
- is understandable without internal engineering context
- does not depend on unpublished local-only references

### 11.2 Internal content policy

A page is not suitable for the public site when it is primarily:

- a design note
- an ADR
- a work-in-progress spec
- a presentation artifact
- a package-local internal note

### 11.3 Public content transformation policy

Repository documents chosen for the public site do not need to be copied verbatim.

They may be:

- lightly rewritten for clarity
- restructured for public navigation
- split into smaller pages when necessary
- combined into curated overview pages
- augmented with site-authored introductory context

The public site should optimize for reader experience, not for one-to-one file preservation.

### 11.4 Synchronization policy

When a public site page is derived from an existing repository doc, the implementation should define a clear ownership rule so future maintenance is predictable.

Preferred rule:

- public-facing canonical content should eventually live in the MkDocs source tree
- repository-only internal docs remain under `docs/`
- during migration, temporary duplication is acceptable only when explicitly tracked

This avoids long-term ambiguity about which file is authoritative.

---

## 12. README Relationship

The root `README.md` remains the GitHub repository front page and shall stay aligned with the public site.

The README shall be treated as both a source input and a maintained public artifact.

The README shall be modernized so that it:

- uses current terminology and links
- matches the site’s positioning
- points prominently to the GitHub Pages site
- acts as a concise GitHub-facing summary rather than trying to replace the full website
- can feed curated homepage and getting-started material without requiring large corrective rewrites every time

The README and the website should complement each other:

- the README is the repo front door
- the Pages site is the polished public experience

### 12.1 README update expectations

The README update should address at least the following:

- remove stale or broken docs links
- align feature/category names with the public site and current docs inventory
- simplify or modernize overly dated structure where needed
- preserve its role as a concise repository overview rather than a long-form website substitute

### 12.2 README-to-site relationship rule

The implementation should avoid both extremes:

- the site must not simply mirror the README verbatim
- the README must not drift so far from the public site that they appear to describe different projects

The intended relationship is:

- shared core positioning
- shared feature vocabulary
- different depth and presentation

---

## 13. Visual Design Direction

The site shall feel like a framework/product website with strong documentation capabilities, not like an unstyled documentation export.

### 13.1 Design goals

The design should be:

- modern
- technical
- distinctive
- credible
- open-source friendly
- clean on both desktop and mobile

### 13.2 Design cues

The site may take inspiration from polished framework landing pages that combine:

- strong hero sections
- card-based capability overviews
- visible calls to action
- balanced white space and section rhythm
- easy transitions from landing content into docs

The design should not copy another site directly. It should reflect bITdevKit’s own identity.

### 13.3 Implementation direction

The implementation shall use:

- Material for MkDocs as the structural base
- custom CSS and theme overrides for brand expression
- the existing bITdevKit logo/identity assets where appropriate

### 13.3.1 Existing brand asset inventory

The repository already contains reusable visual assets in `docs/assets/`, including:

- `bITDevKit_Logo.svg`
- `bITDevKit_Logo_dark.svg`
- `bITDevKit_Logo_light.svg`
- `bITDevKit_Logo_white.svg`
- `bITDevKit_Logo_whiteblue.svg`
- `bITDevKit_Logo_variant.svg`
- `bITDevKit_Icon.svg`
- `bITDevKit_Icon_variants.svg`
- `bITDevKit_Text.svg`
- `bITDevKit_Text_variants.svg`

These assets should be treated as the preferred source for public-site branding before creating new derived branding assets.

### 13.3.2 Intended asset usage

The public site should reuse the existing SVG assets intentionally:

- a full logo variant for header/navigation branding
- dark/light-appropriate logo variants where needed
- icon variants for favicon, compact branding, or feature-area accents
- text/logo variants when a more horizontal lockup is needed

PNG assets may remain useful as compatibility fallbacks, but SVG should be preferred for the public site where supported.

### 13.4 Theme modes

The public site shall support both light and dark presentation modes.

The theme behavior shall satisfy the following expectations:

- the site shall render well in both light and dark modes
- users shall be able to switch between light and dark theme modes
- the selected mode should persist across page navigation
- the default mode may respect system preference when practical
- the homepage and documentation pages shall both support the same theme model

Light and dark mode support is not a cosmetic extra. It is part of the intended public documentation experience.

When logo usage differs by theme mode, the implementation should use the most appropriate existing asset variant rather than applying fragile runtime color inversion.

### 13.5 Docs-page design expectations

The documentation area shall not visually collapse into a generic white-page knowledge base.

Docs pages should maintain:

- clear typography hierarchy
- readable content width
- visible section anchors
- strong code block styling
- useful callouts or notes where needed
- consistent visual connection to the landing page brand

The landing page may be more visually expressive than the docs pages, but both should feel like one site.

---

## 14. Link and Routing Policy

The public site shall follow a strict link policy.

### 14.1 Internal public docs links

Links between public site pages shall use normal site-relative paths and shall resolve inside the generated Pages site.

### 14.2 Source-code links

Links to source files shall use GitHub repository URLs rather than local filesystem paths.

### 14.3 Forbidden link styles

The public site shall not contain links that depend on:

- local Windows paths
- local Unix-style workspace paths
- editor-specific file URLs
- unpublished internal-only pages

### 14.4 External repository links

When content is intentionally excluded from the public Pages site but still valuable, the site may link to the GitHub repository as an external destination.

Typical examples include:

- source-code files
- excluded internal docs categories
- README or changelog pages that still live in the repository

These links should be clearly intentional, not accidental fallbacks caused by missing Pages coverage.

### 14.5 Project-path compatibility

All internal site assets and routes shall work correctly under the GitHub Pages repository subpath:

- `/bITdevKit/`

This includes:

- CSS and image assets
- navigation links
- search behavior
- theme toggle persistence
- landing-page CTAs that point to internal Pages routes

---

## 15. Acceptance Criteria

### 15.1 Site availability

- When the GitHub Actions workflow completes successfully, then `https://bridgingit-gmbh.github.io/bITdevKit/` serves a valid site instead of a 404.

### 15.2 Homepage behavior

- When a visitor opens the site root, then they see a polished landing page rather than a documentation index.
- When a visitor scans the homepage, then they can quickly identify what bITdevKit is, what areas it covers, and where to go next.

### 15.3 Documentation availability

- When a visitor enters the documentation area, then they can browse an extensive set of curated public framework docs.
- When a visitor searches the public docs, then relevant feature and concept pages are discoverable.
- When a visitor is new to the framework, then they can follow a clear path from homepage to getting-started material to deeper feature guides.

### 15.4 Content exclusions

- When the site is built, then `docs/specs`, `docs/adr`, and `docs/presentations` are not included in the public Pages output.
- When the site is built, then `src/**` Markdown is not included in the public Pages output.

### 15.5 Tooling

- When a developer previews the site locally, then they can do so using Docker without installing Python directly on the host machine.
- When GitHub Actions builds the site, then the build runs through Docker.
- When a developer wants to reproduce CI locally, then the same Dockerized MkDocs toolchain can be used with comparable behavior.

### 15.6 Theme behavior

- When a visitor views the site, then both light and dark theme modes are available.
- When a visitor switches theme mode, then the selected mode remains consistent while navigating the site.

### 15.7 Deployment

- When code is pushed to the GitHub default branch, then GitHub Actions can rebuild and republish the site to `gh-pages`.
- When GitHub Pages is configured to serve `gh-pages`, then the published site resolves correctly under the `/bITdevKit/` project path.
- When the workflow publishes the site, then the generated Pages branch contains a valid static-site root with `index.html`.

### 15.8 Link hygiene

- When a visitor navigates the site, then public-site links resolve correctly without local-path failures.
- When a page references source code, then the link opens in the GitHub repository rather than pointing to a local filesystem path.
- When a page links to excluded repository content intentionally, then the link is an explicit GitHub repository link rather than a broken internal site route.

### 15.9 Responsive presentation

- When the site is opened on desktop and mobile widths, then homepage sections, navigation, and core docs remain readable and usable.

### 15.10 Content map fidelity

- When the public docs are implemented, then the current public documentation inventory described in this specification is represented in the site information architecture, unless later design decisions explicitly revise that inventory.

---

## 16. GitHub Actions and Docker Build Specification

The public site build and publish automation shall be defined explicitly rather than left implicit.

### 16.1 Workflow ownership

The GitHub Pages workflow shall live in GitHub, not Azure DevOps.

The workflow is responsible for:

- building the site
- validating that the static output is produced
- publishing the generated site to the `gh-pages` branch

### 16.2 Workflow trigger model

The workflow should support:

- automatic execution on pushes to the GitHub default branch
- optional manual execution through workflow dispatch

The workflow does not need to run on every pull request in the first iteration unless later required for preview validation.

### 16.3 Workflow responsibilities

The workflow shall perform these high-level steps:

1. checkout repository contents
2. prepare the Docker-based MkDocs build environment
3. build the site using the Dockerized MkDocs/Material toolchain
4. verify that the expected static output directory was generated
5. publish the generated output to the `gh-pages` branch

### 16.3.1 Recommended workflow file and permissions

The Pages workflow should live at:

- `.github/workflows/pages.yml`

It should request the minimum permissions needed to publish to the repository Pages branch, typically centered on repository contents write access.

### 16.4 Docker build model

The GitHub Actions workflow shall build the site through Docker rather than through a host-installed Python environment.

The intended model is:

- use the official `squidfunk/mkdocs-material` image
- mount the repository into the container
- run the MkDocs build command inside the container
- emit static output into the configured build directory

This same Docker-first model shall also be the documented local build model so local and CI behavior stay aligned.

### 16.4.1 Local Docker commands

The implementation should document canonical Docker commands for:

- local live preview
- local production build

Those commands should mount the repository or MkDocs source folder into the container and invoke the corresponding MkDocs serve/build command inside the official image.

### 16.5 Publishing model

The workflow shall publish the generated static site to the `gh-pages` branch.

The publishing behavior shall ensure:

- the branch contains only the generated site output relevant for hosting
- the Pages root contains a valid `index.html`
- the published output works under the repository project path `/bITdevKit/`
- the static output is suitable for branch-based GitHub Pages hosting

The publish step should also ensure the output does not accidentally include internal or excluded repository content.

### 16.6 GitHub Pages repository settings

The repository Pages configuration shall be set to:

- source branch: `gh-pages`
- source folder: `/ (root)`

The workflow and repository settings must match each other. A workflow that publishes to `gh-pages` is not sufficient unless GitHub Pages is also configured to serve that branch.

### 16.7 Local Docker workflow parity

The repository should document Docker commands for:

- local preview/serve
- local production build

Those commands should reflect the same image and general build behavior used in GitHub Actions so developers can reproduce the CI build locally with minimal drift.

### 16.8 Workflow failure expectations

The workflow should fail clearly when:

- MkDocs configuration is invalid
- required site source files are missing
- the Docker build does not emit the expected output directory
- publishing to `gh-pages` fails

Clear failure is preferable to silently publishing an incomplete or broken site.
