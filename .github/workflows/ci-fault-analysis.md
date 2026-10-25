---
description: Explains failed CI runs on main and release branches with a root cause analysis
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    conclusion: failure
    branches:
      - main
      - "releases/*"
  workflow_dispatch:

permissions:
  contents: read
  actions: read
  issues: read
  pull-requests: read
  copilot-requests: write

engine: copilot
timeout-minutes: 20

network:
  allowed:
    - defaults
    - github

tools:
  github:
    toolsets: [default, actions]

safe-outputs:
  create-issue:
    title-prefix: "[ci failure] "
    labels: [ci-failure, automation]
    expires: 14d
  add-comment:
    target: "*"
    max: 1
    required-title-prefix: "[ci failure] "
---

# CI Fault Analysis

The `CI` workflow of bITdevKit (a modular .NET solution: version, build, unit tests, optional pack and NuGet push) failed. Find out why and report it so a maintainer can act quickly. This workflow is read-only: do not change code, do not open pull requests.

## Failed run

- Run: ${{ github.event.workflow_run.html_url }} (id `${{ github.event.workflow_run.id }}`)
- Commit: `${{ github.event.workflow_run.head_sha }}`
- Read the branch and other run details from the run itself via the GitHub tools.

When started manually, there is no failed run in the event: use the most recent failed `CI` run on `main` instead.

## Process

1. List the jobs of the run and read the logs of the failed jobs only. Identify the first real error, not the cascade after it.
2. Look at the commit and, if it belongs to a pull request, the changed files, to see what plausibly caused the failure.
3. Compare with the previous `CI` runs on the same branch: is this the first failure, a repeated failure, or an intermittent one? If the same commit has a later successful attempt, treat it as flaky.
4. Classify the failure as one of: **build error** (compile, restore, analyzers), **test failure** (name the failing tests and projects), **package or vulnerability issue** (NU errors, NuGet outage, advisory), **infrastructure or flaky** (runner, network, timeout, Testcontainers), or **pipeline configuration**.
5. Check for an open issue whose title starts with `[ci failure]` describing the same root cause. If one exists, add a short comment with the new run, branch and commit using `add_comment` instead of creating a duplicate issue.
6. Otherwise create one issue with `create_issue`.

## Issue content

Keep it short and factual, with links to the run, job, commit and files:

- **Summary**: one or two sentences with the classification and the most likely cause.
- **Evidence**: the key error lines from the log (a few lines, in a code block) and the failing job and step.
- **Suspect change**: the commit or pull request and files that most likely caused it, or "unclear" when it cannot be determined.
- **History**: first failure, repeated, or flaky, with links to the runs compared.
- **Suggested next steps**: 1-3 concrete actions. A small code or config fix may be shown as a suggested diff in the issue body, but never applied.

Do not speculate beyond what the logs and commits show. If the logs are unavailable or the failure cannot be explained, say so and describe what you checked. If there is nothing to report (for example the run was cancelled or already succeeded on retry), call the `noop` tool with a short explanation.
