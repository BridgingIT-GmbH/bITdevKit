# Subscription icon controls verification

2026-11-04.

Messaging and Queueing subscription controls use icon-only buttons with native title tooltips and accessible names. The action group stays at the right edge beside the text. Pause-state badges remain in the text column; queue and message-type controls have distinct icons. Action URLs and their full identifiers are unchanged.

The native title tooltips survive fragment replacement without another initialization step. Narrow text can wrap while the icon group stays together on the same row.

## Checks

- `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~DashboardPagingTests|FullyQualifiedName~BrokerMessageContextTests" --nologo /p:UseSharedCompilation=false`: 20 passed, zero failed. Both affected Razor projects compiled.
- Shared T3 preview: inspected Queueing and Messaging at 1280 x 800 and 390 x 844. Every action group remains at the right edge within its text row; no document overflow at phone width.
- All buttons contain only an icon, have matching native title/accessibility labels and retain those titles after refresh.
- Exercised Queueing queue pause/resume, Queueing type pause/resume and Messaging type pause/resume. Resume icons and pause-state badges appear correctly. Restored all three to their original unpaused state.
- Circuit reset retains its original action URL; its state-changing action was not needed for this layout verification.
- `git diff --check` passed for touched files. The existing sample server remains restored on HTTPS 5211 / HTTP 5210. Closed the owned preview tab.
- Added the row/action guidance to the dashboard skill and flagged older public overview illustrations.

## Captures

- [Before Queueing](assets/subscription-icons/before-queueing-1280.png)
- [Queueing, desktop](assets/subscription-icons/after-queueing-1280.png)
- [Queueing, phone](assets/subscription-icons/after-queueing-390.png)
- [Messaging, desktop](assets/subscription-icons/after-messaging-1280.png)
- [Messaging, phone](assets/subscription-icons/after-messaging-390.png)
