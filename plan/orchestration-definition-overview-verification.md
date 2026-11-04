# Orchestration definition overview verification

The Overview catalog now shows each registered definition's initial state and state/activity counts. Expand a definition to inspect the state-flow diagram beside its states, ordered activities, declared transitions, signal waits, timers, retry markers and compensation names. The first definition opens initially; refresh preserves expanded sections. View instances opens the existing filtered investigation tab.

Metadata reads the code-first definition without executing activities or transition conditions. A metadata failure remains local to its definition card. Diagrams reuse `IOrchestrationDiagramService` through the dashboard group and its authorization; optional management endpoints are not required. SVG is rendered as an image. Missing diagrams retain readable state details. Dark themes apply a scoped image treatment; phone headers place counts below the name.

## Automated checks

```powershell
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Orchestration" --nologo /p:UseSharedCompilation=false
git diff --check
```

All 27 Orchestrations presentation tests passed. The seven dashboard tests cover the complete registration catalog, separation of Overview/Instances/Diagnostics, encoded definition/activity labels, metadata failures, activities and conditions that would throw if executed, custom-root SVG routing, use of the existing diagram service, and an unavailable service. The whitespace check passed. Full-repository tests were not run for this feature change.

Logs are `.tmp/definition-dashboard-tests.log` and `.tmp/definition-orchestration-tests.log`.

## Live browser checks

Verified the running WeatherFiesta worktree sample at `https://localhost:5211`. Both registered definitions rendered real SVG diagrams: the Alive definition has two states and one activity; the larger definition has six states and six activities. Expanding the second definition, refreshing it, and following View instances preserved the expansion and populated the existing name filter correctly. A deliberately missing diagram showed the fallback while keeping the state details available.

Tokyo Night, Catppuccin Latte and White were checked at 1440, 390 and 320 CSS pixels. All nine layouts fit without horizontal page overflow. Phone checks also require sufficient width for definition names. Desktop and phone captures were visually inspected. Preview timed out twice during screenshot capture; the repository Playwright helper completed the visual checks.

- [Desktop capture](../docs/specs/assets/dashboard-page-consistency/after-orchestration-definitions-1440.png)
- [Phone capture](../docs/specs/assets/dashboard-page-consistency/after-orchestration-definitions-390.png)

The browser script and raw results are `.tmp/playwright-test-definition-overview.js` and `.tmp/definition-browser/results.json`. The existing sample was restarted for the change and remains running. Verification browser tabs were closed. The public documentation illustration was flagged as predating this layout.
