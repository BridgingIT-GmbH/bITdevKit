# Orchestration instance footer verification

Instances now uses the shared Load more / Rows per load footer with a cumulative loaded count. Rows was removed from the filter panel. Load more stays visible and disabled at exhaustion. Existing filters, instance details and actions remain available.

Queries request one extra row to determine whether another batch exists. Continuation uses creation time and instance ID, with a stable initial boundary and no growing offset. Only displayed rows load their detail metadata. The shared append helper preserves loaded rows during refresh and navigation and retains them on a failed append. The result grid contains horizontal table scrolling on narrow screens so the footer remains within the viewport.

## Checks

```powershell
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Orchestration" --nologo /p:UseSharedCompilation=false
git diff --check
```

All 28 Orchestrations presentation tests passed. New coverage verifies timestamp ties, unique rows across batches, a newer arrival between loads, filter scope, look-ahead sizing, footer placement and disabled exhaustion. The whitespace check passed. Full-repository tests were not run.

Live checks used the existing worktree sample at `https://localhost:5211`, with its benign Alive action supplying paging data. A ten-row batch appended to exhaustion without duplicate IDs. A simulated HTTP 503 retained the ten existing rows, exposed retry and recovered successfully. Changing batch size preserved unapplied filter text; Reset retained the selected batch size. Loaded rows survived refresh and navigation through Diagnostics.

Desktop and phone captures were visually inspected. At 390 and 320 CSS pixels the document width stayed within the viewport, the selector was absent from the filter panel and Load more remained visible. The final captures show twelve loaded rows with the exhausted button disabled.

- [Desktop capture](../docs/specs/assets/dashboard-page-consistency/after-orchestration-instance-footer-1440.png)
- [Phone capture](../docs/specs/assets/dashboard-page-consistency/after-orchestration-instance-footer-390.png)

The test log is `.tmp/orchestration-instance-paging-tests.log`. The existing sample was restarted to load the change and remains running. The verification tab was closed and its test row-size preference was removed. No purge or archive action was used.
