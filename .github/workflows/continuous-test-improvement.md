---
description: Twice-weekly small batch of new unit tests aimed at the least covered logic, offered as a draft pull request
on:
  schedule:
    - cron: "0 4 * * 2,5"
  workflow_dispatch:

permissions:
  contents: read
  issues: read
  pull-requests: read
  copilot-requests: write

engine: copilot
timeout-minutes: 60

jobs:
  agent:
    timeout-minutes: 90

steps:
  - name: Setup .NET
    uses: actions/setup-dotnet@v6
    with:
      global-json-file: global.json
  - name: Restore
    run: dotnet restore

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
  repo-memory:
    branch-name: memory/test-improvement
    file-glob: ["*.json", "*.md"]
    description: "Coverage rotation and history of the continuous test improvement workflow"

safe-outputs:
  create-pull-request:
    title-prefix: "[test improvement] "
    labels: [tests, automation]
    draft: true
    expires: 14d
    max-patch-files: 6
    allowed-files:
      - tests/**
  create-issue:
    title-prefix: "[suspected bug] "
    labels: [bug, automation]
    max: 2
    expires: 30d
---

# Continuous Test Improvement

Raise the unit test coverage of bITdevKit in **small steps**. Every run adds tests for one well chosen class (at most two small ones), proves they pass, and stops. Do not try to close all coverage gaps at once. Only tests are changed: never modify production code.

## Process

1. If a pull request whose title starts with `[test improvement]` is still open, call the `noop` tool and mention it. Do not stack pull requests.
2. Read the memory file `state.json` (it may not exist yet). It holds per unit test project the date of the last run and the last measured line coverage, plus a list of classes already improved or deliberately skipped.
3. **Pick one project** among the unit test projects in `tests/` (`Common.UnitTests`, `Domain.UnitTests`, `Application.UnitTests`, `Infrastructure.UnitTests`, `Presentation.UnitTests`): the one not improved for the longest time. Never run integration test projects: Docker is not available.
4. **Measure coverage** for that project only, one command at a time:
   `dotnet test tests/<Project> --collect:"XPlat Code Coverage" --results-directory /tmp/gh-aw/agent/coverage`
   then read the generated `coverage.cobertura.xml` (for example with `python3`) and rank classes by uncovered lines.
5. **Choose the target**: the class with the most uncovered lines that holds real logic (branches, validation, transformations, result handling, state transitions). Skip generated code, DTOs and plain records, CodeGen projects, trivial wrappers, and classes listed as skipped in memory. Prefer a class whose behaviour you can fully understand by reading it and its existing tests.
6. **Write the tests** for the uncovered branches and edge cases:
   - read `.agents/skills/xunit/SKILL.md`, `.agents/skills/dotnet-testing-nsubstitute-mocking/SKILL.md` and `.agents/skills/csharp-coding-standards/SKILL.md` first;
   - follow the conventions in `.github/copilot-instructions.md` and `.editorconfig`: xUnit, Shouldly assertions, NSubstitute mocks, AAA structure, instance under test named `sut`, descriptive test names, independent and repeatable tests;
   - place them next to the existing tests for that class, matching folder and namespace layout and the style of neighbouring files;
   - at most about 12 new test methods in total, no new packages, no sleeps, no dependence on time zone, culture, file system state or execution order.
7. **Verify**, one command at a time:
   - the new tests pass, run them twice to catch flakiness;
   - the whole project passes: `dotnet test tests/<Project>` with zero failures;
   - `dotnet build` of the project has no new warnings;
   - measure coverage again and note the before and after numbers for the target class and the project.
8. **If a new test fails because production code looks wrong**: do not commit the failing test and do not change production code. Instead create one `[suspected bug]` issue with the class, the behaviour you expected, the actual behaviour and a minimal reproduction. Then pick a different target.
9. Update `state.json` (project, date, coverage before and after, class improved or skipped with reason) and keep it small.
10. Create one pull request with `create_pull_request`. If no worthwhile target exists in the chosen project, record that in memory and call the `noop` tool.

## Green gate (mandatory)

A pull request may only be created when the final committed state builds without errors and the complete unit test project passes with zero failures. Never skip, disable or weaken any existing test. A red pull request is not acceptable.

## Pull request content

- **Target**: project, class, and why it was chosen.
- **Added**: the new test methods, one line each.
- **Coverage**: before and after for the class and the project.
- **Verification**: build and test results, both runs of the new tests.
- **Next candidates**: the next two or three classes worth covering.
