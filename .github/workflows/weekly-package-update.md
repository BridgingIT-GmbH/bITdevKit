---
description: Weekly NuGet package and dotnet tool update, offered as a draft pull request
on:
  schedule:
    - cron: "0 5 * * 1"
  workflow_dispatch:

permissions:
  contents: read
  issues: read
  pull-requests: read
  copilot-requests: write

engine: copilot
timeout-minutes: 90

jobs:
  agent:
    timeout-minutes: 120

steps:
  - name: Setup .NET
    uses: actions/setup-dotnet@v6
    with:
      global-json-file: global.json
  - name: Restore tools and packages
    run: |
      dotnet --info
      dotnet tool restore
      dotnet restore

network:
  allowed:
    - defaults
    - dotnet
    - github

tools:
  github:
    toolsets: [default]
  edit:
  bash: [":*"]

safe-outputs:
  create-pull-request:
    title-prefix: "[package update] "
    labels: [dependencies, automation]
    draft: true
    expires: 14d
    protected-files:
      policy: request-review
      exclude:
        - Directory.Packages.props
        - .config/
---

# Weekly Package Update

Update all NuGet packages and dotnet tools of this repository to the latest stable versions and offer the result as a pull request.

## Process

Test environment: the sandbox has no writable `/run/user`, which makes four Presentation unit tests fail with `UnauthorizedAccessException` in `LocalIpcEndpointState`. Run `mkdir -p /tmp/gh-aw/agent/xdg` once and prefix every `dotnet test` with `XDG_RUNTIME_DIR=/tmp/gh-aw/agent/xdg` (the variable does not persist between commands).

1. Check for an existing open pull request whose title starts with `[package update]`. If one exists, do not create another one: call the `noop` tool and mention the existing pull request.
2. Establish a baseline before changing anything: `dotnet build bITdevKit.slnx` and the unit tests (`dotnet test bITdevKit.slnx --no-build --filter "FullyQualifiedName~UnitTests"`). Re-run any failing test alone; a test that fails consistently on the untouched code is a pre-existing failure. If the baseline is not green, call the `noop` tool listing the failing tests. Do not open a pull request on top of a red baseline.
3. Read `.agents/skills/update-packages/SKILL.md` and follow it end to end (preflight, bulk upgrade, restore, build, deprecated check, vulnerability scan, tests, dotnet tools, Kiota regeneration, final check). Its rules take precedence, in particular:
   - latest stable only, bracketed pins and ranges in `Directory.Packages.props` stay untouched,
   - every held back or pinned package gets a one-line comment explaining why,
   - run restore, build and test commands one at a time.
4. If nothing is outdated, call the `noop` tool with a short explanation.
5. Apply the green gate below. Only when it is fully met, create a branch, commit the changes (the repository files changed by the update: `Directory.Packages.props`, `.config/dotnet-tools.json`, `packages.lock.json` files, regenerated Kiota client, and source fixes needed for API breaks) and call `create_pull_request`.

## Green gate (mandatory)

A pull request may only be created when, on the final committed state, all of the following were observed in this run:

- `dotnet restore` has no errors.
- `dotnet build bITdevKit.slnx` has 0 errors and 0 warnings.
- **All unit tests pass**: zero failed tests. Run the full unit test suite again after the last change, not only the tests you touched.
- Integration tests that do not need Docker and cover areas touched by a bump or code fix pass.
- The vulnerability scan (`dotnet list bITdevKit.slnx package --vulnerable --include-transitive`) is empty.

If a test fails:

1. Re-run it alone. If it passes alone it is flaky: it may stay, but name it in the pull request.
2. Otherwise find the package that caused it (bisect by holding back packages or families one at a time), fix the source if the cause is an API or behavior change that has a legitimate fix, or hold the package back with a comment.
3. Repeat until the whole suite is green.

Never make tests pass by deleting, skipping, disabling or weakening them (no `[Fact(Skip=...)]`, no loosened assertions, no excluding projects from the run, no `--filter` that hides the failure). Never lower the vulnerability or warning bar. If the suite cannot be made green, call the `noop` tool with the failing tests, the packages involved and what you tried. A red pull request is not an acceptable outcome.

## Environment notes

- The .NET SDK from `global.json` is installed and `dotnet tool restore` / `dotnet restore` have already run.
- Network access is limited to NuGet, GitHub and basic infrastructure. Docker is not available, so integration tests that need Testcontainers cannot run: list them as skipped in the pull request.
- Do not modify anything under `.github/` or `.agents/`.

## Pull request content

Keep the body short and factual:

- **Updated**: packages and tools that moved (old -> new), grouped by family where sensible.
- **Held back**: package, version it stays on, and the reason (same text as the comment in `Directory.Packages.props`).
- **Source changes**: API breaks that required code changes, with file links.
- **Verification**: restore, build (errors and warnings), unit test totals (passed/failed/skipped, must be 0 failed), integration tests run, vulnerability scan result, flaky or skipped tests.
- **Generated files**: which ones changed (for example the Kiota client).

The pull request description must state that the green gate was met. If it was not met, there is no pull request.
