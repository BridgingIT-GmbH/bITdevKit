---
description: Weekly small-batch review that keeps docs/ and the mkdocs site in line with the code
on:
  schedule:
    - cron: "0 5 * * 3"
  workflow_dispatch:

permissions:
  contents: read
  issues: read
  pull-requests: read
  copilot-requests: write

engine: copilot
timeout-minutes: 40

steps:
  - name: Setup .NET
    uses: actions/setup-dotnet@v6
    with:
      global-json-file: global.json
  - name: Install mkdocs
    run: pip install --user mkdocs-material

network:
  allowed:
    - defaults
    - github

tools:
  github:
    toolsets: [default]
  edit:
  bash: [":*"]
  repo-memory:
    branch-name: memory/docs-review
    file-glob: ["*.json", "*.md"]
    description: "Review state of the continuous documentation workflow"

safe-outputs:
  create-pull-request:
    title-prefix: "[docs] "
    labels: [documentation, automation]
    draft: true
    expires: 21d
    max-patch-files: 12
    allowed-files:
      - docs/*.md
      - docs/site/*.md
      - mkdocs.yml
---

# Continuous Documentation

Keep the documentation of bITdevKit correct and in line with the code. Work in **small batches**: every run reviews at most three documents plus the cheap mkdocs consistency check, then stops. Never try to review everything at once.

## How the docs are organised

- `docs/*.md` is the source of truth for the reference pages (`features-*.md`, `common-*.md`, `testing-*.md`, `introduction-ddd-guide.md`, `INDEX.md`).
- `docs/site/scripts/sync-docs.ps1` copies those files into `docs/site/reference/` for mkdocs. `docs/site/reference/` is generated and git-ignored: **never edit it**.
- `docs/site/*.md` holds the hand-written site pages (`docs.md`, `guides.md`, `decisions-*.md`, `getting-started.md`, `packages.md`, `architecture.md`, and others).
- `mkdocs.yml` defines the navigation. Reference pages appear there as `reference/<file>.md`.
- Out of scope, do not touch: `docs/adr`, `docs/specs`, `docs/prd`, `docs/presentations`, `docs/api`, `docs/assets`.

## Process

1. If a pull request whose title starts with `[docs]` is still open, call the `noop` tool and mention it. Do not stack pull requests.
2. Read the memory file `review-state.json` (it may not exist yet). It records, per document, the commit and date of its last review, and the repository commit of the previous run.
3. **Consistency check (every run)**:
   - run `pwsh docs/site/scripts/sync-docs.ps1`, then `python3 -m mkdocs build --strict --site-dir /tmp/gh-aw/agent/mkdocs-site` (if mkdocs is unavailable, compare the nav manually);
   - every file in `docs/` matching the sync patterns has a nav entry in `mkdocs.yml`, and every nav entry points to an existing file;
   - new feature pages are linked from `docs/site/docs.md` and `docs/site/reference/index.md` (source `docs/INDEX.md`) like their siblings;
   - relative links between pages resolve.
4. **Pick up to three documents to review**, in this order of priority:
   1. documents whose mapped source projects changed since the previous run (use `git log` on `src/` since the recorded commit);
   2. documents never reviewed or reviewed longest ago.
5. For each picked document, map it to its source (for example `features-jobs.md` maps to `src/Application.Jobs` and `src/Presentation.Web.Jobs`) and verify **against the code**:
   - every type, method, extension method, option and configuration key named in the text exists with that name and a compatible signature;
   - registration and usage snippets (`Add...`, `With...`, options builders) still match the current API and compile logically;
   - behaviour statements, defaults and limits match the implementation;
   - significant public capabilities that exist in the code but are missing from the page.
6. Fix what is wrong or outdated directly in `docs/*.md` or `docs/site/*.md`. Keep each edit minimal and in the style of the file.
7. Suggestions that need a decision (new sections, restructuring, missing guides) go into the pull request body as a list. Do not write them into the docs.
8. Update `review-state.json` with the reviewed documents and the current commit, whether or not you changed anything.
9. If there were fixes, run the consistency check again, then create one pull request with `create_pull_request`. If everything is already correct, call the `noop` tool and list the documents reviewed.

## Rules

- The code is the truth. Never invent APIs, options or behaviour. If you cannot verify a statement in the code, leave it and list it as a suggestion.
- Follow `.agents/skills/unslop/SKILL.md` for all prose you write: plain, specific, no filler, no marketing tone.
- Do not change the meaning of decision guides (`decisions-*.md`) unless the code contradicts them; list doubts as suggestions instead.
- Do not modify `README.md`, `AGENTS.md`, anything under `.github/` or `.agents/`, or any source code.

## Pull request content

- **Reviewed**: the documents checked in this run.
- **Fixed**: what changed per file, one line each, with the code location that proves it.
- **Navigation**: results of the mkdocs consistency check.
- **Suggestions**: open items for a human to decide.
