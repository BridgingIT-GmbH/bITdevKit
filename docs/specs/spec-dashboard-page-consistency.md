---
created: 2026-11-03
status: draft
scope: Presentation dashboard
---

# Dashboard Page Consistency

## Purpose and status

Give every dashboard page the same visual and interaction rules, especially the filter and action bars at the top. This document records an assessment and proposes a contract for later implementation. It does not authorize or implement a redesign.

The existing shell, palettes, compact controls, and routed pill tabs provide a useful base. Keep them. The main gap is that each feature assembles its own toolbar and filters, with different spacing, action placement, labels, and refresh behavior.

## Scope

The assessment covers built-in dashboard pages and their routed views in `Presentation.Web` and the feature dashboard packages. Application-owned pages should follow the same contract when they provide equivalent controls. Their business forms can retain task-specific layouts.

In scope for later work:

- Page structure below the shared breadcrumb.
- Refresh status, automatic refresh, and secondary actions.
- Filter containers, labels, spacing, Apply, Reset, and advanced filters.
- Provider, entity, store, and other context selectors.
- Responsive wrapping, accessible names, focus, and theme roles.
- Summary cards, tables, empty states, and detail dialogs where they affect consistency.
- Modal frames, footers, dismissal, internal scrolling, focus, and editing states.
- Incremental loading for growing lists, with a row-count selector beside Load more.
- Routed tab separation between operational lists and diagnostics.

Domain logic, capture policies, query capabilities, authorization, and provider semantics stay with their owning features. A visual cleanup must preserve those contracts.

## Assessment method

Compare rendered pages at a fixed viewport and palette, then inspect their Razor and scripts to explain differences. Separate a visible difference from a verified behavior difference. An empty dataset is sufficient to assess controls, but cannot establish populated-table or detail-dialog conformity.

At the user's request, selected actual review screenshots are embedded in this spec and stored beside it under `assets/dashboard-page-consistency/`. They are reference evidence for this assessment, not public feature-guide examples. Runtime logs, capture scripts, and the full comparison gallery remain outside `docs/`. A later implementation should capture fictional examples separately for public documentation.

### Review baseline and limits

Assessment date is 2026-11-03, against revision `3f8978f96a5c5c9974ac92828c6d6a82136867e6`. The live review used a signed-in administrator, the normal startup seed, and automatic refresh Off where available. No operational actions, throughput tests, stress workloads, or destructive tools were invoked.

The primary comparison uses a 1440 by 1000 CSS-pixel viewport and Tokyo Night. Seven representative filter pages were also captured at 390 by 844. Logs, Auditing, Documents, and Operations were additionally captured in Catppuccin Latte and White. White supplies a monochrome comparison. This is a representative theme review, not a contrast certification of every palette.

Live reads covered populated Logs, Auditing, Jobs, Entities, broker, and blob/permalink views, plus empty Errors, Documents, and Change History views. Runtime was idle. The follow-up review inspected representative read, edit, and confirmation dialogs without saving or confirming mutations. Missing-provider, permission-denied, network-failure, long-identifier, and active-session states still need coverage during implementation.

### Page inventory and assessment

`Close` means the page largely follows the current common structure but still needs the proposed contract. `Partial` means its top controls have clear differences. `Specialized` means it needs shared styling around a task-specific interaction, rather than an artificial Apply/Reset form. These are qualitative assessments, not conformance percentages.

| Page family | Live coverage | Assessment and follow-up |
| --- | --- | --- |
| Dashboard overview | Card overview | Close. Reuse its compact refresh structure; no filter form is needed. |
| Identity | Current principal, claims, and tokens | Specialized. Captured-time wording differs; retain its read-only layout. |
| System | Overview, Process, Environment | Close. Routed pills align well; environment search remains a local utility. |
| Metrics | App, .NET, ASP.NET, Routes | Close. Keep tabs and summaries; adopt shared refresh presentation. |
| Profiling Runtime | Idle session and snapshot controls | Specialized. Session controls need the shared toolbar and action roles. Do not treat snapshot Reset as filter Reset. |
| Profiling Operations and Requests | Both list forms and retained/empty results | Close in structure. Text Apply/Reset is a useful reference; refresh color, field density, and tools still differ. |
| Profiling Timeline and Insights | Both analytical views | Close in structure. Their refresh modes and filter action rows differ from Runtime and Operations. |
| Health | Registered checks | Close. Shared refresh structure and compact summaries; no invented filters. |
| Console | Connected terminal | Specialized. Preserve connection, cancellation, and terminal behavior. |
| MCP | Offline server with registered operations | Close. Reuse the common toolbar while preserving connection-state meaning. |
| Jobs | Registered jobs and occurrences | Close. No ordinary filter form; separate operational tools from refresh. |
| Logs and Errors | Populated Logs and empty Errors | Partial. Wide Apply, icon-only Reset, placeholder-only secondary fields, and draft/export behavior. |
| Logs Stream | Stream controls, not started | Specialized. Start/Stop and connection controls need consistent action roles; reset remains icon-only. |
| Auditing | Executions and Diagnostics | Close in structure. Keep text Apply/Reset and the tools popup; align refresh, labels, dates, and filter background. |
| Messaging and Queueing | Filters, retained messages, subscriptions | Close within their family. Wide Apply differs from analytical pages; refresh currently reads draft fields. |
| Broadcasting | Nodes and delivery summary | Close. Retain feature actions with shared toolbar styling. |
| Orchestrations | Filters and instance views | Close to Messaging and Queueing. Apply/Reset sizing and tools need alignment. |
| Files | Explorer and Diagnostics | Specialized. Provider/path navigation and mutations share one strip; preserve its tree browser. |
| Documents and Blobs | Explorer and Diagnostics for both | Partial. Icon-only clear, Size label, mixed creation/upload controls, and custom container styling. |
| Permalinks | Registry list | Partial. Unboxed, unlabeled filter row; extra title/count; icon-only clear. |
| Change History | Empty query and full filter form | Partial. Dense multi-row criteria and a stretched Apply in a nonterminal grid group. |
| Entities | No selection and a selected entity with rows | Partial. Custom selector/tools row, separate edit header, fixed wider Apply, and icon-only Reset. |
| Application-owned pages | Overview, management, diagnostics lab, request generator | Partial or specialized. Repeated large titles and introductory text differ from built-ins. Adopt common chrome where controls have the same purpose. |

The inventory includes 23 built-in landing pages, counting MCP reached from the header, four application-owned pages, and 13 additional routed views. The entity-selected state is a separate capture because the landing page alone does not expose its filter bar.

The initial evidence set contains 56 saved screenshots: 41 desktop page/view/state captures, seven phone captures, and eight additional palette captures. Additional DOM measurements covered Messaging and Operations at 1024, 768, and 320 pixels. Screenshot capture timed out for those six extra checks; they are measurements, not saved visual evidence. The evidence manifest records routes, palette, viewport, controls, and screenshot filenames. Follow-up dialog captures use the same desktop and phone CSS viewports. Playwright supplied those captures after repeated shared-preview screenshot timeouts.

## Findings

### F01. Apply and Reset differ across pages

Messaging, Queueing, Orchestrations, Auditing, Change History, and the analytical Profiling views use visible Apply and Reset labels. Logs, Errors, Entities, Documents, Blobs, and Permalinks use an icon for the reset or clear action. Logs and Entities use `bi-x-circle`; storage pages use `bi-x-lg`.

Apply sometimes stretches to fill a grid column, sometimes uses its natural width, and sometimes sits beside resource creation or upload controls. Analytical filters can place the pair on a separate final row, while other pages fit them into the last field column.

This is the highest-priority visual inconsistency. A user should recognize the same pair without interpreting an icon or searching a different part of the form.

Source references:

- [Messaging filters](../../src/Presentation.Web.Messaging/Dashboard/Pages/Index.cshtml).
- [Logs filters](../../src/Presentation.Web/Logging/Dashboard/Pages/Index.cshtml).
- [Entities toolbar](../../src/Presentation.Web/Entities/Dashboard/Pages/Toolbar.cshtml).
- [Document filters](../../src/Presentation.Web.Storage/Documents/Dashboard/Pages/Content.cshtml).
- [Blob filters](../../src/Presentation.Web.Storage/Blobs/Dashboard/Pages/Content.cshtml).
- [Permalink filters](../../src/Presentation.Web.Storage/Permalinks/Dashboard/Pages/Index.cshtml).

### F02. Filter containers and field density have several variants

Logs and Errors use a card body with `p-3`; Messaging uses `py-2`; Auditing and analytical Profiling use `p-2`. Entities and storage explorers define feature-specific toolbar borders and padding. Permalinks uses an unboxed grid without visible field labels.

The shared layout gives `.operation-filters` and `.storage-toolbar` an inset background explicitly. Other filter forms do not share that semantic class. Global selectors already unify many card and panel styles, but do not establish one filter contract. Some source padding utilities are overridden by shared CSS, so markup alone overstates certain differences.

Computed values at the desktop baseline confirm the remaining differences:

| Filter | Inner padding | Label font size | Apply width |
| --- | --- | --- | --- |
| Logs | 8.8px | 11.52px | 145px |
| Messaging | 5.6px vertical, 8.8px horizontal | 11.52px | 124px |
| Auditing | 7.2px | 12px | 54px |
| Operations | 7.2px | 11.2px | 54px |
| Documents | 8.8px | 10.88px | 54px |

The common button height is about 28px. That part is already consistent. The visible difference comes mainly from width, arrangement, container background, and labels.

Source references:

- [Shared layout and surface selectors](../../src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml).
- [Auditing styles](../../src/Presentation.Web.Auditing/Dashboard/Pages/Style.cshtml).
- [Entities styles](../../src/Presentation.Web/Entities/Dashboard/Pages/Style.cshtml).

### F03. Refresh presentation varies even within Profiling

Most pages use a neutral outline refresh button. Auditing and Profiling use warning outlines. Interval selectors vary in width and available choices. Some pages use interval selection, Logs uses Tail, and Auditing is manual. Those modes have different purposes; the visual role of Refresh does not need to change with the mode.

Most standard pages render local `Updated` times. Auditing and Operations show explicit UTC times. Status placement and live-status semantics also vary. Entities embeds Refresh in its context-selector row and has a separate edit-mode header.

Source references:

- [Metrics toolbar](../../src/Presentation.Web/Metrics/Dashboard/Pages/Index.cshtml).
- [Profiling runtime toolbar](../../src/Presentation.Web/Profiling/Dashboard/Pages/Index.cshtml).
- [Operations toolbar](../../src/Presentation.Web/Profiling/Dashboard/Pages/Operations/Toolbar.cshtml).
- [Auditing toolbar](../../src/Presentation.Web.Auditing/Dashboard/Pages/Index.cshtml).

### F04. Labels and accessible names are uneven

Permalink filter fields rely on options and placeholders. The secondary identifier fields in Logs and Errors also rely on placeholders. Several refresh and operational icon buttons have a `title` without an explicit accessible name. Newer Auditing, Entities, and Profiling controls generally provide both.

Rows, Size, and page-size selectors represent similar presentation choices with different labels. Domain-specific distinctions such as Archive versus Lifecycle still need accurate feature terminology.

### F05. Apply does not yet imply one consistent state boundary

Messaging builds refresh queries directly from the current input values. Logs also reads the current form during refresh, and updates its export URL on input and change. Consequently, scripts can use draft values without a distinct applied selection. A visible Apply button suggests a stronger boundary.

Auditing and Operations use GET forms and route-based Reset links. Blob clear-filter explicitly clears the prefix and continuation while keeping context. Logs Reset restores Information, 100 rows, and the current UTC day's start. Reset therefore has both visual and semantic differences.

Live checks confirmed two examples. Typing a nonmatching Type in Messaging and using Refresh without Apply requested the draft type and changed Messages from one to zero. Typing a nonmatching Search in Logs changed the export URL before Apply; Refresh then committed that draft into the page URL and returned zero rows. Logs Reset restored Information, 100 rows, and UTC midnight. These are observed behavior differences, not assumptions from button appearance.

The later implementation must state what Reset resets, which preferences it retains, and whether background refresh can commit a draft. Merely changing button classes would leave this inconsistency.

### F06. Page structure is mostly shared, but tool placement drifts

The common pattern is breadcrumb, refresh toolbar, optional routed pill tabs, filters, and results. Metrics, System, storage views, Auditing, and Profiling already use much of this structure.

Differences include an extra title and count in Permalinks, tools inside context selectors in Entities, exposed export and purge controls in Logs, and mutations beside browser filters in storage. Secondary tools and dangerous actions need predictable groups. They must retain their permission and confirmation rules.

### F07. The shared header overlaps page controls on phones

At 390 CSS pixels, the global documentation, MCP, window, theme, texture, and sign-out actions wrap below the heading into the first page-toolbar row. Screenshots show them over the refresh status and controls on Logs, Messaging, Documents, Permalinks, and Operations. This is a shared-shell defect and takes priority over polishing individual filter bars.

Several analytical forms use a full row for actions; other forms allocate a small grid column. On phones, Documents and Entities place filter actions on the left, while analytical forms keep them on the right. Messaging stretches Apply across most of the row.

The tested document width does not exceed the viewport. That check does not catch overlapping elements, so it is insufficient as a responsive acceptance check.

Results tables may scroll inside their own container. Filter fields, toolbars, tabs, and popovers must remain usable without horizontal page scrolling.

### F08. Result layouts differ for useful reasons, but their framing can agree

Messaging and Queueing use a main list beside subscriptions and a chart. Files uses a folder tree and list. Documents reserves a tall result area even when empty. Auditing uses a content-height execution list, while Operations groups executions with timing bars. Those layouts support different tasks.

Standardize the framing around them: panel headers, summary labels, empty messages, row-action sizes, and spacing after the filters. Review fixed viewport-height layouts separately on phones. Do not force all results into the same table or remove useful master/detail views to make screenshots identical.

### F09. Dialogs share a theme but not a complete frame or action contract

There are two implementations: Bootstrap modals for most features and native `<dialog>` elements for Entities. A shared appearance does not require changing the rendering technology, but both need the same title, body, footer, focus, and dismissal rules.

The source shows these concrete differences:

- Jobs uses a large scrollable occurrence-context modal; Messaging and Queueing use extra-large context modals. They provide a header close control but no footer Close action.
- Auditing and Profiling execution details use extra-large scrollable modals with explicit title references, a small footer Close button, and loading/cancellation logic.
- Blob details uses a large scrollable modal with small Close and Save expiration buttons. Its upload and delete forms use default-width dialogs with larger footer buttons.
- Files uses several default-width forms and a larger text-file editor. Its Cancel and mutation buttons use the default Bootstrap size, unlike the compact shared confirmation dialog.
- Permalink details has a footer Close and Save changes pair. Entities editors use Cancel and the less specific OK label.
- Several storage modal roots and the Change History restore modal omit `aria-labelledby`, even though a visible title exists. Bootstrap supplies the dialog role when opened, but that does not supply an accessible name.

These differences affect recognition and keyboard use. Dialog width can vary with content; title treatment, footer roles, button sizes, and accessible naming should not vary accidentally.

The follow-up live review opened and dismissed Auditing and Profiling details, blob details/upload/delete, folder creation, a new document, permalink details, and Entities details/editor. Blob and Auditing details were also captured on a phone viewport. No save, upload, delete, or create action was submitted. Escape dismissal succeeded for these opened dialogs. Source review establishes the remaining lifecycle requirements; dirty-editor protection and focus return after list replacement are not yet verified.

Jobs occurrence, Messaging context, and Queueing context are source-assessed in this follow-up. Their row triggers were absent from the newly seeded lists during capture, so no live screenshot or completed interaction is claimed for those dialogs. The embedded screenshots identify which dialog families were actually opened.

The live new-document editor also aligns its Close and Save Changes actions to the left, unlike blob and entity editors. It occupies almost the entire available height for an empty JSON payload. Align the footer roles first; keep the larger editor size only where its content needs it. Auditing's long execution detail correctly confines scrolling to the body and keeps its footer visible on phones, making it a useful reference for that behavior.

Source references:

- [Shared confirmation and messages](../../src/Presentation.Web/Dashboard/Pages/_DashboardDialogs.cshtml).
- [Jobs context dialog](../../src/Presentation.Web.Jobs/Dashboard/Pages/Index.cshtml).
- [Messaging context dialog](../../src/Presentation.Web.Messaging/Dashboard/Pages/Index.cshtml).
- [Queueing context dialog](../../src/Presentation.Web.Queueing/Dashboard/Pages/Index.cshtml).
- [Auditing detail dialog](../../src/Presentation.Web.Auditing/Dashboard/Pages/DetailDialog.cshtml).
- [Profiling detail dialog](../../src/Presentation.Web/Profiling/Dashboard/Pages/Operations/DetailDialog.cshtml).
- [Entities editor](../../src/Presentation.Web/Entities/Dashboard/Pages/Editor.cshtml).

### F10. Growing lists have inconsistent limits and no common append footer

Entities is the requested reference. Its `LoadMore.cshtml` places a small Load more button immediately beside a small selector, with the accessible name Rows per load, below the list. Its current choices are 10, 25, 50, 100, and 200. Changing the choice reloads the first batch using the applied selection.

Jobs currently reads up to 200 job registrations, 500 triggers, and 50 occurrences, with no append control for older occurrences. Messaging and Queueing have a top-bar Rows selector, defaulting to 50, but the dashboard reads a bounded message list rather than offering continuation. Waiting-message lists have another fixed limit of 25. Scrolling a panel is not a substitute for access to rows beyond these limits.

Make retained occurrences and messages the first migration targets. Job registrations can use the same footer when the collection exceeds one batch. Do not add meaningless append controls to fixed summary cards or a short static list of statuses.

Source references:

- [Entities append footer](../../src/Presentation.Web/Entities/Dashboard/Pages/LoadMore.cshtml).
- [Entities batch-size behavior](../../src/Presentation.Web/Entities/Dashboard/Pages/Script.cshtml).
- [Jobs list queries](../../src/Presentation.Web.Jobs/Dashboard/Pages/Data.cshtml).
- [Messaging list query](../../src/Presentation.Web.Messaging/Dashboard/Pages/Data.cshtml).
- [Queueing list query](../../src/Presentation.Web.Queueing/Dashboard/Pages/Data.cshtml).

### F11. Graph-only tabs do not separate Jobs operations from diagnostics

Jobs puts summaries, registrations, occurrences, and graphs inside one viewport-height composition. Its graph region reserves at least 275px, with a 230px chart canvas. The Realtime Graph and History Graph tabs switch only that bottom region; both lists remain on the same page.

Blob storage already uses the more useful page-level pattern: an Explorer route and a separate Diagnostics route with consistent pill navigation above the content. Jobs should adopt that separation. Moving graphs to a Diagnostics tab gives operational lists more room and avoids loading diagnostic charts during ordinary job investigation.

Messaging and Queueing also place a realtime flow graph beneath messages and subscriptions. They are additional candidates for an Operations/Diagnostics split. Keep subscription and waiting-handler context close to the operational list unless a separate task and sufficient content justify another tab.

| Page family | Tab assessment | Proposed treatment |
| --- | --- | --- |
| Jobs | Registrations, growing occurrences, and graphs compete for the same fixed-height space | Jobs / Occurrences / Diagnostics, with charts entirely in Diagnostics. A two-tab Operations / Diagnostics arrangement is a smaller alternative if separate registration/history routes prove unnecessary. |
| Messaging and Queueing | Operational lists, subscription context, and a flow chart share one composition | Operations / Diagnostics. Keep useful subscription and waiting-handler context with the messages. |
| Files, Documents, and Blobs | Existing Explorer / Diagnostics separation already matches distinct tasks | Retain and use as the reference. Consolidate shared navigation and lifecycle code. |
| Auditing | Executions / Diagnostics already separates review from capture/provider diagnostics | Retain; keep individual execution details in a dialog. |
| Profiling | Existing Runtime / Operations / Requests / Timeline / Insights already separates tasks | Retain. Improve large detail dialogs with local disclosures or tabs only where their content needs it. |
| Entities and Orchestrations | Large lists alone do not justify more top-level tabs | Apply bounded append loading and focused details first. Add tabs only for a distinct task. |
| Health, Identity, and MCP | Compact primary investigations | Keep a single page unless later evidence establishes a separate task. |

Source references:

- [Jobs combined composition](../../src/Presentation.Web.Jobs/Dashboard/Pages/Content.cshtml).
- [Blob Explorer navigation](../../src/Presentation.Web.Storage/Blobs/Dashboard/Pages/Index.cshtml).
- [Blob Diagnostics](../../src/Presentation.Web.Storage/Blobs/Dashboard/Pages/Diagnostics.cshtml).

## Proposed global contract

The requirements below describe the target. They are not a claim that current pages already comply.

### C01. Page order

Use the shared shell and breadcrumb as the page title. Below it, render these regions in order when applicable:

1. A compact page toolbar with page actions, refresh status, refresh mode, Refresh, and a rightmost Tools control.
2. Routed page tabs, outside replaceable result fragments.
3. Context selection and the filter panel. They can share one panel when the context affects all filters.
4. Selection-specific feedback, summaries, and results.
5. Pagination or Load more beneath the results.

Do not add an empty toolbar or filter panel to pages that do not need one. Preserve Console and stream workflows as explicit page types.

### C02. Filter appearance

- Use one named, shared filter-panel style with the existing `--dashboard-surface-inset` and `--dashboard-panel-border` roles.
- Use compact controls based on `form-control-sm` and `form-select-sm`.
- Use consistent panel padding of `.5rem`, field gaps of `.5rem`, and spacing of `.5rem` between top-level regions, matching the compact pages.
- Use one shared small-label style and `.25rem` between a label and its control.
- Provide visible labels for every ordinary filter. Placeholders describe matching syntax or examples; they do not replace labels.
- Allow the number and width of fields to vary. Keep related date bounds and property/operator/value conditions together.
- Keep advanced filters below the basic controls in a labeled disclosure. Preserve typed dimensions, query constraints, and existing validation.

### C03. Apply and Reset

- Show the exact text **Apply** and **Reset** on every explicit filter form.
- Render Apply with `btn btn-primary btn-sm` and Reset with `btn btn-outline-secondary btn-sm`.
- Use natural button widths with a `.5rem` gap. Do not stretch only Apply to fill unused grid space.
- Place the pair at the trailing edge of the final filter row, after all filter controls. A small one-row form can keep them inline. A multi-row form keeps them after its last field or advanced-filter region.
- Keep the pair together when wrapping. On narrow screens it may occupy a separate full-width row, aligned to the end.
- Enter in a normal filter input performs Apply. Textarea editing and specialized keyboard widgets retain their expected behavior.
- Resource mutations such as Upload, Create, Delete, and Purge are outside the Apply/Reset pair.

### C04. Filter state

- Editing ordinary filters changes a draft. Apply commits the draft and reloads the first result batch.
- Refresh, automatic refresh, export, pagination, and Load more use the last successfully applied selection. They do not silently commit draft edits.
- Applying invalid filters shows a local validation message and preserves the previous successful results.
- Reset clears investigation criteria, returns to the page's documented default range, clears paging and selection, and loads the default results immediately.
- Reset preserves the selected context, including entity, source, provider, store, and container, where it remains valid. It also preserves palette, column preferences, page-size preference, and refresh mode.
- Record each feature's default time range and default context fallback. A full-scan permission must return to its safe default on Reset; do not preserve permission to run an expensive scan as a cosmetic preference.
- An immediate context switch is allowed for navigation selectors. Make it distinct from ordinary filters and clear invalid dependent selections.
- Shareable filters remain in query parameters. Preserve existing route, storage-key, cursor, and export contracts during migration.

Retaining page-size preference and defining a draft/applied boundary change current behavior on some pages. Implement and test those changes deliberately.

### C05. Page actions and refresh

- Use neutral outline styling for ordinary Refresh. Reserve warning styling for actions that warrant caution.
- Use one refresh icon, button size, tooltip convention, and accessible name pattern, such as `Refresh logs`.
- Show status in one place, before the mode selector and Refresh. Use a shared time formatter with an explicit zone, proposed as `Updated HH:mm:ss UTC`.
- Refresh status describes the last successful response. Failed refresh retains existing data and exposes an actionable stale/error state through the existing shared mechanism.
- Keep `Off` and shared labels for common intervals. Only offer intervals the feature supports; interval lists do not have to be identical.
- Keep Tail as a named live mode. Preserve the distinction between a moving live range and a fixed historical selection.
- Put exports and secondary tools in the rightmost Tools control. Use the existing Bootstrap icons and themed popovers. Preserve native download forms and query scope.
- Keep destructive tools separate from ordinary refresh and filter actions. Preserve confirmations, capability checks, and authorization.

### C06. Responsive and accessible controls

- At 1440, 1024, 768, 390, and 320 CSS pixels, the page toolbar and filter panel must fit their container.
- Wrap actions and tabs without clipping; constrain popovers to the viewport. Large tables and charts can scroll within a labeled local region.
- Reserve layout space for every wrapped global-header row. Header actions must not intersect the page toolbar, refresh status, or filter controls.
- Every icon-only action has an accessible name and a hover/focus explanation. Decorative icons are hidden from assistive technology.
- Every field has an associated label. Toggle chips expose `aria-pressed`; routed tabs expose current-page state.
- Keyboard order follows the visual order. Apply, Reset, refresh, advanced filters, and tools remain reachable with visible focus.
- Loading and failures expose status without moving focus unexpectedly. Replaced fragments preserve draft edits and modal ownership.
- Use existing palette roles and contrast-adjusted text tokens across light, dark, and monochrome themes.

### C07. Content below the filters

Use the same small table headers, compact card spacing, summary-label hierarchy, and local overflow rules. Align comparable metadata and row actions consistently. Retain useful feature differences such as trees, analytical charts, and execution groups.

Distinguish empty, unavailable, unready, unsupported, and failed states. Do not fill missing observations with a zero. Keep exact identifiers available in tooltips and details while applying existing shortening helpers to dense tables.

Detail and edit dialogs use the shared theme and dialog conventions. Toolbar consolidation must preserve their refresh pause, cancellation, concurrency, focus return, and selection behavior.

### C08. Modal dialogs

- Use a shared visual frame with a title, optional concise record subtitle, scrollable body, and predictable footer. Every dialog has a visible title and an accessible name linked to it.
- Use the same compact header/footer spacing, title style, small buttons, theme roles, and close icon. Define standard, wide, and extra-wide content sizes. Choose by task, not by feature name.
- Read-only details provide a trailing Close button. Editors provide Cancel followed by a specific primary action, such as Save changes, Save expiration, Create folder, or Upload. Replace generic OK where the actual action is known.
- Destructive confirmation provides Cancel and a danger-styled action named for the mutation. Put exact affected identifiers in the confirmation body. Reuse `confirmAction` where its existing selection contract fits.
- Keep dangerous secondary actions separate from the normal editor submit action. Do not give a form's first submit button an unintended destructive default.
- Trap focus, support Escape according to the existing dialog technology, expose visible focus, and return focus to the originating row or an appropriate replacement when that row no longer exists.
- Read-only details may dismiss through backdrop or Escape. Dirty editors must follow one shared discard/cancel policy. Do not lose typed content through backdrop dismissal, fragment refresh, or a failed save.
- Keep the title and footer available while long payloads scroll inside the body. At phone widths, fit the viewport and wrap footer actions without clipping.
- Preserve abort-on-close, stale-response rejection, loading/error announcements, refresh pause, ETags, and validation. Detail navigation must keep focus in the dialog and retain the original list position.
- Avoid stacked dialogs. If confirmation originates inside an editor, preserve editor state and define how focus returns after cancellation.

### C09. Load more with adjacent row-count selection

The placement requirement is explicit: below each growing list, place **Load more immediately followed by the row-count selector**, in the same compact centered footer, matching Entities. The selector has the visible or accessible label Rows per load. It does not belong in the top filter/action bar.

- Use `btn btn-outline-secondary btn-sm` for Load more and a compact `form-select-sm` beside it with the same gap and alignment on every migrated page.
- Treat the selector as a batch size, not a global result limit or total count. Default to 50 and reuse the Entities choices where the provider supports them. Preserve lower provider caps and label unsupported choices honestly.
- Selecting another amount stores that list's preference and reloads its first batch, matching Entities. Keep the applied filters and context. Reset continuation and row selection without changing draft filter inputs.
- Apply and Reset start a new first batch with the selected amount. Remove the duplicate top-bar Rows selector on Messaging and Queueing when it serves this same batch-size purpose.
- Load more appends the next batch for the applied selection. Keep existing rows, scroll, and selection stable. Deduplicate by exact row identity; preserve deterministic ordering and an upper time boundary where needed.
- Disable the button while loading. On failure, preserve rows and continuation, show local feedback, and allow retry of the same batch. At the end, retain the footer with a disabled Load more button and the usable selector, matching the Entities reference.
- Use a stable cursor or a bounded, deterministic paging strategy supported by the owning query service. Increasing Take and calling that continuation is not sufficient.
- When new records arrive, do not insert them ahead of appended history and shift later page boundaries. Preserve the current browsing boundary and expose an explicit refresh/new-record indication. Refresh can update known row states, but must not silently collapse the appended history to the first batch.
- Scope list state and the preference by feature, route/view, and list identity. Jobs registrations and occurrences need independent continuation and footer state.
- Show loaded count and total only when the query supplies a truthful total. Keep global summary counts separate from loaded-row counts.

Existing query APIs can support the first implementation. `IMessageBrokerService.SearchMessagesAsync` and `IQueueBrokerService.SearchMessagesAsync` return `ResultPaged<T>` from a `FilterModel`. Jobs query requests already provide Skip, Take, and sorting. Reuse these before adding any query contract. Verify tie-breaking and moving-dataset behavior; if an existing provider cannot preserve a browsing boundary, make the smallest required change in its owning query layer. The reusable footer must not access persistence directly.

### C10. Page-level tabs and Diagnostics separation

- Use routed `nav nav-pills` beneath the shared page toolbar and outside refreshed content, matching Blob Explorer/Diagnostics. Use the shared view-tabs component and `DashboardPage.RememberedViews` behavior.
- Jobs has Jobs, Occurrences, and Diagnostics views. Jobs contains registration/scheduling controls and compact relevant summaries. Occurrences contains the growing execution history with its own filters and append footer. Diagnostics contains the realtime and history graphs, time-window controls, and diagnostic summaries.
- Preserve the existing Jobs root as the registration landing route. Proposed additional routes are `/jobs/occurrences` and `/jobs/diagnostics`, relative to the configured dashboard root. Final route names remain an implementation detail; use the existing path helpers and authorization group.
- Realtime/History may remain local subviews within Jobs Diagnostics. They must not remain the only separation while charts still occupy the operational list page.
- Messaging and Queueing should use Operations and Diagnostics views. Keep messages, subscriptions, and waiting-handler information in Operations; move flow graphs and their time/history controls to Diagnostics.
- Preserve the currently applied filters, selected row-count preference, appended list, scroll, and appropriate query scope when returning to a list. Diagnostics-specific date windows must not silently rewrite operational filters.
- Initialize and poll charts only for the active Diagnostics view. Dispose graph timers, subscriptions, and chart instances on leaving. Graphs resize correctly after activation and theme changes.
- Explain graph scope where it differs from the filtered list, such as session-wide realtime samples. Moving a graph into a tab must not imply it represents the same dataset as the current list.
- Add a tab for a distinct task with enough content. Keep compact Health or Identity views as single pages; do not create empty Diagnostics tabs for visual symmetry.

## Implementation approach for later work

Keep the change in Presentation. Reuse `_DashboardLayout`, `DashboardPageSlice`, existing RazorSlice partials, `ViewNavigation`, `DateRange`, shared dialogs, and `window.bdkDashboard.createRefresher`. Keep provider queries and mutations in their existing services.

Introduce a small shared toolbar/filter styling contract and reusable markup where it removes real duplication. Avoid a new UI framework, a generic query builder, or a dashboard-specific persistence abstraction. Feature pages still own their field sets and supported actions. Replace feature-name CSS matching for common controls with explicit shared classes as pages migrate.

Use the current compact analytical forms and explicit Apply/Reset pair as a starting point. They are references for parts of the contract, not a completely conforming page to copy wholesale.

## Reusable component candidates

Names in this section are proposals, not existing public APIs. Prefer small RazorSlice partials with typed models and shared CSS in `Presentation.Web/Dashboard`. Feature dashboard packages can consume them without depending on one another. Client helpers belong under the existing `window.bdkDashboard` object.

### Existing elements to reuse

Reuse these before extracting another implementation:

- `_DashboardLayout` and `DashboardPageSlice` already own shell and title composition. Fix the phone header overlap there once.
- `DashboardPage.RememberedViews` and `Dashboard/Pages/ViewNavigation.cshtml` already own remembered navigation. The feature-specific pill markup is a separate extraction opportunity.
- `DateRange`, `DateRangeScript`, `DashboardDateRangeModel`, and `DashboardDateTime` already provide date-range behavior. Align Auditing with them rather than making a second date picker.
- `window.bdkDashboard.createRefresher` already owns request cancellation, stale-response protection, interval scheduling, hidden-page pause, and refresh failure signaling. New toolbar markup must connect to it.
- `_DashboardDialogs`, `confirmAction`, `showMessage`, and `postAction` already provide shared confirmation and feedback behavior. Feature-specific detail payloads and editors remain feature-owned.
- Existing chart helpers, semantic palette roles, `DashboardCorrelationLink`, and `Shortener` already solve common chart and identifier presentation concerns.

### Extract first

| Candidate | Existing consumers and duplication | Shared responsibility | Feature-owned responsibility |
| --- | --- | --- | --- |
| `DashboardPageToolbar` | Overview, System, Metrics, Health, Jobs, MCP, broker pages, storage, Auditing, Profiling | Row layout, spacing, wrapping, action slots, and placement of refresh and tools | Which actions are available and which workflow each action invokes |
| `DashboardRefreshControls` | Repeated Updated span, interval select, refresh icon, and IDs across most pages | Consistent status markup, formatter, neutral refresh button, accessible name, optional interval/Tail presentation | Supported modes and intervals, content URL, storage key, and pause conditions supplied to the existing refresher |
| `DashboardFilterPanel` | Logs/Errors cards, broker filter cards, Auditing, analytical forms, Entities, and document/blob toolbars | Filter background, border, density, field regions, optional advanced disclosure, and a final action region | Field markup, contextual selectors, validation rules, matching modes, and supported query predicates |
| `DashboardFilterActions` | Apply/Reset repeated in every explicit filter form | Text labels, order, natural widths, gap, busy/disabled presentation, form association, and Reset link/button support | Apply and Reset destinations or callbacks, default state, and permission to query |
| `DashboardIconAction` | Refresh, export, compare, details, and mutation triggers | Shared small-button/link markup, icon, accessible name, focus treatment, tooltip initialization contract, and semantic role | Target URL or callback, enabled reason, authorization, and confirmation requirement |
| Applied-filter client helper | Logs, Errors, Messaging, Queueing, Orchestrations, and storage pages assemble similar request state separately | Draft/applied state separation, commit/reset notifications, and a stable applied-selection accessor for refresh/export/paging | Query serialization, cursor rules, safe defaults, provider changes, and server validation |
| `DashboardListFooter` | Entities is the placement reference; Jobs occurrences, Messaging, and Queueing need the same growing-list controls | Compose Load more and its immediately adjacent Rows per load selector, count/status, busy/end/retry states, and stable instance IDs | Continuation queries, exact row identity, applied filters, sorting, refresh reconciliation, and provider limits |
| `DashboardModalFrame` and `DashboardModalActions` | Bootstrap feature modals and native Entities dialogs repeat title/body/footer markup with different naming and button conventions | Shared visual sizing, linked title, compact body/footer, Close or Cancel/action layout, and accessible naming requirements | Bootstrap/native adapter, record content, dirty state, validation, permission checks, ETags, and actual mutations |

Implement the state helper only where it removes repeated logic. A normal GET form already has a useful committed state in its URL. Do not replace it with a client framework solely for uniformity.

### Extract after the top bars agree

| Candidate | Evidence and consumers | Boundary and priority |
| --- | --- | --- |
| `DashboardViewTabs` | System, Metrics, Profiling, Auditing, and storage repeat routed `nav nav-pills` markup | Accept label, URL, view key, and current state. Preserve `data-dashboard-view`, `aria-current`, and remembered-view behavior. High value, but keep route generation with the feature. |
| `DashboardToolsPopover` | Auditing export, Profiling tools, and Entities saved-filter/column/export popovers use similar anchored panels | Own anchor alignment, viewport bounds, Escape, click-outside, focus return, and form semantics. Each feature supplies its content. Do not convert form popovers into ARIA menus. |
| `DashboardRowsSelector` | Rows, Size, Page size, and bottom batch selectors recur | For growing lists, render it through `DashboardListFooter` immediately beside Load more. Keep genuine analytical result-limit controls distinct. Preserve allowed sizes and provider caps. |
| `DashboardSummaryMetric` | Overview, broker, Jobs, Health, Metrics, Auditing, and analytical pages repeat small label/value cards | Own label/value/unit layout and theme role. Features supply scope, observed/unavailable state, and meaning. Existing `dashboard-metric` CSS is the starting point. Medium priority. |
| `DashboardResultState` | Empty lists, unavailable services, read failures, and loading messages repeat | Standardize compact visual and accessible status treatment. Require an explicit state and feature-supplied message. Preserve the distinction between no matches, no provider, and a failed read. Medium priority. |
| `DashboardLoadMore` | Logs, Errors, Auditing, and Entities expose similar append controls; Jobs and brokers need them | Use as an internal element of `DashboardListFooter`, rather than a separately placed button. Keep ID deduplication, pagination cursors, and append processing with the feature. Prioritize Jobs occurrences and broker messages. |
| Shared detail-loader lifecycle | Auditing and Profiling already implement cancellable details and focus return | Reuse loading/error/abort/stale-response/focus behavior around the shared modal frame. Keep record-specific content, editors, ETags, and destructive actions outside a generic detail model. |

### Extraction decisions and trade-offs

The following are draft decisions for this spec. A separate ADR is only needed if implementation introduces a significant new architectural dependency or changes the established rendering model.

**Context.** Shared CSS already exists, but independently composed markup and scripts still produce different controls. Feature packages must retain their own query, capability, and authorization behavior.

**Proposed decision.** Use typed RazorSlice partials for repeated markup, shared semantic classes for layout, and small JavaScript helpers for repeated lifecycle and state behavior. Keep the current rendering model and dependency direction.

**Alternatives.** CSS-only changes are the least invasive, but cannot enforce button names, labels, order, or applied state. Copying a reference form is initially fast, but preserves the duplication that caused this drift. A schema-driven form engine would require many special cases for entity conditions, dimensions, file navigation, provider scans, and native downloads.

**Consequences.** Shared components make routine controls consistent and reduce repeated accessibility and responsive fixes. They also increase the effect of a shared regression. Validate changes against two distinct consumers before migrating the rest. Keep extension points limited to known feature differences.

### Component acceptance criteria

- One shared change controls Apply/Reset markup and one controls refresh appearance across migrated pages.
- One composed footer controls both Load more and the selector beside it. Consumers cannot accidentally put the batch selector back into the top filter row.
- Shared dialog actions enforce read-only Close versus editor Cancel/submit roles without taking over mutation or concurrency logic.
- Every component accepts a stable instance prefix or explicit IDs so labels, forms, and scripts cannot collide when composed twice.
- Shared markup has no service lookup for feature data and no embedded provider query, authorization decision, or mutation endpoint.
- Native links, GET forms, form validation, and downloads continue to work where the feature supports them.
- Client components can initialize after fragment replacement and dispose listeners, timers, tooltips, and request ownership without duplicate handlers.
- A page can supply a feature-specific field or action without copying the entire shared component.
- Existing storage keys, query parameters, exact identifiers, capability limits, and cancellation behavior remain compatible unless a separately tested behavior change is required by C04.

## Agent implementation phases

All phases are Planned. The assessment and reference screenshots are complete; implementation has not started. This section replaces the earlier delivery sequence with bounded work packages. Keep this plan in the spec rather than creating a second task manifest.

### Execution rules

- Work on the phase named in the implementation request. Follow `AGENTS.md` and the dashboard-pages skill, inspect current source, and preserve unrelated changes.
- Read the phase dependencies before editing. If a prerequisite is missing, report the concrete missing component or behavior. Do not silently implement an additional phase or mark a prerequisite complete.
- Keep queries, capabilities, authorization, mutations, concurrency, and feature-specific content with their existing owners. Add shared components when the named phase has real consumers; do not prebuild every candidate.
- Run top-level builds and tests sequentially. Use existing test projects and browser workflows. Do not add a separate acceptance framework, source scanner, or generated conformance manifest.
- Finish a phase by updating its status below and recording changed files, commands/results, screenshot references, limitations, and remaining work under that phase. Mark it Complete only when its exit criteria pass. If a check cannot run, record it as unverified and keep the phase In progress.
- Preserve the original screenshots as the before baseline. Add separately named after captures for changed pages using comparable viewports, palettes, and populated/empty states. Follow the screenshot distinction between assessment evidence and public documentation examples.
- Commit or push only when the implementation request asks for it. Document edits alone do not start any implementation phase.

### Requirements and working defaults

The user explicitly requested a global look and feel, reusable elements, consistent filter actions and dialogs, a row-count selector directly beside Load more below growing lists, and a Jobs Diagnostics tab for graphs. These are requirements.

C01 through C10 provide the working defaults for later implementation. Specific component names, exact spacing, UTC status wording, and the three-view Jobs layout are proposals rather than claims of user approval. When implementation of this spec is requested without another choice, use those defaults and record the result. The two-view Jobs layout in F11 is an alternative, not a second task to implement. Seek clarification only if a material conflict prevents the requested phase from proceeding.

### Phase status and dependencies

| Phase | Work package | Depends on | Status |
| --- | --- | --- | --- |
| PHASE-01 | Baseline and contract readiness | None | Planned |
| PHASE-02 | Shared toolbar/filter controls and phone header | PHASE-01 | Planned |
| PHASE-03 | Investigation filters and applied state | PHASE-02 | Planned |
| PHASE-04 | Storage and Change History controls | PHASE-03 | Planned |
| PHASE-05 | Shared modal frames and lifecycle | PHASE-04 | Planned |
| PHASE-06 | Growing-list footer and continuation | PHASE-03, PHASE-05 | Planned |
| PHASE-07 | Operational and Diagnostics routes | PHASE-06 | Planned |
| PHASE-08 | Remaining pages and final verification | PHASE-02 through PHASE-07 | Planned |

The default execution order is PHASE-01 through PHASE-08. Dependencies describe prerequisites, not authorization to launch parallel agents.

### PHASE-01. Baseline and contract readiness

Scope is the spec, its source references, and local read-only inspection. No application code changes belong to this phase.

- [ ] TASK-01.1: Compare the current revision, dashboard page providers, and `src/Presentation.Web/Dashboard/Pages/_DashboardLayout.cshtml` with the review baseline. Record source drift that changes a finding or target file.
- [ ] TASK-01.2: Record the working defaults used for C03 through C05 and C08 through C10. Map F01 through F11 to the later phases below. Retain any unresolved material conflict with its affected phase.
- [ ] TASK-01.3: Confirm the actual build/test commands, host startup isolation, and availability of fictional rows for Jobs occurrence and broker context dialogs. Inspect existing query paging contracts; do not add a new query abstraction.

Exit criteria: source references still resolve, drift and material blockers are explicit, the defaults for implementation are recorded, and the verification host can be started without targeting another instance's data. This phase needs no application unit-test run.

### PHASE-02. Shared toolbar/filter controls and phone header

Scope is C01 through C03, C05, and C06, addressing F01 through F04, F06, and F07. Start in `src/Presentation.Web/Dashboard/Pages/`, `src/Presentation.Web.Messaging/Dashboard/Pages/Index.cshtml`, and `src/Presentation.Web/Profiling/Dashboard/Pages/Operations/Index.cshtml` plus its `Toolbar.cshtml`.

- [ ] TASK-02.1: Fix the shared header so wrapped global actions reserve space and cannot overlap page controls at 390 and 320 pixels.
- [ ] TASK-02.2: Extract the minimum shared toolbar, refresh, filter-panel, and Apply/Reset markup/styles. Preserve stable IDs, tooltip behavior, theme roles, and `createRefresher` bindings.
- [ ] TASK-02.3: Migrate Messaging and Operations as the simple and multi-row references. Keep ordinary Reset text, natural action widths, neutral Refresh, and consistent labels. Leave new append controls and Diagnostics routes to PHASE-06 and PHASE-07.

Exit criteria: both reference pages meet the scoped contracts at 1440, 1024, 768, 390, and 320 pixels, in dark, light, and monochrome palettes. Their existing filter and refresh behavior still works. Build affected web projects and run `tests/Presentation.UnitTests/Presentation.UnitTests.csproj` because this phase changes shared layout/helpers. Save before/after evidence for both references.

### PHASE-03. Investigation filters and applied state

Scope is C02 through C05 and C07, addressing F01 through F06. Targets are Logging, Profiling, and Entities dashboard pages under `src/Presentation.Web/`, `src/Presentation.Web.Auditing/Dashboard/Pages/`, and the Messaging, Queueing, and Orchestrations filter scripts in their `src/Presentation.Web.*` packages.

- [ ] TASK-03.1: Migrate Logs, Errors, Auditing, Entities, and the remaining analytical Profiling forms to the shared controls. Add visible or associated labels where placeholders currently carry the field meaning.
- [ ] TASK-03.2: Separate draft and applied filters for fragment-driven pages, including the broker and orchestration forms. Refresh, export, paging, and existing Load more consume the applied selection. Keep normal GET forms where they already provide that boundary.
- [ ] TASK-03.3: Implement the C04 Reset rules per feature, document its default range/context fallback, and preserve preferences and explicit navigation.

Exit criteria: typing without Apply leaves results, exports, and next-batch queries unchanged; Apply and Reset have the documented results; invalid filters retain prior data. Run relevant existing Logging, Auditing, Profiling, Entities, Messaging, Queueing, and Orchestrations tests. Add focused state/query tests where coverage is missing and verify draft/refresh/export behavior in the browser.

### PHASE-04. Storage and Change History controls

Scope is C01 through C07, addressing F01 through F06 and F08. Targets are `src/Presentation.Web.Storage/{Files,Documents,Blobs,Permalinks}/Dashboard/` and `src/Presentation.Web.EntityFramework/ChangeHistory/Dashboard/`.

- [ ] TASK-04.1: Migrate storage filter/context strips and Change History forms to shared controls. Add Permalink labels and separate creation/upload tools from the Apply/Reset group.
- [ ] TASK-04.2: Apply C04 while preserving provider/store/container context, dependent filters, scan permission defaults, ETags, and supported match modes. Do not make all providers support identical queries.
- [ ] TASK-04.3: Align result framing and empty/unavailable/error messages without replacing the Files tree or forcing all storage results into one layout.

Exit criteria: filter/reset/provider switching preserves supported behavior and safe scan defaults, and continuation/download/edit targets retain exact identifiers. Run existing `DocumentStorageDashboardTests`, `BlobStorageDashboardTests`, and `ChangeHistoryDashboardTests` plus relevant Files/Permalinks tests. Verify populated, empty, and unsupported/unavailable cases with representative provider capabilities.

### PHASE-05. Shared modal frames and lifecycle

Scope is C08 and C06, addressing F09. Start with `src/Presentation.Web/Dashboard/Pages/_DashboardDialogs.cshtml`, Auditing `DetailDialog.cshtml`, storage modal markup, and Entities `Content.cshtml`, `Editor.cshtml`, `Detail.cshtml`, and `Script.cshtml`. Then cover Jobs, Messaging, Queueing, Profiling, Health, MCP, and Change History dialogs.

- [ ] TASK-05.1: Extract a small shared visual/action contract. Validate it with Auditing details, a Blob editor, and the native Entities dialog before migrating other families. Do not replace native dialogs solely to match Bootstrap markup.
- [ ] TASK-05.2: Link every visible title to its dialog, align header/footer sizing and action roles, and replace generic OK with a specific action. Keep destructive confirmation separate from ordinary editor submit.
- [ ] TASK-05.3: Reuse detail loading/cancellation/focus logic where it actually repeats. Implement consistent dirty-editor dismissal, retain form values on failed saves, and preserve refresh pause and concurrency checks.

Exit criteria: every affected dialog meets C08 on desktop and phone, including keyboard focus, Escape/backdrop policy, long-body scrolling, retry, and return to the originating list. Verify row-backed Jobs/Messaging/Queueing dialogs with fictional rows rather than claiming source-only coverage. Run existing `AuditDashboardDialogTests`, relevant `OperationProfilingDashboardTests`, Entities, and storage tests. Check two mounted dialog instances for ID and handler collisions when the shared component supports composition.

### PHASE-06. Growing-list footer and continuation

Scope is C09 and C04, addressing F10. Targets are Entities `LoadMore.cshtml` as the reference, Jobs `Pages/Data.cshtml`, and Messaging/Queueing `Pages/Index.cshtml`, `Data.cshtml`, and `Content.cshtml`. Query owners are `src/Application.Jobs/Queries/`, `src/Application.Messaging/IMessageBrokerService.cs`, and `src/Application.Queueing/IQueueBrokerService.cs`.

- [ ] TASK-06.1: Extract `DashboardListFooter` and render Load more immediately followed by Rows per load below each growing list. Migrate Jobs occurrences and broker messages, remove duplicate top-bar batch selectors, and give each Jobs list independent state.
- [ ] TASK-06.2: Reuse Jobs Skip/Take queries and broker `SearchMessagesAsync` with bounded, deterministic ordering. Preserve applied query scope, exact identity, continuation, and a browsing boundary while new records arrive. Change an owning query layer only if its existing contract cannot support this behavior.
- [ ] TASK-06.3: Implement batch-size persistence, first-batch reload on size change, append/retry/end states, loaded-count semantics, and refresh reconciliation. Do not hide older rows behind a larger Take or materialize an unbounded collection.

Exit criteria: each migrated list loads at least three distinct batches with no duplicates or skipped boundary rows; changing the selector reloads the first batch; failure retries the same continuation; new arrivals do not corrupt historical browsing. At the end, Load more is disabled while the adjacent selector remains usable. Run relevant Presentation and owning-query tests, including tie timestamps, row deletion, and concurrent arrivals where the provider supports them. Capture the footer on Jobs, Messaging, and Queueing at desktop and phone widths.

### PHASE-07. Operational and Diagnostics routes

Scope is C10, addressing F11. Targets are `DashboardEndpoints.cs`, `DashboardPageProvider.cs`, and the page/fragment/chart code in `src/Presentation.Web.Jobs/Dashboard/`, `src/Presentation.Web.Messaging/Dashboard/`, and `src/Presentation.Web.Queueing/Dashboard/`. Reuse Blob storage's existing navigation pattern and shared remembered views.

- [ ] TASK-07.1: Implement the recorded Jobs layout default: Jobs, Occurrences, and Diagnostics. Preserve the Jobs root and move realtime/history graphs and their windows entirely into Diagnostics.
- [ ] TASK-07.2: Add Operations/Diagnostics views for Messaging and Queueing. Retain subscriptions and waiting-handler context with messages; move flow charts into Diagnostics. Reuse routed pill markup and preserve configured dashboard roots and access rules.
- [ ] TASK-07.3: Restore applied filters, loaded history, row-count preference, and scroll when returning to a list. Stop polling and dispose charts when leaving Diagnostics; initialize and resize them on activation and palette changes.

Exit criteria: direct links, remembered views, refresh fragments, and a custom dashboard root reach the correct view. Operational pages load no diagnostic charts or graph polling. Changing tabs preserves PHASE-06 list state and does not alter query scope. Run existing Jobs/Messaging/Queueing endpoint tests plus relevant dashboard navigation tests, and verify chart lifecycle and repeated tab transitions in the browser.

### PHASE-08. Remaining pages and final verification

Scope is the remaining page inventory, C01 through C10, and all findings. Targets include Dashboard overview, System, Metrics, Health, Identity, Console, Logs Stream, MCP, Broadcasting, Orchestrations, and equivalent application-owned controls. Keep specialized terminal, stream, and navigation workflows intact.

- [ ] TASK-08.1: Migrate remaining equivalent toolbars, controls, and result framing. Record justified exceptions for specialized workflows. Do not add artificial filters, append controls, or empty Diagnostics tabs.
- [ ] TASK-08.2: Run the complete acceptance criteria against the final page inventory. Cover populated/empty/unavailable/error states, long identifiers, keyboard use, responsive widths, and dark/light/monochrome palettes. Recheck shared component disposal and explicit navigation after fragment replacement.
- [ ] TASK-08.3: Add after screenshots, update affected fictional public documentation captures and dashboard guidance, and record the final test results and any unresolved limitations. Preserve the original assessment captures.

Exit criteria: all global acceptance criteria pass or a remaining gap keeps this phase In progress. Run the complete Presentation unit suite and relevant owning-layer tests for actual behavior changes, then finish browser verification. Stop verification hosts and confirm their ports are released. Mark the overall spec implemented only after every phase is Complete.

### Agent task prompt

Use this pattern with the actual phase identifier:

> Implement PHASE-02 only from `docs/specs/spec-dashboard-page-consistency.md`. Follow `AGENTS.md` and the dashboard-pages skill. Check the declared prerequisites, apply this phase's contracts and working defaults, and preserve feature behavior outside its scope. Run its required tests and browser checks. Update its checklist, status, results, and screenshot references in the spec. Leave later phases untouched. Do not commit or push.

The phase identifier in this example is illustrative; it does not request execution.

## Acceptance criteria

- Every explicit filter form has the same labeled Apply/Reset pair and shared panel style.
- Comparable controls have the same computed height, label style, spacing, border, palette role, and keyboard behavior within one theme and viewport.
- Refresh has one neutral visual role and consistent status placement, including across Profiling views.
- Editing a filter without Apply does not change refreshed results, exports, or subsequent result batches.
- Apply commits one selection, resets paging, and retains the user's context. Reset follows C04 and displays its resulting state accurately.
- Tab restoration cannot override Apply, Reset, paging, or an explicit tab choice.
- Jobs occurrences, Messaging, and Queueing can load beyond their first batch. Their row-count selector is directly beside Load more below the corresponding list, with no equivalent duplicate selector in the top bar.
- Append failures retain rows and continuation; retries do not duplicate or skip rows. New arrivals cannot shift the historical browsing boundary. Changing batch size resets paging using the applied filters.
- Every modal has an accessible title, consistent footer roles and compact action sizes, bounded body scrolling, Escape handling, and focus return. Unsaved editor changes survive refresh and failed saves.
- Jobs graphs are available only in the Diagnostics page-level tab, using the Blob storage navigation pattern. Diagnostics tab switching preserves operational list filters, loaded rows, scroll, and batch preference.
- Inactive Diagnostics views stop polling and dispose charts; returning and changing palette does not create duplicate timers or broken chart sizing.
- Narrow-screen controls and menus fit at the widths in C06. Result overflow stays inside its own region.
- Empty, unavailable, failed, populated, and long-identifier states preserve their meaning and remain readable.
- Existing permissions, capability checks, safe scan behavior, export scope, and mutation confirmations pass their existing tests.
- Runtime checks include a dark, a light, and a monochrome palette, with keyboard use and at least one replaced-fragment workflow per page family.

Use the existing Presentation unit tests and relevant feature tests for behavior changes. Add focused tests where a real state, accessibility, or query regression needs coverage. Keep browser captures as review evidence; no separate acceptance framework or generated source scanner is required.

## Reference screenshots

These are current rendered pages and dialogs, captured before implementation. They document differences; they do not show the proposed target. Desktop captures use 1440 by 1000 CSS pixels and phone captures use 390 by 844. The initial shared-preview images are scaled raster captures; the follow-up Playwright PNGs use 1x scale. Select an image to open the full capture.

The 24 selected PNGs are stored in `assets/dashboard-page-consistency/` and travel with this spec. The complete initial 56-image gallery and follow-up manifests remain local runtime evidence outside the documentation tree.

<details>
<summary>Filter and action bars</summary>

### Logs

A wide Apply button, icon-only Reset, and a second row of identifier fields.

[![Logs before dashboard consistency changes](assets/dashboard-page-consistency/logs.png)](assets/dashboard-page-consistency/logs.png)

### Messaging

Visible Reset text, a stretched Apply button, and a top-bar Rows selector targeted for relocation.

[![Messaging before dashboard consistency changes](assets/dashboard-page-consistency/messaging.png)](assets/dashboard-page-consistency/messaging.png)

### Auditing

Text Apply/Reset on the final row and a warning-colored refresh control.

[![Auditing before dashboard consistency changes](assets/dashboard-page-consistency/auditing.png)](assets/dashboard-page-consistency/auditing.png)

### Documents

Filtering and record creation share one strip; clear is icon-only.

[![Documents before dashboard consistency changes](assets/dashboard-page-consistency/documents.png)](assets/dashboard-page-consistency/documents.png)

### Permalinks

Unboxed controls with no visible field labels.

[![Permalinks before dashboard consistency changes](assets/dashboard-page-consistency/permalinks.png)](assets/dashboard-page-consistency/permalinks.png)

### Profiling Operations

An inset filter panel with text actions and advanced fields.

[![Profiling Operations before dashboard consistency changes](assets/dashboard-page-consistency/profiling-operations.png)](assets/dashboard-page-consistency/profiling-operations.png)

</details>

<details>
<summary>Growing lists and tab separation</summary>

### Entities append footer

Requested placement reference: Load more followed immediately by the row-count selector below the list. The small seeded list has no next batch, so Load more is disabled.

[![Entities append footer before dashboard consistency changes](assets/dashboard-page-consistency/entities-load-more-reference.png)](assets/dashboard-page-consistency/entities-load-more-reference.png)

### Jobs current layout

Registrations and occurrences compete with a permanently reserved graph region. This capture has no occurrences; it does not demonstrate append behavior.

[![Jobs current layout before dashboard consistency changes](assets/dashboard-page-consistency/jobs-current-layout.png)](assets/dashboard-page-consistency/jobs-current-layout.png)

### Jobs current graph tabs

Realtime/History switches only the graph region. Proposed page-level Diagnostics separation is not yet implemented.

[![Jobs current graph tabs before dashboard consistency changes](assets/dashboard-page-consistency/jobs-current-history-graph.png)](assets/dashboard-page-consistency/jobs-current-history-graph.png)

### Blob Diagnostics reference

Existing routed Explorer/Diagnostics pills provide the reference for separating Jobs graphs from operations. No throughput test was started.

[![Blob Diagnostics reference before dashboard consistency changes](assets/dashboard-page-consistency/blobs-diagnostics.png)](assets/dashboard-page-consistency/blobs-diagnostics.png)

</details>

<details>
<summary>Modal dialogs</summary>

### Auditing execution details

Extra-wide read-only detail with a linked accessible title and a small trailing Close action.

[![Auditing execution details before dashboard consistency changes](assets/dashboard-page-consistency/dialog-auditing-detail.png)](assets/dashboard-page-consistency/dialog-auditing-detail.png)

### Profiling execution details

Existing details modal provides another reference for compact close actions and fragment loading.

[![Profiling execution details before dashboard consistency changes](assets/dashboard-page-consistency/dialog-profiling-detail.png)](assets/dashboard-page-consistency/dialog-profiling-detail.png)

### Blob details and expiration editor

Wide dialog with a specific Save expiration action. Its modal root lacks a linked accessible title.

[![Blob details and expiration editor before dashboard consistency changes](assets/dashboard-page-consistency/dialog-blob-details.png)](assets/dashboard-page-consistency/dialog-blob-details.png)

### Blob upload

Default-width form and larger Cancel/Upload buttons contrast with the small detail-dialog actions. Nothing was uploaded.

[![Blob upload before dashboard consistency changes](assets/dashboard-page-consistency/dialog-blob-upload.png)](assets/dashboard-page-consistency/dialog-blob-upload.png)

### Blob delete confirmation

Larger Cancel/Delete buttons. The confirmation was dismissed without deletion.

[![Blob delete confirmation before dashboard consistency changes](assets/dashboard-page-consistency/dialog-blob-delete.png)](assets/dashboard-page-consistency/dialog-blob-delete.png)

### Create folder

Default-width mutation form with default-size actions. Nothing was created.

[![Create folder before dashboard consistency changes](assets/dashboard-page-consistency/dialog-file-create-folder.png)](assets/dashboard-page-consistency/dialog-file-create-folder.png)

### New document

Nearly full-height JSON editor with left-aligned Close and Save Changes. Nothing was saved.

[![New document before dashboard consistency changes](assets/dashboard-page-consistency/dialog-document-new.png)](assets/dashboard-page-consistency/dialog-document-new.png)

### Permalink details

Compact mixed detail/editor with Close and Save changes; its title is not linked to the modal root.

[![Permalink details before dashboard consistency changes](assets/dashboard-page-consistency/dialog-permalink-details.png)](assets/dashboard-page-consistency/dialog-permalink-details.png)

### Entities record details

Native dialog implementation. Shared appearance and behavior should work across both native and Bootstrap dialogs.

[![Entities record details before dashboard consistency changes](assets/dashboard-page-consistency/dialog-entities-detail.png)](assets/dashboard-page-consistency/dialog-entities-detail.png)

### Entities editor

Native editor with compact Cancel/OK. Replace the generic OK with an action-specific label. Nothing was created.

[![Entities editor before dashboard consistency changes](assets/dashboard-page-consistency/dialog-entities-editor.png)](assets/dashboard-page-consistency/dialog-entities-editor.png)

</details>

<details>
<summary>Phone layouts</summary>

### Documents page at 390 CSS pixels

Global header actions overlap the page toolbar. The clear and create actions also differ from the analytical form layout.

[![Documents page at 390 CSS pixels before dashboard consistency changes](assets/dashboard-page-consistency/documents-mobile.png)](assets/dashboard-page-consistency/documents-mobile.png)

### Operations page at 390 CSS pixels

The same header overlap appears above the wrapped routed tabs.

[![Operations page at 390 CSS pixels before dashboard consistency changes](assets/dashboard-page-consistency/operations-mobile.png)](assets/dashboard-page-consistency/operations-mobile.png)

### Blob details at 390 CSS pixels

Reference for body/footer fit and wrapping in an editor dialog.

[![Blob details at 390 CSS pixels before dashboard consistency changes](assets/dashboard-page-consistency/dialog-blob-details-mobile.png)](assets/dashboard-page-consistency/dialog-blob-details-mobile.png)

### Auditing details at 390 CSS pixels

A long execution body scrolls internally while the title and Close footer remain available.

[![Auditing details at 390 CSS pixels before dashboard consistency changes](assets/dashboard-page-consistency/dialog-auditing-detail-mobile.png)](assets/dashboard-page-consistency/dialog-auditing-detail-mobile.png)

</details>


## Related guidance

- [Dashboard feature guide](../features-presentation-dashboard.md).
- [Dashboard page skill](../../.agents/skills/dashboard-pages/SKILL.md).
- [Dashboard page patterns](../../.agents/skills/dashboard-pages/references/page-patterns.md).
- [Clean Architecture](../adr/0001-clean-onion-architecture.md).
- [Modular monolith architecture](../adr/0003-modular-monolith-architecture.md).
