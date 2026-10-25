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

1. Check for an existing open pull request whose title starts with `[package update]`. If one exists, do not create another one: call the `noop` tool and mention the existing pull request.
2. Read `.agents/skills/update-packages/SKILL.md` and follow it end to end (preflight, bulk upgrade, restore, build, deprecated check, vulnerability scan, tests, dotnet tools, Kiota regeneration, final check). Its rules take precedence, in particular:
   - latest stable only, bracketed pins and ranges in `Directory.Packages.props` stay untouched,
   - every held back or pinned package gets a one-line comment explaining why,
   - run restore, build and test commands one at a time.
3. If nothing is outdated, call the `noop` tool with a short explanation.
4. Otherwise create a branch, commit the changes (the repository files changed by the update: `Directory.Packages.props`, `.config/dotnet-tools.json`, `packages.lock.json` files, regenerated Kiota client, and source fixes needed for API breaks) and call `create_pull_request`.

## Environment notes

- The .NET SDK from `global.json` is installed and `dotnet tool restore` / `dotnet restore` have already run.
- Network access is limited to NuGet, GitHub and basic infrastructure. Docker is not available, so integration tests that need Testcontainers cannot run: list them as skipped in the pull request.
- Do not modify anything under `.github/` or `.agents/`.

## Pull request content

Keep the body short and factual:

- **Updated**: packages and tools that moved (old -> new), grouped by family where sensible.
- **Held back**: package, version it stays on, and the reason (same text as the comment in `Directory.Packages.props`).
- **Source changes**: API breaks that required code changes, with file links.
- **Verification**: restore, build (errors and warnings), unit test result, vulnerability scan result, flaky or skipped tests.
- **Generated files**: which ones changed (for example the Kiota client).

If the build or unit tests still fail after reasonable fixes, hold back the offending packages until they pass. If that is not possible, call the `noop` tool and describe the blocker instead of creating a pull request with a broken build.
