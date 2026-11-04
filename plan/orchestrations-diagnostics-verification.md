# Orchestrations diagnostics verification

Verified on 2026-11-04 in this worktree.

Orchestrations now uses the shared four-chart layout and bounded runtime metric history:

- Instance starts and finishes per second.
- Completed, failed, cancelled and terminated instance outcomes per second.
- Activity attempts, returned calls and exceptions per second.
- Mean instance and activity duration in milliseconds.

Activity returns include Wait and Retry outcomes. They do not represent completed instances. Instance duration includes waiting time. Instance metrics use the existing optional metrics service; activity metrics use the existing optional `MetricsOrchestrationBehavior`. Existing instruments retain their meaning; additional aggregate terminal-outcome counters and an instance-duration histogram supply the new series.

Collection uses completed one-second intervals and retains up to fifteen minutes in the local process. It continues independently of browser visits and does not derive rates from database counts. The existing retained-history endpoint remains available through a separate collapsed panel. Overview and filterable Instances remain separate tabs.

## Checks

Run sequentially from the repository root:

```powershell
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Orchestrations" --nologo /p:UseSharedCompilation=false
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --nologo /p:UseSharedCompilation=false
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName~MetricsOrchestrationBehaviorTests" --nologo
node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs
git diff --check
```

Results:

- All 6 focused orchestration metric tests passed. They cover activity returns and exceptions, terminal completed/failed/cancelled/terminated outcomes, one finish event and one duration observation per terminal execution.
- All 1,447 Presentation tests passed, including custom-root/metrics-unavailable rendering and separation of instance filters from diagnostics. Existing collector and other workload feature coverage also passed.
- All 9 JavaScript refresher checks passed. The whitespace check passed.
- The broader Orchestrations Application run passed 153 of 154 tests. `OrchestrationQueryServiceTests.SearchAsync_InvalidCriteria_ReturnsValidationFailure` fails for `OrchestrationName` with `GreaterThan`: the result succeeds when the test expects a validation failure. That query service and test were unchanged by this chart work. This run therefore does not establish a green broader Application suite.

Local logs are `.tmp/orchestrations-application-tests.log`, `.tmp/orchestrations-focused-metrics-tests.log`, `.tmp/orchestrations-presentation-tests.log` and `.tmp/orchestrations-js-tests.log`.

## Live browser verification

Used the running WeatherFiesta worktree sample at `https://localhost:5211`. Its existing Start alive orchestration action produced measured peaks of 1 started/finished/completed instance per second, 2 activity attempts/returns per second, approximately 960 ms instance mean duration and 607 ms activity mean duration. These are sample observations, not throughput benchmarks.

The retained-history panel opened and rendered Completed, Failed, Running, Waiting and Problem series. A simulated realtime HTTP 503 preserved all four charts and their activity; restoring the request recovered the Updated status. Navigating to Instances showed its filters with no workload charts; returning to Diagnostics retained the same collection start time.

Checked Tokyo Night, Catppuccin Latte and White themes at 1280, 390 and 320 CSS pixels. All nine combinations had four appropriately sized charts and no horizontal page overflow. The desktop grid had two columns; narrow views had one. Desktop and phone captures were visually inspected:

- [Desktop capture](../docs/specs/assets/dashboard-page-consistency/after-orchestrations-throughput-1280.png)
- [Phone capture](../docs/specs/assets/dashboard-page-consistency/after-orchestrations-throughput-390.png)

The existing sample instance was restarted to load the changes and remains running. No purge or destructive diagnostic action was used. Full-repository tests and distributed metric aggregation are outside this verification.
