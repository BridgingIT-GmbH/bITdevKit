# Job occurrence ID column

2026-11-04.

`JobSchedulerOccurrenceModel.OccurrenceId` is already projected by the query service and used by row actions and continuation. The Occurrences table now displays the complete GUID beside the job name, with the shared copy action. Backend queries, execution IDs and paging remain unchanged.

The shared copy handler previously restored a hardcoded correlation tooltip. It now remembers each button's own title and accessible label, including repeated clicks during feedback. Failure feedback and the existing clipboard fallback remain available.

## Checks

- Focused dashboard paging tests: 19 passed.
- `node --test tests/Presentation.UnitTests/Web/Dashboard/dashboard-copy.test.cjs tests/Presentation.UnitTests/Web/Dashboard/dashboard-refresher.test.cjs`: 12 passed. Covers exact copied values, original-label restoration, repeated clicks and copy failure, plus the existing refresh cases.

## Captures

- [Before, supplied by the user](assets/jobs-occurrence-id/before-user.png)

- Full Presentation suite after the shared-copy change: `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --nologo /p:UseSharedCompilation=false`: 1,463 passed, zero failed.

## Browser verification

Shared T3 preview against the existing WeatherFiesta worktree sample. Dispatched an alive job for a retained occurrence after rebuilding.

- The table has an Occurrence ID header, and each rendered GUID and copy value match the row's exact occurrence identity.
- Desktop layout inspected at 1440 x 900. At 390 x 844, the table scrolls within its own frame; the document does not widen.
- A trusted click on the copy button reported Copied and `Copy occurrence ID: copied`; its original title and accessible name returned after feedback.
- The shared JavaScript checks additionally verify the exact clipboard value, existing correlation labels, repeated-click restoration and failure behavior.
- The existing sample was restored on HTTPS 5211 / HTTP 5210. Closed the owned preview tab.
- Updated the Jobs guide, dashboard skill copy-action notes and older screenshot flag. `git diff --check` passed for touched files.

- [After, desktop](assets/jobs-occurrence-id/after-desktop-1440.png)
- [After, phone with the ID column scrolled into view](assets/jobs-occurrence-id/after-phone-390.png)
