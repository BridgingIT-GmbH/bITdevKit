# System diagnostics verification

Verified on 2026-11-04 in this worktree.

The System page now has a remembered Diagnostics tab. Overview retains its current summary, route and database panels. Process and Environment remain separate tabs. The four old browser-local SVG trends were removed; Diagnostics uses the shared Plotly chart layout, theme helpers, refresh controls and navigation lifecycle.

| Chart | Traces |
| --- | --- |
| CPU usage, % | Process CPU; five-sample rolling mean |
| Memory, MB | Working set; private memory; managed memory; GC heap |
| Request rate, requests/s | Started; completed; failed (5xx) |
| Request latency, ms | Interval mean; since-process-start mean |

`DashboardSystemHistory` samples the existing runtime and HTTP snapshot services once per second, independently of browser visits. History is limited to 900 samples and fifteen minutes. Request rates use cumulative counter changes over actual elapsed sampling time. Completed-request status counts supply the denominator for both latency means. No completed requests means a gap in interval latency. Counter resets and source recovery establish a new baseline. Missing sources retain null values and display their availability separately. The sampler stops with the host.

These are local-process measurements. HTTP measurements include dashboard requests. Working set, private memory, managed memory and GC heap describe different memory views and should not be added together. Instance-wide summaries and existing metrics APIs retain their behavior.

## Automated checks

```powershell
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --nologo /p:UseSharedCompilation=false
node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs
git diff --check
```

All **1,452 Presentation tests** and **9 JavaScript checks** passed. The whitespace check passed. The five new tests cover actual elapsed rates, start/completion distinctions, completed-request latency, idle gaps, CPU averaging, missing/failed sources, counter resets and recovery, bounded retention, disposal, custom routes and remembered navigation. Existing workload chart and dashboard tests also passed. The full repository suite was not run.

Logs are `.tmp/system-diagnostics-tests.log` and `.tmp/system-diagnostics-js.log`.

## Browser checks

Verified the running WeatherFiesta worktree sample at `https://localhost:5211` with the shared browser. All four charts initialized with the expected eleven traces. Twelve benign identity-page requests returned HTTP 200 and produced a measured request-rate spike of approximately 13 requests/s including polling traffic. CPU and all four memory traces showed real process samples; both latency traces were populated.

A simulated HTTP 503 preserved all four charts and displayed the stale-data message. Restoring successful requests recovered the Updated status. Switching to Overview showed its summary and Busiest Routes panel with no chart elements. Returning to Diagnostics preserved the server history start time. Process and Environment still returned HTTP 200.

Tokyo Night, Catppuccin Latte and White were checked at 1280, 390 and 320 CSS pixels. All nine settled layouts fitted without horizontal page overflow. Checks waited for both the requested browser viewport and Plotly canvas width to settle. Desktop had two chart columns; phone widths had one. Desktop and phone screenshots were visually inspected.

- [Desktop capture](../docs/specs/assets/dashboard-page-consistency/after-system-diagnostics-1280.png)
- [Phone capture](../docs/specs/assets/dashboard-page-consistency/after-system-diagnostics-390.png)

Raw layout results are `.tmp/system-diagnostics-layouts.json`. The existing sample was restarted to load the changes and remains running. The browser tab opened for verification was closed. Public documentation screenshots were flagged as predating the new tab.
