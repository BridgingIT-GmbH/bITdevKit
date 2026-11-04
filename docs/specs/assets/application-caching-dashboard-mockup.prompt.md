# Application Caching Dashboard mockup prompt

The selected image uses the abstract black-and-white wireframe style in the final revision below. Earlier prompts are retained as generation history.

## Current navigation revision

The current overview image also includes Diagnostics in both cache and collection tab bars. Diagnostics has its own companion wireframe.

```text
Update only the tab inventories in this supplied black-and-white Dashboard wireframe. In the TOP cache overview tab bar, insert "Diagnostics" between "Shared Loads" and "Operations", yielding "Overview", "Collections", "Shared Loads", "Diagnostics", "Operations". In BOTH LOWER collection tab bars, insert "Diagnostics" between "Shared Loads" and "Operations", yielding "Overview", "Entries", "Shared Loads", "Diagnostics", "Operations", "Settings". Preserve the existing active tabs: Overview at the top, Shared Loads at lower left, Operations at lower right. Keep all existing three views, monochrome hand-drawn style, white paper, rough outlines, placeholder data, provider names, one-owner/waiting-requests semantics, Node-local scope, operations controls, and caption. Adjust tab spacing or text size slightly to fit clearly. Do not add a Diagnostics content panel to this image; it has its own separate companion image. No colors, realistic screenshot styling, real metrics, or new controls.
```

## Current style revision

```text
Restyle this exact Application Caching dashboard concept into an ABSTRACT, LOW-FIDELITY, BLACK-AND-WHITE UX WIREFRAME. Preserve the three-view idea and the important labels, but deliberately remove production UI realism.

Visual style: white paper, thin slightly uneven black pen or pencil lines, rough hand-drawn rectangular containers, a little light gray crosshatching for active tabs, generous blank space. Simple readable handwritten or informal sans-serif labels. No dark theme, no blue, no cyan, no colored status badges, no gradients, no glass, no glow, no realistic screenshot treatment, no device frame. This should look like an early design sketch made to discuss layout and behavior, not finished software.

Keep the upper full-width view titled "Application Caching" with tabs "Overview", "Collections", "Shared Loads", "Operations". Overview is active. Use a few simple outlined metric placeholders rather than specific numbers. Show a rough collections table with columns Collection and Provider and the three rows product-cards / In-memory, search-snapshots / Entity Framework, report-artifacts / Filesystem. Replace exact entry counts, memory quantities, percentages, timestamps and chart measurements with short placeholder dashes or scribbled lines. Replace the detailed chart with an abstract chart placeholder.

Keep two lower views. Lower-left breadcrumb "Collection: product-cards", tabs "Overview", "Entries", "Shared Loads", "Operations", "Settings", Shared Loads active. Show a simple "One source load" owner box and several "Waiting request" follower boxes or a tiny abstract table. A short label "Node-local" is allowed. These are requests in one process, never separate nodes. Do not show real-looking IDs, start times or exact performance measurements.

Lower-right breadcrumb "Collection: product-cards", the same five tabs with Operations active. Keep simple wireframe fields "Key prefix" and "Reason", outline buttons "Preview", "Invalidate", "Clear collection", and a small empty preview-results box. No specific matching-entry count. No raw JSON editor, no live provider switching, no cancel-load controls.

Bottom caption: "Wireframe concept - illustrative placeholders".
Use the term Collections everywhere. Keep all important tab names legible. Keep the provider names exactly In-memory, Entity Framework, Filesystem. Keep the whole composition schematic and unpolished; fewer details are better.
```

Generated with the built-in image-generation tool. This is a design concept with fictional data; the prompt is retained for future revisions.

```text
Use case: ui-mockup.
Asset type: one high-resolution PNG concept image for a backend caching feature specification.
Primary request: a polished, realistic bITdevKit Application Caching dashboard mockup showing routed tabs, collection insights, shared loading, and cache operations. This is a design concept with fictional illustrative data, not an implemented product screenshot or benchmark result.

Composition: one cohesive landscape concept board, approximately 2400 by 1800 pixels, with three clearly separated flat UI panels on a dark background. The top panel spans the full width and shows the cache overview. The lower left panel shows a collection's Shared Loads tab. The lower right panel shows that same collection's Operations tab. The bottom panels are focused content views, not duplicated complete navigation shells. Keep generous separation and readable text; no perspective, monitor, device frame, photography, or decorative 3D.

Visual style: match a compact existing developer Dashboard built with Bootstrap and RazorSlices. Charcoal page background, slate-blue bordered cards, pale lavender text, subdued cyan and blue accents, small corner radii, thin borders. Compact monospace typography, aligned tables, restrained status badges, thin line icons. The shared page title is small and useful, not a giant marketing heading. Active page tabs use blue nav-pills, not underlined tabs. Refresh toolbar above tabs, persistent nav outside the changing content. High contrast and crisp, readable English text.

Top panel:
Small title: "Application Caching"
Small badge: "Concept mockup"
Refresh toolbar at right: "Updated just now" and "Refresh 10s".
Show these exact tabs in this order: "Overview", "Collections", "Shared Loads", "Operations"; Overview is active.
Four compact summary cards:
"Stored entries" / "96,536"
"Hit rate" / "96.2%"
"Resident memory" / "82 MiB / 256 MiB"
"Shared loads" / "1 active · 7 waiting"
Below the cards, show a compact collections table with exactly three fictional rows and these headings: "Collection", "Provider", "Entries", "Stored", "Status".
Rows:
"product-cards" | "In-memory" | "84,256" | "44 MiB" | "Ready"
"search-snapshots" | "Entity Framework" | "12,000" | "2.4 GiB" | "Ready"
"report-artifacts" | "Filesystem" | "280" | "7.2 GiB" | "Ready".
A small cyan line chart titled "Cache activity" can sit beside the table. It is illustrative, not a benchmark comparison.

Lower left panel:
Breadcrumb: "Application Caching / product-cards"
Exact tabs: "Overview", "Entries", "Shared Loads", "Operations", "Settings". Shared Loads is active.
Heading: "Shared load"
Small labels: "Key: product:42" and "Scope: Node-local".
Three readable cards: "Source calls 1", "Joined callers 7", "Status Loading".
A small table shows one "Owner" row with state "Running", followed by three representative "Follower" rows with state "Waiting".
A discreet footnote: "One source operation. Followers await the result."
Do not add a cancel-load button or unsupported controls.

Lower right panel:
Breadcrumb: "Application Caching / product-cards"
The same five tabs with Operations active.
Heading: "Invalidate entries"
Friendly filter controls labeled "Key prefix" with value "product:" and "Reason" with value "Catalog updated".
Buttons: "Preview" and "Invalidate"; separate subdued red outline button: "Clear collection".
A result card: "Preview only", "1,248 matching entries", "Scope: product-cards".
Show a small neutral provider badge: "In-memory".
No raw JSON editor. No live configuration editing. No automatic mutation or confirmation checklist.

Across the bottom, a small clear caption: "Design concept · illustrative data".
Constraints: use the word "collections" throughout the UI. Spell all tab and provider labels exactly. Exactly three built-in provider types: In-memory, Entity Framework, Filesystem. No other product or application names. No production claims, benchmark rankings, trademark watermarks, invented features, gradients, neon glow, or oversized decorative headings.
```
## Targeted revision

```text
Edit only the Caller column in the lower-left Shared load table. Replace its four caller labels, which currently begin node-1, node-2, node-3 and node-4, with exactly request-001, request-002, request-003 and request-004 respectively. These are four requests in the SAME process and node, consistent with the unchanged Scope: Node-local label. Preserve Role values Owner and Follower, State values Running and Waiting, start times, all rows, and the One source operation footnote. Keep every other part of this dashboard concept identical: all three panels, layout, dimensions, color palette, typography, tab labels, active tabs, headings, provider names, collection names, numbers, form fields, chart, operation buttons, and illustrative-data caption. Do not change anything outside those four Caller cells.
```
