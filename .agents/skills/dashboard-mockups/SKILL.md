---
name: dashboard-mockups
description: "Create or revise abstract black-and-white bITdevKit Dashboard mockups and wireframes from feature specifications. Use this when asked for Dashboard concepts, tab layouts, diagnostic chart mockups, mock images, or low-fidelity wireframes. Use dashboard-pages for implementing actual RazorSlice pages."
---

# Dashboard mockups

Create a design artifact that explains navigation, content, and operations. Default to a low-fidelity black-and-white sketch with placeholders. Follow explicit user choices when they request a different style or level of detail. A mockup does not establish an implemented API, measured performance, or a working page.

## Ground the design

1. Read the feature's current specification and the relevant parts of [dashboard-pages](../dashboard-pages/SKILL.md). Use its [source map](../dashboard-pages/references/source-map.md) and [page patterns](../dashboard-pages/references/page-patterns.md) when choosing a working reference.
2. Inspect the [Dashboard guide](../../../docs/features-presentation-dashboard.md), shared layout, and closest relevant page where needed. Screenshots show visual context; code/specification establishes capabilities, authorization, readiness, and operations.
3. Extract the required pages, routed tabs, selected context, table fields, states, and supported actions before drawing. Use canonical feature terminology and preserve the user's earlier decisions. Resolve routine layout details directly; ask only when a material requirement is genuinely ambiguous.

## Choose the artifact

- For an explicitly generated image, use the available image-generation skill/tool and follow its instructions. For changes to a generated raster, use its edit workflow with the existing image as the target. Do not substitute HTML/SVG and call it an AI-generated image.
- For an editable code-native wireframe, SVG or self-contained HTML is appropriate when it matches the request. Architecture and message-flow diagrams belong in Mermaid rather than a Dashboard screenshot mockup.
- If the requested generation capability is unavailable, explain the limitation and available alternatives; do not silently switch to a paid CLI or different generation service.
- Do not implement or deploy Dashboard pages, start an application, or mutate live operational data just to produce a concept. Use dashboard-pages when the user actually requests implementation.

## Default visual language

- White background, black pen/pencil outlines, slightly irregular rectangles, and optional light gray hatching. Use no decorative gradients, glow, realistic device chrome, or polished screenshot treatment.
- Keep labels readable and use short dashes, scribbles, or neutral blocks for values. Show only the views needed to explain the requested design. Avoid shrinking many screens into an unreadable contact sheet.
- Show active tabs with an outline or hatching. A simplified chart box can suggest placement without inventing a time series or performance result.
- Use fictional, self-contained examples. Do not copy repository example-application names, production identifiers, tenants, secrets, or personal data from reference screenshots into documentation mockups.
- If illustrative numbers are needed, label them clearly and keep totals, units, scope, and rows consistent. Prefer placeholders over plausible but unmeasured throughput, latency, or capacity claims.

## Preserve Dashboard semantics

- Reuse the shared layout hierarchy: a compact page title/breadcrumb, refresh toolbar, routed page-level tabs, then current-view content. Avoid a second oversized heading. Match the standard nav-pill structure conceptually even when the sketch is monochrome.
- Tabs represent addressable subpages and use the existing `DashboardPage.RememberedViews` conventions in the implementation design. Navigation sits outside refreshed fragments; selected views, filters, and paging survive read-only refresh.
- Use compact toolbars, cards, responsive tables, useful badges, and friendly domain-labeled filter fields. Keep raw filter JSON, implementation essays, and unnecessary verification checkboxes out of the normal flow.
- Draw only operations supported by the feature contract. Keep read-only settings distinct from editable configuration. Do not invent cancel/retry/edit buttons, live provider switching, or automatic destructive actions.
- Distinguish empty, unavailable, failed, unsupported, and not-yet-measured states. An unready provider is not a successful zero.
- When a Diagnostics view is requested or defined, use labeled chart placeholders with units, legends, time range, refresh controls, and explicit observation scope. Derive metrics from the feature contract; do not add fictitious measurements or represent process-local data as fleet-wide totals. Use a separate view when chart detail would make an overview unreadable.
- Preserve scope and ownership meanings. For example, a node-local shared operation can show several waiting requests, but must not show separate nodes sharing that operation unless cross-node coordination is actually supported and selected.

For raster generation or revision, adapt the [prompt template](references/prompt-template.md). Use exact required labels, a concise view inventory, and explicit invariants for edits.

For concrete style examples, read [visual references](references/visual-examples.md) and open the relevant bundled image with the available image-viewing tool. The overview example shows multi-view composition and scoped operations; the Diagnostics example shows chart layout. Reuse their sketch language and hierarchy while deriving labels, tabs, data, and capabilities from the current feature.

## Save, inspect, and deliver

1. Inspect every generated image for legible labels, active tabs, provider/capability names, operational scope, and invented features. Correct semantic errors before embedding it. Pixel edits to generated images follow the image tool's workflow.
2. Save project-bound assets in the workspace. For a specification, use its adjacent assets directory; keep concept images separate from verified runtime screenshot collections. Never reference an image only in the generator's private output directory.
3. Preserve existing assets unless replacement is requested. A style-revision request can update the consuming asset; retain the original generated source or use a versioned sibling. Keep prompts and revisions in a companion Markdown file when useful for reproduction.
4. Embed the selected asset with a relative Markdown path, useful alt text, and an explicit concept/illustrative-data caption. Check links and image existence. Do not describe a mockup as runtime verification or benchmark evidence.
5. Show the result using the active harness's supported image/visual rendering path, and report the saved asset, consuming document, prompt, and generation method. Use native preview/render tools when required for HTML. Do not start a preview server for a static artifact.

No additional approval is needed to generate or revise the already-requested mockup. Existing authorization and privacy boundaries still apply.
