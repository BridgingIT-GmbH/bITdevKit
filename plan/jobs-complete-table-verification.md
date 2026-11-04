# Complete Jobs table verification

Jobs now renders every registration matching the existing filters in one full-width table. Its Load more and Rows per load controls and client continuation state were removed. Old job paging parameters are ignored. The existing bounded query APIs are drained internally, including trigger metadata; the Jobs catalog is not loaded when viewing Occurrences. Occurrences retains its paging controls and behavior.

All 26 focused Jobs/paging Presentation tests passed. Coverage includes 1,003 jobs across two query batches, 501 triggers across two batches, filter scope, ignored legacy cursors and the unchanged occurrence continuation. `git diff --check` passed. Full-repository tests were not run.

```powershell
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~DashboardPagingTests|FullyQualifiedName~Web.Jobs" --nologo /p:UseSharedCompilation=false
```

Live checks on the existing WeatherFiesta worktree sample at port 5211 confirmed all six registrations remained visible with an old one-row paging URL. At 1920px the panel and available content region both measured 1630.4px. Apply/Reset worked, the 390px view had no horizontal page overflow, and Occurrences retained Load more. Desktop and phone captures were visually inspected. Preview screenshot capture timed out twice; the repository Playwright helper completed verification.

- [Desktop](../docs/specs/assets/dashboard-page-consistency/after-jobs-complete-table-1920.png)
- [Phone](../docs/specs/assets/dashboard-page-consistency/after-jobs-complete-table-390.png)

Logs and raw browser results are `.tmp/jobs-complete-table-tests.log` and `.tmp/jobs-complete-table/results.json`. The sample remains running; verification browser tabs were closed.
