# Messaging and Queueing dialog tabs

Verified on 2026-11-04.

## Change

Both message dialogs keep their status summary and tab navigation above the scrollable content. They have four local Bootstrap tabs:

- Overview: complete identifiers, message type, creation/expiration, archival date and content hash. Queueing also keeps the queue name.
- Processing: lease, processing time, last error and the registered queue handler or Messaging handler results.
- Properties: complete stored properties, including existing correlation links and copy actions.
- Payload: the stored payload, with JSON formatting where supported.

Each opened item resets to Overview; switching tabs starts at the top of the content. The header retains its message identifier and uses a short dialog title; the complete type remains on Overview. Long keys and values wrap within narrow tables.

## Automated checks

`dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~DashboardPagingTests|FullyQualifiedName~BrokerMessageContextTests" --nologo /p:UseSharedCompilation=false`

20 passed, zero failed; both affected Razor projects compiled. The new regression check verifies the Messaging handler JSON consumed by Processing: handler name, status, attempts, processed date and error. Existing tests cover dashboard routes and paging behavior.

`git diff --check` passed for the files touched in this follow-up.

## Browser checks

Shared T3 preview, existing WeatherFiesta worktree instance on HTTPS 5211 / HTTP 5210. Actual execution screenshots are kept here, outside the public documentation tree.

A live check found an existing Messaging handler projection mismatch: inferred PascalCase names were consumed as camelCase. The projection now explicitly emits the field names the dialog reads.

- Verified both dialogs at 1280 x 800 and 390 x 844. Each has one active panel and starts on Overview; switching to Processing, Properties and Payload shows the corresponding content.
- Messaging's live handler name and zero attempt count now render correctly. The regression test also covers a failed handler with three attempts, a processed timestamp and error text.
- Overview retains complete type and message identifiers. Properties retain their original values, correlation links and copy actions.
- Refresh is paused while either modal is open. Reopening after Payload returns to Overview.
- Detail tables override the shared narrow-screen list-table minimum. Both modal bodies have no horizontal overflow at 390 pixels, and Close remains within the viewport.
- Temporary browser-only fixtures checked empty properties, absent payload, empty handlers and long property/payload strings. They were not sent to the broker. Tab switching reset a scrolled body from 4104 pixels to zero.
- Used the sample's alive controls to obtain retained messages after rebuilding. The existing server was restored on the same worktree ports; no additional host was left running.

## Captures

- [Before Queueing](assets/broker-modal-tabs/before-queueing-1280.png)
- [Queueing Overview](assets/broker-modal-tabs/after-queueing-overview-1280.png)
- [Queueing Processing](assets/broker-modal-tabs/after-queueing-processing-1280.png)
- [Queueing on phone](assets/broker-modal-tabs/after-queueing-overview-390.png)
- [Messaging Overview](assets/broker-modal-tabs/after-messaging-overview-1280.png)
- [Messaging Processing with handler details](assets/broker-modal-tabs/after-messaging-processing-1280.png)
- [Messaging Overview on phone](assets/broker-modal-tabs/after-messaging-overview-390.png)
- [Messaging Processing on phone](assets/broker-modal-tabs/after-messaging-processing-390.png)

Also inspected the dialog in White (light, monochrome) and Tokyo Night (dark), then restored Tokyo Night. [White theme capture](assets/broker-modal-tabs/after-queueing-white-1280.png).
