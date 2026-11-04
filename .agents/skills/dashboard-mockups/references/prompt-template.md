# Dashboard wireframe prompt

Adapt this template to the feature. Include only the views and labels needed for the current request; do not copy an entire specification into an image prompt.

```text
Use case: ui-mockup.
Asset: a low-fidelity Dashboard concept for [feature/specification].
Purpose: explain content, tab navigation, scope, and supported operations.

Style: white paper, thin black hand-drawn outlines, simple readable labels,
slightly uneven rectangles, optional pale gray hatching for active tabs.
Abstract and schematic, not a realistic production screenshot.
No colored UI, gradients, glow, device frame, or decorative imagery.

Views:
- [Overview or list view, its exact tabs, active tab, and key content groups.]
- [Detail or operations view only if needed, with its exact tabs and scope.]

Exact labels: [canonical page/tab/entity/provider/action names].
States and scope: [which data is empty, unavailable, waiting, or selected].
Actions: [only controls supported by the feature contract].
Data: placeholder dashes or scribbles. Use short fictional identifiers only
where needed. Do not invent measured performance values or real application data.

Constraints: use the shared Dashboard hierarchy: compact title/breadcrumb,
refresh toolbar, routed page tabs, current-view content. Use simple filter
fields and bounded table placeholders, not raw JSON or an implementation essay.
Keep all labels readable and the selected context unambiguous.

Caption: "Wireframe concept - illustrative placeholders".
```

## Diagnostics views

For a requested Diagnostics tab, include the exact tab bar with Diagnostics active, selected entity/collection scope, time range, and refresh controls. Use a small grid of labeled chart placeholders. Name units and legends explicitly, such as operations/s, entries/s, ms, bytes, or event counts, according to the feature's actual metrics. Keep different denominators and scopes distinct. Use schematic strokes and no numeric ticks by default. Mutation controls and benchmark execution do not belong in a read-only diagnostics sketch unless explicitly supported and requested.

When the overview and Diagnostics both need explanation, save a separate companion image rather than shrinking all charts into the overview.

## Revising an existing image

Identify the image as the edit target. State what changes and what must remain invariant. For a style correction, request preservation of the view inventory, exact tab names, active tabs, and supported operations while replacing the rendering style and numeric data.

Example revision intent:

```text
Restyle the supplied Dashboard concept as an abstract black-and-white wireframe.
Preserve the pages, exact tabs, selected contexts, supported operations, and
scope relationships. Replace precise metrics and identifiers with placeholders.
Remove dark panels, color, gradients, shadows, and screenshot-like detail.
Do not add capabilities or controls. Preserve [feature-specific invariants].
```

Inspect scope as well as spelling. Requests and nodes are different entities; a local operation cannot be illustrated as cross-node work unless that is the selected feature capability.
