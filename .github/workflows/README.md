# GitHub Actions Pipelines

This folder contains the repository automation for CI, packaging and documentation publishing.

## Pipeline Overview

| Workflow | File | Trigger | Purpose |
| --- | --- | --- | --- |
| CI | `github-actions.yml` | Every push | Calculates version, builds the solution, runs unit tests, optionally packs and pushes NuGet packages. |
| Pages | `pages.yml` | Pushes to `main` that touch docs/site/API/source inputs, or manual dispatch | Builds and publishes the MkDocs site, DocFX API reference and agent-readable API reference metadata to `gh-pages`. |

The two workflows are intentionally separate. CI validates and packages code. Pages owns the documentation site and generated API reference artifacts.

## CI Workflow

`github-actions.yml` runs on every push.

### Environment

- `Build_Configuration=Debug`
- Release is not used because assembly signing currently depends on a missing `.snk` file.
- `MinVerDefaultPreReleaseIdentifiers=preview.0`
- `NUGET_PUSH` defaults to `false` unless the repository variable enables it.
- `PUBLISHABLE_BRANCHES=main,releases/dotnet9`

### Jobs

1. `version`
   - Checks out the full repository history.
   - Sets up .NET from `global.json`.
   - Installs `minver-cli` version `4.3.0`.
   - Calculates the package/build version and exposes it as the `version` job output.
   - Writes the calculated version to the GitHub step summary.

2. `build`
   - Depends on `version`.
   - Restores NuGet packages.
   - Builds the full solution with `dotnet build --configuration Debug --no-restore`.
   - Uploads `src/**/bin`, `src/**/obj`, `tests/**/bin` and `tests/**/obj` as the short-lived `BuildOutput` artifact.

3. `tests`
   - Depends on `version` and `build`.
   - Downloads `BuildOutput`.
   - Discovers `tests/**/*UnitTests.csproj`.
   - Runs each unit test project with `--no-restore --no-build`.
   - Retries each unit test project up to four attempts.
   - Publishes `.trx` results through `dorny/test-reporter`.

4. `publish`
   - Depends on `version` and `tests`.
   - Downloads `BuildOutput`.
   - Evaluates whether the current ref is publishable:
     - tags are publishable
     - branches listed in `PUBLISHABLE_BRANCHES` are publishable
   - Packs non-example, non-test projects when the ref is publishable.
   - Pushes packages only when `NUGET_PUSH=true` and `NUGET_API_KEY` is configured.
   - Uploads produced `.nupkg` files as the `NuGet packages` artifact.

### Package Rules

Package packing excludes:

- `./examples/*`
- `*Tests.csproj`

Package pushing is gated by both:

- publishable ref
- `NUGET_PUSH=true`

## Pages Workflow

`pages.yml` publishes the public documentation site to the `gh-pages` branch.

### Triggers

The workflow runs on:

- manual `workflow_dispatch`
- pushes to `main` that touch:
  - `mkdocs.yml`
  - `global.json`
  - `Directory.Build.props`
  - `Directory.Packages.props`
  - `.config/dotnet-tools.json`
  - `docs/site/**`
  - `docs/api/**`
  - selected top-level docs under `docs/*.md`
  - `src/**`
  - `.github/workflows/pages.yml`

The `src/**` path is included because DocFX API metadata is generated from source assemblies.

### Job

`publish` runs only for the canonical repository:

```text
github.repository == 'BridgingIT-GmbH/bITdevKit'
```

Steps:

1. Checks out the full repository history.
2. Sets up .NET from `global.json`.
3. Caches NuGet packages.
4. Runs `./docs/site/scripts/build-pages.ps1` using PowerShell.
5. Verifies that `.github/pages/index.html` and `.github/pages/api/index.html` exist.
6. Publishes `.github/pages` to the `gh-pages` branch.
7. Writes `.nojekyll` so GitHub Pages serves DocFX and MkDocs assets unchanged.

## Pages Build Script

`docs/site/scripts/build-pages.ps1` is the source of truth for the documentation build.

The script:

1. Synchronizes docs into the MkDocs site tree.
2. Runs MkDocs Material in Docker.
3. Restores .NET local tools.
4. Restores and builds `docs/api/ApiReference.proj` in Release mode.
5. Stages built source assemblies and XML documentation into `docs/api/obj/assemblies`.
6. Clears stale DocFX metadata under `docs/api/obj/api`.
7. Runs DocFX with `docs/api/docfx.json`.
8. Runs `docs/api/scripts/build-agent-index.ps1`.

## Generated Documentation Artifacts

The Pages workflow publishes generated output from `.github/pages`.

Important generated paths:

| Path | Producer | Purpose |
| --- | --- | --- |
| `.github/pages/index.html` | MkDocs | Public documentation landing site. |
| `.github/pages/api/index.html` | DocFX | Human-readable API reference. |
| `.github/pages/api/agent-index.json` | `build-agent-index.ps1` | Searchable API symbol index for MCP agents. |
| `.github/pages/api/agent-symbols/*.json` | `build-agent-index.ps1` | Page-level API symbol detail payloads for MCP agents. |

Generated Pages artifacts are not committed. They are published to `gh-pages`.

## Agent API Reference Metadata

The agent API reference metadata is generated from DocFX mref YAML in `docs/api/obj/api/**/*.yml`.

Rules:

- Include DevKit symbols with `BridgingIT.DevKit.*` UIDs.
- Exclude example projects because the Pages build stages only `src/**` assemblies.
- Ignore DocFX `references:` entries so synthetic external and extension-method projection references do not bloat the search index.
- Keep real methods, properties, constructors, fields, events, operators, types and namespaces.
- Write one detail JSON file per DocFX mref page.

The MCP server reads:

```text
https://bridgingit-gmbh.github.io/bITdevKit/api/agent-index.json
```

In local development it prefers:

```text
.github/pages/api/agent-index.json
```

## Common Maintenance Tasks

When adding or changing source projects:

- Check whether the project should be included in API reference docs.
- Keep examples under `examples/**` so package packing and API docs continue to exclude them.
- Ensure public XML docs are generated if the API should be useful in DocFX and MCP.

When changing docs or API reference generation:

- Update `pages.yml` path filters if new source paths should trigger publishing.
- Run `pwsh -File ./docs/site/scripts/build-pages.ps1` for an end-to-end local docs build.
- Confirm `agent-index.json` and `agent-symbols/*.json` are generated.
- Confirm a known symbol such as `BridgingIT.DevKit.Common.Result` is present.

When changing CI/package behavior:

- Keep publish gates explicit.
- Do not push NuGet packages unless `NUGET_PUSH=true`.
- Keep package exclusions aligned with repository structure.

## Troubleshooting

### Pages publish fails with a permission error under `.github/pages/api/obj`

DocFX must not write intermediate `obj` output under `.github/pages`. Keep DocFX metadata/intermediate output under `docs/api/obj`, then copy or publish final output under `.github/pages/api`.

### API metadata is missing members

Check the generated `docs/api/obj/api/**/*.yml` files first. The agent index generator can only expose members that DocFX emitted.

Then verify:

```powershell
pwsh -File ./docs/api/scripts/build-agent-index.ps1
```

and inspect `.github/pages/api/agent-index.json`.

### CI tests fail with transient `obj/ref` metadata errors

Avoid running multiple top-level `dotnet build` or `dotnet test` commands in parallel in the same worktree. Rebuild sequentially before rerunning tests.
