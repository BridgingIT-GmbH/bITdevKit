# Workload diagnostics verification

Verified on 2026-11-04 against the implementation in this worktree.

## Result

Jobs, Messaging and Queueing each render four measured diagnostics charts. Desktop uses two columns; phones use one. The runtime history contains completed one-second buckets from the local process, with a maximum of fifteen minutes. Counter rates use the observed interval; duration charts show measured means and leave gaps when no timing was recorded.

Jobs shows execution rates, outcome rates including Completed, execution duration and scheduling delay. Messaging and Queueing show publication/enqueue rates, handler invocation rates, failure rates and operation latency. Handler calls are not unique-message counts. Successful-completion counters use `.completed`, keeping them distinct from legacy typed instrument suffixes.

The collector listens to exact aggregate meter instruments before browser requests. Browser polling and additional viewers cannot increment the measurements. It does not read retained message or occurrence counts; archival and purge therefore are not inputs to these charts. No destructive purge was performed during verification. Jobs retains its separate, initially collapsed retained-history chart with Day/Week/Month controls.

## Automated checks

Run sequentially from the repository root:

```powershell
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MetricsMessageBehaviorsTests|FullyQualifiedName~MetricsQueueingBehaviorTests|FullyQualifiedName~JobSchedulerTelemetryTests|FullyQualifiedName~JobMetricsBehaviorTests" --nologo /p:UseSharedCompilation=false
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --nologo /p:UseSharedCompilation=false
node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs
git diff --check
```

Results: **23 focused Application tests, 1,446 Presentation tests and 9 JavaScript checks passed**. The whitespace check passed.

Application checks cover completion after delegate return, failed results, exceptions, cancellation, fan-out, duration recording and balanced active values. Collector checks cover partial startup intervals, completed buckets, means, idle/disabled sources, exclusion of typed copies and unrelated meters, repeatable snapshots, bounded retention and disposal. Three TestServer hosts verify custom dashboard roots, four chart elements and unavailable metrics responses.

## Browser checks

Used the running WeatherFiesta worktree instance at `https://localhost:5211`, authenticated with its built-in demonstration identity. Existing Alive actions generated real operations. No synthetic workload generator or purge action was added.

- Each feature produced nonzero ingress/execution and completion rates plus measured latency. Broker handler activity was observed independently.
- A second viewer received the same collection start time and unchanged completed buckets.
- A simulated HTTP 503 retained the rendered charts and reported stale refresh status; the next successful response recovered them.
- Jobs retained history opened on demand and switched to Week successfully.
- Leaving Diagnostics removed its chart ownership and polling; returning preserved server collection history.
- All four charts fit without horizontal page overflow at 1440, 1024, 390 and 320 pixels in Tokyo Night, Catppuccin Latte and White: **36 feature/theme/width combinations**. No browser page errors were recorded.
- Desktop and phone captures were inspected for chart sizing, axes, legends and layout.

The browser script and raw results are local execution evidence in `.tmp/playwright-test-workload-diagnostics.js` and `.tmp/workload-browser/results.json`. Test logs are `.tmp/workload-{application,presentation,javascript}-tests.log`.

Actual screenshots are kept with the dashboard consistency assessment, separate from fictional public documentation illustrations:

| Feature | Desktop | Phone |
| --- | --- | --- |
| Jobs | [1440 px](../docs/specs/assets/dashboard-page-consistency/after-jobs-throughput-1440.png) | [390 px](../docs/specs/assets/dashboard-page-consistency/after-jobs-throughput-390.png) |
| Messaging | [1440 px](../docs/specs/assets/dashboard-page-consistency/after-messaging-throughput-1440.png) | [390 px](../docs/specs/assets/dashboard-page-consistency/after-messaging-throughput-390.png) |
| Queueing | [1440 px](../docs/specs/assets/dashboard-page-consistency/after-queueing-throughput-1440.png) | [390 px](../docs/specs/assets/dashboard-page-consistency/after-queueing-throughput-390.png) |

## Boundaries

Metrics remain opt-in through the existing metrics service and application behaviors. An unavailable service is labelled unavailable; an enabled source with no observations is labelled awaiting measurements. Runtime charts do not provide distributed aggregation or durable history. Validation covers the named suites and local sample; the full repository test suite was not run.

## Successful outcome follow-up

The initial Outcome rates chart included only unsuccessful outcomes and retries. Added Completed so successful work appears there too. All six outcomes have theme colors. The focused `DashboardMetricsHistoryTests` suite passed (6 tests), as did the 9 JavaScript refresher checks and `git diff --check`. In the restarted sample, Dispatch alive job produced a Completed rate of 1 execution/s in Outcome rates; its values matched the Completed series in Execution rate. The 1280px and 390px views had no horizontal overflow; the phone legend retained all six entries. [Updated capture](../docs/specs/assets/dashboard-page-consistency/after-jobs-outcomes-completed-1280.png).
