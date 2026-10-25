---
description: Weekly status report for bITdevKit maintainers
on:
  schedule:
    - cron: "0 6 * * 1"
  workflow_dispatch:

permissions:
  contents: read
  issues: read
  pull-requests: read
  actions: read
  copilot-requests: write

engine: copilot
timeout-minutes: 20

tools:
  github:
    toolsets: [default, actions]

safe-outputs:
  create-issue:
    title-prefix: "[weekly status] "
    labels: [report, automation]
    close-older-issues: true
    expires: 30d
---

# Weekly Repo Status Report

Create a weekly status report for the maintainers of bITdevKit, a modular .NET (Onion/Clean Architecture, DDD) application development kit.

Cover the last 7 days. Link every item to its issue, pull request, run or commit.

## Include

- **Highlights**: the 3-5 most important changes, releases or decisions of the week.
- **Pull requests**: merged, opened, and stale (open > 14 days without activity). Call out PRs waiting on review.
- **Issues**: opened, closed, and open issues without a response. Group by theme where possible.
- **CI health**: pass/fail trend of the `CI` and `Pages` workflows, flaky or repeatedly failing jobs, and the likely cause when evident.
- **Dependencies**: merged or open Dependabot updates and anything blocked.
- **Code changes**: notable areas of the codebase touched (for example `src/Common`, `src/Application`, `src/Domain`, `src/Infrastructure`, `src/Presentation`, `docs`), without listing every commit.
- **Recommendations**: 3-5 concrete next steps for maintainers, ordered by impact.

## Format

- Start with a 2-3 sentence summary.
- Use short sections with bullet lists and small tables where they help.
- Keep it concise: skip empty sections instead of writing "none".
- Do not speculate. Only report what you can verify from repository data.

If there was no activity in the period, call the `noop` tool with a short explanation instead of creating an issue.
