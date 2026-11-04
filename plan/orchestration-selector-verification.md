# Orchestration selector verification

Instances replaces the free-text Name field with an Orchestration dropdown. It lists registered runtime names, including definitions with no persisted instances, and offers All orchestrations. Selection uses an exact name and keeps the existing `name` query parameter. Deep links to unregistered historical names retain an explicit selected option. Discovery reads names without building definitions or executing activities.

All 30 Orchestrations presentation tests passed. Coverage includes registered/custom names, encoded option text, historical deep links, exact matching against overlapping names and the existing instance continuation behavior. `git diff --check` passed. Full-repository tests were not run.

```powershell
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Orchestration" --nologo /p:UseSharedCompilation=false
```

Live checks on the worktree sample at port 5211 confirmed both registered choices were present. Selecting Alive and Apply showed its instance; selecting the other definition showed an empty result while retaining the selected option. Reset returned to All orchestrations, removed the name criterion and preserved the footer batch size. Desktop and phone screenshots were inspected; the 390px and 320px layouts had no horizontal page overflow. Load more and Rows per load remain below the table.

- [Desktop](../docs/specs/assets/dashboard-page-consistency/after-orchestration-selector-1440.png)
- [Phone](../docs/specs/assets/dashboard-page-consistency/after-orchestration-selector-390.png)

The test log is `.tmp/orchestration-selector-tests.log`. The sample remains running and the verification tab was closed.
