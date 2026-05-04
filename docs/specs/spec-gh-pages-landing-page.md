---
status: implemented
---

# Specification: GitHub Pages Site for bITdevKit

> This document defines the GitHub Pages experience, information architecture, content model, build model, and publishing behavior for bITdevKit.

[TOC]

## 1. Introduction

bITdevKit shall have a public website at `https://bridgingit-gmbh.github.io/bITdevKit/` that serves two purposes at the same time:

- act as the public front door for the project
- provide a rich, searchable technical documentation experience

The site is not meant to be a raw rendering of the repository and it is not meant to be a documentation-only shell. It shall present bITdevKit like a serious .NET framework/toolkit while still remaining lightweight, maintainable, and Markdown-first.

The resulting site shall combine:

- a product-style landing page
- curated feature and architecture documentation
- strong navigation into relevant repository resources
- a design that feels intentional and modern rather than like a stock docs theme

## 2. Goals

The GitHub Pages site shall:

- present bITdevKit as a modular .NET development kit/framework
- help first-time visitors quickly understand what the project is, what problems it solves, and why it is useful
- provide a polished landing page with strong calls to action
- provide extensive technical documentation for the public feature set
- keep documentation discoverable through clear navigation and search
- remain maintainable by using Markdown as the primary content authoring format
- build locally through Docker without requiring Python to be installed directly on the developer machine
- build and publish automatically through GitHub Actions after code is pushed to the GitHub remote

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

## 4. Audience

The site primarily serves:

- .NET developers evaluating or working with bITdevKit
- architects and technical leads assessing architectural fit
- contributors and adopters looking for feature guidance
- developers searching for concrete entry points into the framework

The tone shall therefore be:

- technical
- clear
- practical
- confident
- concise

The tone shall not be:

- sales-heavy
- generic
- buzzword-driven
- enterprise brochure-like

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

Implemented structure:

```text
/mkdocs.yml
/docs/site/
  index.md
  getting-started.md
  templates.md
  why.md
  architecture.md
  packages.md
  examples.md
  decisions.md
  decisions-messaging-vs-queueing.md
  decisions-repository-vs-activeentity.md
  /assets/
    /images/
    /stylesheets/
  /scripts/
    build-pages.ps1
    serve-pages.ps1
    sync-docs.ps1
  /reference/
    ...generated synced public docs...
```

`docs/site/` is the MkDocs input directory.

Existing reusable branding assets from `docs/assets/` shall be copied, referenced, or curated into the public site asset structure as needed.

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

## 7. Build and Deployment Model

### 7.1 Local build and preview

Local build and preview shall be Docker-based only.

The canonical build tool shall be the official `squidfunk/mkdocs-material` container image.

The local workflow shall support:

- local preview/serve
- local production build

without requiring a Python installation on the host machine.

Implemented wrapper scripts:

- [build-pages.ps1](f:\projects\bit\bITdevKit\docs\site\scripts\build-pages.ps1)
- [serve-pages.ps1](f:\projects\bit\bITdevKit\docs\site\scripts\serve-pages.ps1)
- [sync-docs.ps1](f:\projects\bit\bITdevKit\docs\site\scripts\sync-docs.ps1)

Canonical local commands:

```powershell
pwsh -File ./docs/site/scripts/serve-pages.ps1
pwsh -File ./docs/site/scripts/build-pages.ps1
pwsh -File ./docs/site/scripts/sync-docs.ps1
```

### 7.2 Azure DevOps

Azure DevOps shall not be used for GitHub Pages build or deployment.

The main project may continue to use Azure DevOps for other CI/CD concerns, but the public GitHub Pages site shall be built and published only from GitHub-side automation.

### 7.3 GitHub Actions

GitHub Actions shall be the only automation responsible for Pages build and deployment.

The workflow shall:

- trigger on pushes to the GitHub default branch
- support manual dispatch
- build the site inside Docker through the PowerShell wrapper scripts
- publish the generated static output to the `gh-pages` branch

### 7.4 GitHub Pages publishing mode

GitHub Pages shall use branch-based publishing from:

- branch: `gh-pages`
- folder: `/ (root)`

The GitHub Actions workflow shall be responsible for keeping `gh-pages` up to date with the generated static output.

### 7.5 Output directory

The generated static site shall be written to:

- `.github/pages/`

This output directory shall be the source copied to the root of the `gh-pages` branch.

## 8. Information Architecture

The site shall have two major layers:

1. a public landing and overview experience
2. a deep technical docs experience

### 8.1 Top-level navigation

The top-level navigation shall be visitor-oriented and product-oriented.

Implemented top-level navigation:

- Home
- Getting Started
- Why bITdevKit
- Architecture
- Packages
- Examples
- Decisions
- Documentation

The exact labels shall remain aligned with the repository’s public site pages and user journeys.

### 8.2 Docs navigation

Once a user enters the documentation area, the site shall provide a documentation-style navigation model with:

- page hierarchy grouped by topic
- search across public documentation pages
- a curated grouping model instead of a flat file list

The docs experience shall feel like a real framework documentation site, not just a set of disconnected pages.

### 8.3 Canonical public routes

The public site shall expose stable, human-readable routes for the major public areas.

Implemented canonical route families:

- `/` for the landing page
- `/getting-started/` for onboarding
- `/templates/` for scaffolding guidance
- `/why/` for positioning and fit
- `/architecture/` for architecture overview and request flow
- `/packages/` for the package map
- `/examples/` for the example story
- `/decisions/` for decision guides
- `/reference/` for the broader curated docs overview

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

## 9. Homepage Information Architecture

The homepage shall act as the main public entry point for bITdevKit.

### 9.1 Exact homepage section order

The homepage shall be implemented in this order:

1. Hero
2. Primary CTA row
3. Trust and quick-signal strip
4. Choose a start path
5. When bITdevKit fits best
6. Capability overview
7. Why not just plain ASP.NET Core + MediatR + EF Core?
8. Architecture overview
9. Request flow in practice
10. Example applications
11. Templates for new solutions and modules
12. Common early decisions
13. Closing call to action

The page shall read as one continuous product story:

- what the kit is
- where it fits
- what it contains
- how it is structured
- which paths and decisions matter first
- where to go next

### 9.2 Top navigation

The homepage shall use the site-wide top navigation defined in [mkdocs.yml](f:\projects\bit\bITdevKit\mkdocs.yml).

It shall include the light/dark theme toggle through Material for MkDocs theme chrome.

### 9.3 Hero

The hero shall be the strongest piece of homepage messaging and should immediately answer what bITdevKit is.

The hero shall contain:

- an eyebrow line
- a primary marketing/developer message
- a supporting paragraph
- a primary CTA row

The primary CTA row shall use these targets:

- `Get Started` -> `/getting-started/`
- `Use Templates` -> `/templates/`
- `Explore Docs` -> `/reference/`
- `View Source` -> GitHub repository root

### 9.4 Trust and quick-signal strip

Immediately below the hero, the homepage shall provide a compact strip of factual project signals.

The implemented signal strip shall highlight:

- DDD
- CQRS
- Modular Monolith
- Results
- Messaging
- Queueing
- Templates

### 9.5 Choose a start path

This section shall provide a small set of high-value entry paths for different developer intents.

The implemented cards shall route to:

- `Learn the devkit`
- `Scaffold a solution`
- `Explore examples`
- `Read the docs`

### 9.6 When bITdevKit fits best

This section shall explain when the framework is a strong fit.

The implemented value themes shall include:

- modular monoliths
- business-heavy applications
- operationally realistic platforms
- teams that need consistency

### 9.7 Capability overview

This section shall be the main homepage bridge into the public docs.

It shall use a grid of cards with:

- capability name
- one short explanatory sentence
- one docs link

The homepage shall present these capability cards:

- `Domain`
- `Application`
- `Requester & Notifier`
- `Messaging`
- `Queueing`
- `Pipelines`
- `Storage`
- `Scheduling`
- `Presentation`

### 9.8 Plain-stack comparison

The homepage shall include a short comparison explaining why the devkit exists beyond a plain stack of ASP.NET Core, MediatR, and EF Core.

This section shall remain concise and link to the broader `Why` page.

### 9.9 Architecture overview

This section shall explain the framework shape in a visually digestible way.

It shall communicate:

- clean architecture
- modular vertical slices
- domain/application/infrastructure/presentation boundaries

and route to `/architecture/`.

### 9.10 Request flow in practice

The homepage shall include a compact code example that shows request flow in a familiar developer-facing way.

This section shall route to `/architecture/` for the fuller architecture and flow explanation.

### 9.11 Example applications

This section shall show that the framework is grounded in real usage and examples.

The public homepage example story shall highlight:

- `GettingStarted`
- `DoFiesta`
- `EventSourcingDemo`

and route to `/examples/` for the fuller example inventory.

### 9.12 Templates section

This section shall introduce the template story and route to `/templates/`.

It shall emphasize:

- scaffolding a full solution
- adding modules
- starting from the kit’s structural conventions instead of assembling them manually

### 9.13 Common early decisions

This section shall provide decision-entry cards for:

- `Messaging vs Queueing`
- `Repository vs ActiveEntity`
- `Package map`
- `Why bITdevKit`

### 9.14 Closing call to action

The homepage shall end with a strong, simple next-step section.

The closing CTA hierarchy shall be:

- `Start Here`
- `Why bITdevKit`
- `See Architecture`
- `Explore Templates`
- `Browse GitHub`

### 9.15 Homepage content rules

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

## 10. Public Documentation Scope

The public documentation area shall be extensive, but curated.

### 10.1 Included public doc categories

The public site documentation shall include curated content derived from or based on:

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

The public docs shall be organized into clear reader journeys rather than file-system-shaped buckets.

Implemented public docs groupings:

- Getting Started
- Why bITdevKit
- Architecture
- Packages
- Examples
- Decisions
- Documentation

### 10.3.1 Public docs map

The public documentation structure shall be derived from the current repository documentation set as follows.

**Getting Started**

- site-authored getting-started overview page
- curated orientation derived from `README.md`
- curated orientation derived from `docs/INDEX.md`
- use of `docs/introduction-ddd-guide.md` as an early onboarding concept page

**Why / Architecture / Packages / Examples / Decisions**

- site-authored overview pages for evaluation and onboarding
- site-authored decision pages for recurring technical tradeoffs

**Documentation**

The public technical docs shall expose:

**Common Infrastructure**

- `docs/common-extensions.md`
- `docs/common-utilities.md`
- `docs/common-serialization.md`
- `docs/common-options-builders.md`
- `docs/common-mapping.md`
- `docs/common-caching.md`
- `docs/common-observability-tracing.md`

**Core Domain and Application**

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

**Execution, Messaging and Modularity**

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

**Storage, Scheduling and Utilities**

- `docs/features-startuptasks.md`
- `docs/features-jobscheduling.md`
- `docs/features-storage-documents.md`
- `docs/features-storage-files.md`
- `docs/features-storage-monitoring.md`
- `docs/features-log-entries.md`

**Testing and Test Utilities**

- `docs/testing-fake-authentication.md`
- `docs/testing-common-xunit.md`

### 10.3.2 Documentation entry hierarchy

The public docs shall expose a layered reading hierarchy:

1. homepage overview
2. getting-started, why, architecture, examples, and decisions entry points
3. grouped feature/category indexes
4. individual deep-dive pages

Users shall not need to start from an alphabetical list of files.

### 10.3.3 Cross-linking expectations

Documentation pages shall cross-link intentionally between:

- conceptual prerequisites
- adjacent features
- testing guidance
- source code references

Cross-linking should support learning flow, not just file discoverability.

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

The public site shall optimize for reader experience, not for one-to-one file preservation.

### 11.4 Synchronization policy

When a public site page is derived from an existing repository doc, the implementation shall define a clear ownership rule so future maintenance is predictable.

The rule is:

- site-authored public pages live in `docs/site/`
- synchronized repository docs are generated into `docs/site/reference/`
- the repository source of truth for synchronized docs remains under `docs/`
- internal docs remain outside the public site

## 12. README Relationship

The root `README.md` remains the GitHub repository front page and shall stay aligned with the public site.

The README shall be treated as both a source input and a maintained public artifact.

The README shall:

- use current terminology and links
- match the site’s positioning
- point prominently to the GitHub Pages site
- act as a concise GitHub-facing summary rather than trying to replace the full website
- feed curated homepage and getting-started material without requiring large corrective rewrites every time

The README and the website shall complement each other:

- the README is the repo front door
- the Pages site is the polished public experience

## 13. Visual Design Direction

The site shall feel like a framework/product website with strong documentation capabilities, not like an unstyled documentation export.

### 13.1 Design goals

The design shall be:

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

The design shall not copy another site directly. It shall reflect bITdevKit’s own identity.

### 13.3 Implementation direction

The implementation shall use:

- Material for MkDocs as the structural base
- custom CSS and theme overrides for brand expression
- the existing bITdevKit logo/identity assets where appropriate

### 13.3.1 Existing brand asset inventory

The repository contains reusable visual assets in `docs/assets/`, including:

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

These assets shall be treated as the preferred source for public-site branding before creating new derived branding assets.

### 13.3.2 Intended asset usage

The public site shall reuse the existing SVG assets intentionally:

- a compact icon/logo for header branding
- dark/light-appropriate variants where needed
- favicon usage
- horizontal or icon-based variants where layout requires it

### 13.4 Theme modes

The public site shall support both light and dark presentation modes.

The theme behavior shall satisfy the following expectations:

- the site shall render well in both light and dark modes
- users shall be able to switch between light and dark theme modes
- the selected mode should persist across page navigation
- the default mode may respect system preference when practical
- the homepage and documentation pages shall both support the same theme model

### 13.5 Docs-page design expectations

The documentation area shall not visually collapse into a generic white-page knowledge base.

Docs pages shall maintain:

- clear typography hierarchy
- readable content width
- visible section anchors
- strong code block styling
- useful callouts or notes where needed
- consistent visual connection to the landing page brand

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

## 15. Acceptance Criteria

### 15.1 Site availability

- When the GitHub Actions workflow completes successfully, then `https://bridgingit-gmbh.github.io/bITdevKit/` serves a valid site.

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
- When GitHub Actions builds the site, then the build runs through Docker via the wrapper scripts.
- When a developer wants to reproduce CI locally, then the same Dockerized MkDocs toolchain can be used with comparable behavior.

### 15.6 Theme behavior

- When a visitor views the site, then both light and dark theme modes are available.
- When a visitor switches theme mode, then the selected mode remains consistent while navigating the site.

### 15.7 Deployment

- When code is pushed to `main`, then GitHub Actions can rebuild and republish the site to `gh-pages`.
- When GitHub Pages is configured to serve `gh-pages`, then the published site resolves correctly under the `/bITdevKit/` project path.
- When the workflow publishes the site, then the generated Pages branch contains a valid static-site root with `index.html`.

### 15.8 Link hygiene

- When a visitor navigates the site, then public-site links resolve correctly without local-path failures.
- When a page references source code, then the link opens in the GitHub repository rather than pointing to a local filesystem path.
- When a page links to excluded repository content intentionally, then the link is an explicit GitHub repository link rather than a broken internal site route.

### 15.9 Responsive presentation

- When the site is opened on desktop and mobile widths, then homepage sections, navigation, and core docs remain readable and usable.

### 15.10 Content map fidelity

- When the public docs are built, then the public documentation inventory described in this specification is represented in the site information architecture unless later design decisions explicitly revise that inventory.

## 16. GitHub Actions and Docker Build Specification

The public site build and publish automation shall be defined explicitly rather than left implicit.

### 16.1 Workflow ownership

The GitHub Pages workflow shall live in GitHub, not Azure DevOps.

The workflow is responsible for:

- building the site
- validating that the static output is produced
- publishing the generated site to the `gh-pages` branch

### 16.2 Workflow trigger model

The workflow shall support:

- automatic execution on pushes to the default branch
- manual execution through workflow dispatch

### 16.3 Workflow responsibilities

The workflow shall perform these high-level steps:

1. checkout repository contents
2. invoke the Docker-based MkDocs build through the PowerShell wrapper script
3. verify that the expected static output directory was generated
4. publish the generated output to the `gh-pages` branch

### 16.3.1 Workflow file and permissions

The Pages workflow shall live at:

- `.github/workflows/pages.yml`

It shall request the repository permissions needed to publish to the Pages branch, centered on repository contents write access.

### 16.4 Docker build model

The GitHub Actions workflow shall build the site through Docker rather than through a host-installed Python environment.

The model is:

- use the official `squidfunk/mkdocs-material` image
- mount the repository into the container through the wrapper scripts
- run the MkDocs build command inside the container
- emit static output into `.github/pages/`

This same Docker-first model shall also be the documented local build model so local and CI behavior stay aligned.

### 16.4.1 Local Docker commands

The implementation shall document canonical commands for:

- local live preview
- local production build
- docs synchronization

Those commands shall be the wrapper scripts under `docs/site/scripts/`.

### 16.5 Publishing model

The workflow shall publish the generated static site to the `gh-pages` branch.

The publishing behavior shall ensure:

- the branch contains only the generated site output relevant for hosting
- the Pages root contains a valid `index.html`
- the published output works under the repository project path `/bITdevKit/`
- the static output is suitable for branch-based GitHub Pages hosting

### 16.6 GitHub Pages repository settings

The repository Pages configuration shall be set to:

- source branch: `gh-pages`
- source folder: `/ (root)`

The workflow and repository settings must match each other.

### 16.7 Local Docker workflow parity

The repository shall document Docker-based commands for:

- local preview/serve
- local production build
- docs sync

These commands shall reflect the same image and general build behavior used in GitHub Actions so developers can reproduce the CI build locally with minimal drift.

### 16.8 Workflow failure expectations

The workflow should fail clearly when:

- MkDocs configuration is invalid
- required site source files are missing
- the Docker build does not emit the expected output directory
- publishing to `gh-pages` fails

Clear failure is preferable to silently publishing an incomplete or broken site.
