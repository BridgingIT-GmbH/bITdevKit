# GitHub Actions Pipelines

This folder contains the repository automation for CI, packaging and documentation publishing.

## Pipeline Overview

| Workflow | File | Trigger | Purpose |
| --- | --- | --- | --- |
| CI | `github-actions.yml` | Every push | Calculates version, builds the solution, runs unit tests, optionally packs and pushes NuGet packages. |
| Pages | `pages.yml` | Pushes to `main` that touch docs/site/API/source inputs, or manual dispatch | Builds and publishes the MkDocs site, DocFX API reference and agent-readable API reference metadata to `gh-pages`. |
| Weekly Repo Status | `weekly-repo-status.md` (compiled to `weekly-repo-status.lock.yml`) | Mondays 06:00 UTC, or manual dispatch | [GitHub Agentic Workflow](https://github.github.com/gh-aw/) that opens a maintainer status issue (PRs, issues, CI health, dependencies, recommendations) and closes the previous report. |
| Weekly Package Update | `weekly-package-update.md` (compiled to `weekly-package-update.lock.yml`) | Mondays 05:00 UTC, or manual dispatch | Agentic workflow that runs the `update-packages` skill (NuGet packages and dotnet tools, build, tests, vulnerability scan) and opens a draft pull request. |
| CI Fault Analysis | `ci-fault-analysis.md` (compiled to `ci-fault-analysis.lock.yml`) | After a failed `CI` run on `main` or `releases/*`, or manual dispatch | Read-only agentic workflow that reads the failed job logs, classifies the failure (build, test, package, flaky, pipeline) and opens a `[ci failure]` issue, or comments on an existing one for the same cause. |
| Continuous Docs | `continuous-docs.md` (compiled to `continuous-docs.lock.yml`) | Wednesdays 05:00 UTC, or manual dispatch | Reviews at most three documents per run against the code, checks `mkdocs.yml` navigation and links, and opens a draft `[docs]` PR for fixes. Open suggestions go into the PR body. Progress is kept in the `memory/docs-review` branch. |
| Continuous Test Improvement | `continuous-test-improvement.md` (compiled to `continuous-test-improvement.lock.yml`) | Tuesdays and Fridays 04:00 UTC, or manual dispatch | Measures coverage of one unit test project per run, adds tests for one under-covered class and opens a draft `[test improvement]` PR. Suspected production bugs become `[suspected bug]` issues. Rotation state is kept in the `memory/test-improvement` branch. |
| Issue Triage | `issue-triage.md` (compiled to `issue-triage.lock.yml`) | New issue | Classifies the issue, adds `bug`/`enhancement`/`documentation`/`question` and `area:*` labels, flags duplicates, asks for missing details (`needs-info`) or marks it `triage:ready` with a summary and size estimate. Runs for any reporter (`roles: all`), so it can only label (allowlist) and comment. |
| Issue Plan | `issue-plan.md` (compiled to `issue-plan.lock.yml`) | Label `ai-plan` on an issue or on a `[plan]` PR | On an issue: writes a plan in `plan/` (template of the `create-implementation-plan` skill) and opens a draft `[plan]` PR with `Refs #N`. On the PR: revises the plan from the review comments. Only `plan/*.md` may change. |
| Issue Implementation | `issue-implement.md` (compiled to `issue-implement.lock.yml`) | Label `ai-implement` on a `[plan]` PR | Sign-off. Implements the plan on the same PR branch and tracks tasks in the plan file. Pushes only when build, unit tests and the green gate pass. Can be re-applied for fix iterations on review feedback or failed CI, at most five per PR. Also started automatically by Issue Review. |
| Issue Review | `issue-review.md` (compiled to `issue-review.lock.yml`) | Started by Issue Implementation after a successful push, or label `ai-review` on a `[plan]` PR | Independent, comment-only review of the PR against its plan: every `TASK-`/`REQ-`/`SEC-` item as done, partial, missing or deviating with evidence, scope creep, architecture (`review-architecture`), code quality (`review-code`), tests, plus its own build and unit test run. Cannot approve or request changes. If it finds blockers or major issues it starts the next implementation iteration itself, at most three times without a human and never twice for the same findings. |

CI and Pages are intentionally separate. CI validates and packages code. Pages owns the documentation site and generated API reference artifacts.

## Agentic Workflows

`weekly-repo-status.md`, `weekly-package-update.md`, `ci-fault-analysis.md`, `continuous-docs.md`, `continuous-test-improvement.md` and the `issue-*.md` files are the source of truth. Never edit the `.lock.yml` files by hand.

- Install the CLI once: `gh extension install github/gh-aw`
- After changing the `.md` file run `gh aw compile` and commit both files.
- The agent runs read-only; the issue is created through the `create-issue` safe output.
- Copilot inference uses the workflow token (`permissions: copilot-requests: write`, centralized Copilot billing). No PAT secret is needed. Without centralized billing, remove that permission and add a `COPILOT_GITHUB_TOKEN` secret (fine-grained PAT with "Copilot Requests"), see [engine setup](https://github.github.com/gh-aw/reference/engines/).
- To preview without writing anything, temporarily add `staged: true` under `safe-outputs`.
- `weekly-package-update` follows `.agents/skills/update-packages/SKILL.md`, so change the process there. It creates a draft PR; pull requests created with the default token do not trigger CI, so close and reopen the PR (or push an empty commit) to run CI before merging.

## Issue Pipeline (loop)

```text
issue opened -> triage -> [human: label ai-plan] -> plan PR (draft)
                                   ^                      |
                                   |  review comments     v
                                   +-- [label ai-plan] <- revise plan
                                                          |
                                      [human: label ai-implement = sign-off]
                                                          v
                               +--> implementation on the same PR
                               |                          |  green gate, then push
                               |                          v
                               |       independent review vs plan (comment only)
                               |                          |
                               |   blockers or major, < 3 iterations, new findings?
                               +-------- yes -------------+--- no ---> human review, CI, merge
                                                                              |
                                                       [human: label ai-implement] again if needed
```

- Two loops: plan and review (`ai-plan` on the PR), and implement and review. The implement and review loop runs by itself for at most three iterations, then a human decides. A human can continue with `ai-implement` up to five iterations in total.
- The automatic loop stops when the review finds nothing blocking, when the same findings come back, when the limit is reached, or when the green gate fails (nothing is pushed, so no review follows).
- Humans own the two decisions that matter: `ai-plan` starts planning and `ai-implement` signs off the plan. Labels applied by workflows never trigger other workflows, so only a person can apply them, and only users with write access can trigger them.
- Create the labels once, see [Getting started](#getting-started).
- Pushes made with the workflow token do not run CI. Set the repository secret `GH_AW_CI_TRIGGER_TOKEN` (fine-grained PAT with `Contents: Read and write`) and the workflows add an empty commit that starts CI, see [Triggering CI](https://github.github.com/gh-aw/reference/triggering-ci/). Without it, close and reopen the PR to run CI.

### Getting started

One time:

1. Create the labels:

   ```powershell
   'ai-plan','ai-implement','ai-review','plan','plan:proposed','plan:implemented','plan:reviewed','needs-info','triage:ready' |
     ForEach-Object { gh label create $_ --force }
   ```

2. Optional but recommended: `gh aw secrets set GH_AW_CI_TRIGGER_TOKEN --value "<pat>"` so CI runs on agent pushes.
3. Make sure the organization allows Copilot CLI to be billed to it (Organization settings, Copilot, Policies).

Per issue:

1. Open an issue. Triage labels it and comments. If it is `needs-info`, answer the questions.
2. If it is `triage:ready` and a good fit for the agent, apply `ai-plan`. A draft `[plan]` PR appears.
3. Read the plan file in the PR. Comment on what is wrong and apply `ai-plan` on the PR to have it revised. Repeat until you agree.da
4. Apply `ai-implement` on the PR to sign off. The implementation is pushed and reviewed automatically.
5. Read the review and CI. Merge when it is good, or apply `ai-implement` again to fix more.

Good to know:

- Start with a small, clear issue to learn the flow and the cost. Check spend with `gh aw logs`.
- To stop the pipeline, remove the label, close the PR, or disable a workflow with `gh workflow disable <name>`.
- Plans are files in `plan/`, so the PR shows exactly what was agreed before any code was written.

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
