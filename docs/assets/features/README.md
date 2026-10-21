# Feature icons

[Open the interactive catalog](index.html).

The collection contains 200 standalone SVGs across 11 families: 55 features and 145 subfeatures. The inventory covers all 60 published feature, common, and testing guides in `mkdocs.yml`. Each entry in [catalog.json](catalog.json) records its family, parent, theme role, and source guides.

The approved Messaging, Jobs, Queueing, Requester, and Notifier icons retain their original geometry. Built-in dashboard navigation uses these canonical SVGs.

## Design

- 32 × 32 view box, transparent background, 1.6-unit strokes, rounded ends and joins.
- Two semantic colors per icon. Storage, Jobs, and Orchestrations use the dashboard analysis role. Messaging, Queueing, Broadcasting, Metrics, and Profiling use info. Shared and host capabilities use primary.
- Subfeatures share contours with their parent where that helps recognition. Their silhouettes or internal marks distinguish their purpose.
- Geometry remains readable in monochrome. Provider icons represent capabilities rather than vendor trademarks.
- The catalog supports all 15 dashboard palettes, search, family and hierarchy filters, monochrome rendering, and size previews from 16 to 64 pixels.

## Agent instructions

Follow this style when adding or revising icons. Read the feature's documentation and inspect its nearest relatives in [the catalog](index.html) before drawing.

### Choose a meaningful shape

Use one concrete symbol for the capability and, when needed, one small modifier for its distinguishing behavior. Keep enough empty space for both to read at 16 pixels. Remove details that disappear at that size.

These approved icons define the visual direction:

- [Messaging](messaging.svg) combines an envelope with a branching delivery path.
- [Jobs](jobs.svg) combines a calendar with an execution badge.
- [Queueing](queueing.svg) combines stacked work items with one dispatch arrow.
- [Requester](requester.svg) shows a request entering one handler and a response returning.
- [Notifier](notifier.svg) shows one publisher sending to multiple handlers.
- [MCP](mcp.svg) uses a robot head with a small antenna, two eyes, and a simple mouth.

Represent the actual feature behavior. For example, Notifier fan-out and Requester request-response need different geometry. A color change alone does not distinguish two icons. Preserve the approved icons when extending the collection.

For a subfeature, reuse a recognizable contour or visual motif from its parent and add a distinct mark. Keep that mark simple enough to survive monochrome rendering. Set the existing parent's ID in `catalog.json`; the parent can belong to another family, as provider implementations do.

### Keep geometry consistent

- Use `width="32" height="32" viewBox="0 0 32 32"` on the standalone SVG.
- Use `fill="none"` on the root and `stroke-width="1.6"`, `stroke-linecap="round"`, and `stroke-linejoin="round"` on each drawing group.
- Keep most geometry within coordinates 3 to 29. Account for the stroke's 0.8-unit extension when placing shapes near an edge. No painted part may extend beyond the view box.
- Use simple paths, rectangles, circles, ellipses, and polygons. Start with a few shapes and add detail only when it communicates a different capability.
- Match neighboring icons' apparent size and visual weight. Center the visible drawing rather than its number of SVG elements.
- Let strokes scale with the icon. Avoid `vector-effect="non-scaling-stroke"` and transforms that change the apparent stroke width.
- Use a small solid fill only for a meaningful accent, such as the Jobs play triangle. Keep the surrounding icon outlined.
- Keep the background transparent. Exclude gradients, shadows, textures, decorative frames, animation, text, and embedded raster images.
- Draw editable SVG geometry directly. Do not rasterize an icon or convert a generated bitmap into paths.

### Use dashboard theme tokens

Use two semantic roles, with these standalone fallback values:

- Info: `var(--dashboard-info-text, #62adb8)`.
- Analysis: `var(--dashboard-analysis-text, #b295e8)`.
- Primary: `var(--dashboard-primary-text, #7da4f7)`.

Info and analysis families use primary for the accent. Primary families use info for the accent. Follow an existing related icon when its established arrangement differs. Set the catalog's `tone` to the family role.

The SVG's `stroke` and `fill` attributes determine its colors. Changing `tone` in the catalog does not recolor the drawing. Use the contrast-adjusted `--dashboard-*-text` tokens rather than raw palette colors. Keep fallback colors inside `var(...)`; theme inheritance must remain active.

Read the current token definitions in [the dashboard theme source](../../../src/Presentation.Web/Pages/_DashboardTheme.cshtml) when changing the color system. If its default colors change, derive new fallbacks from that source rather than choosing unrelated hex values.

### Keep assets independent and accessible

Each standalone SVG must include `xmlns="http://www.w3.org/2000/svg"`, `role="img"`, and `aria-labelledby="{id}-title"`. Include `<title id="{id}-title">Display name</title>` and match the catalog title. Use a lowercase kebab-case ID equal to the filename without `.svg`.

Keep every drawing self-contained. Exclude scripts, event handlers, external references, embedded stylesheets, fonts, and `foreignObject`. Put stroke styling on SVG groups so the sprite generator can preserve it. The integration examples below explain how to adapt accessibility attributes when an icon is decorative or repeated.

## Use an icon

Each SVG is independent. For a theme-aware icon, inline its markup so its CSS variables inherit from the dashboard. Standalone image embeds use the SVG's Tokyo Night fallback colors.

The body and accent use `--dashboard-info-text`, `--dashboard-primary-text`, or `--dashboard-analysis-text`. These are the same contrast-adjusted tokens used by the dashboard. No background fill or animation is embedded.

For decorative inline icons beside a text label, replace the root's `role` and `aria-labelledby` attributes with `aria-hidden="true" focusable="false"`, and remove its `<title>`. For an icon that supplies its own accessible name, retain the title and give its ID a unique value for each instance.

## Dashboard navigation

The dashboard embeds the SVGs it needs in `Presentation.Web`, then renders them inline so their colors follow the selected theme. Built-in pages resolve icons by their stable `DashboardPage.Key` through [DashboardFeatureIcons.cs](../../../src/Presentation.Web/Dashboard/DashboardFeatureIcons.cs). Custom pages and legacy navigation items retain their Bootstrap icon fallback.

When adding a built-in navigation icon, add its SVG to the embedded resources in [Presentation.Web.csproj](../../../src/Presentation.Web/Presentation.Web.csproj), add the page-key mapping in `DashboardFeatureIcons`, and extend `DashboardFeatureIconTests`. Rebuild the application after changing a canonical SVG. The dashboard removes standalone titles and marks navigation icons as decorative; the link supplies the accessible name.

## Use the sprite

[features.svg](features.svg) contains one symbol per icon, named `feature-{id}`. Serve the sprite from the same origin as the page. This decorative example uses the label beside it as the accessible name:

```html
<svg width="24" height="24" viewBox="0 0 32 32" aria-hidden="true" focusable="false">
  <use href="/assets/features/features.svg#feature-storage-documents"></use>
</svg>
<span>Document storage</span>
```

For a named icon, add a unique title to the containing SVG:

```html
<svg width="32" height="32" viewBox="0 0 32 32" role="img" aria-labelledby="storage-title">
  <title id="storage-title">Document storage</title>
  <use href="/assets/features/features.svg#feature-storage-documents"></use>
</svg>
```

The gallery embeds its sprite and metadata, so `index.html` can also be opened directly from disk.

## Edit and regenerate

The individual SVG files and `catalog.json` are the editable sources. Add an icon to both, keeping its ID equal to its filename without `.svg`. Use `parent` for a subfeature and include source-guide paths relative to the repository root.

To add an icon:

1. Read the owning feature guide and inspect the parent and sibling SVGs.
2. Create `{id}.svg` using the style instructions above.
3. Add its entry to the `icons` array in `catalog.json`. Supply `id`, `title`, an existing `group`, `parent`, `sources`, and `tone`. Use `null` for a top-level parent. Reference documentation that describes the implemented capability.
4. Run the generator and its consistency check below.
5. Review the new icon beside its relatives in the gallery, then complete the visual checks below.

For example, the existing upload icon has this catalog entry:

```json
{
	"id": "storage-uploads",
	"title": "Storage uploads",
	"group": "Storage",
	"parent": "storage",
	"sources": ["docs/features-storage-blobs.md"],
	"tone": "analysis"
}
```

Edit the standalone SVG and catalog entry. Rebuild `features.svg` and `index.html` with the generator instead of editing those generated files directly. Update the collection counts in this README when adding or removing entries.

Run from the repository root with Node.js:

```sh
node docs/assets/features/build-catalog.mjs
```

The generator rebuilds `features.svg` and `index.html`. It derives theme tokens using the palette definitions and contrast functions in `src/Presentation.Web/Pages/_DashboardTheme.cshtml`. Regenerate after dashboard palette changes.

To check that the generated files match their sources:

```sh
node docs/assets/features/build-catalog.mjs --check
```

Generation checks unique IDs, parent references, source-guide existence, published-guide coverage, and SVG content restrictions. Visual review remains necessary after changing geometry. The five original `*-preview.html` studies are preserved separately from the generated catalog.

### Validate a new or changed icon

- Parse the standalone file as XML and confirm its title, view box, and stroke attributes.
- Check the actual painted bounds, including strokes. SVG path control points alone do not establish the visible bounds.
- Inspect 16, 20, 24, 32, 48, and 64 pixel sizes. Look for merged strokes, cramped modifiers, clipped arrowheads, and unequal visual weight beside sibling icons.
- Switch through all dashboard themes. Check light palettes and monochrome explicitly; the shape must remain identifiable without its two colors.
- Confirm the icon appears under the intended family and hierarchy filter. Open its detail dialog and verify its SVG download and source links.
- Check that the sprite renders the same geometry as the standalone SVG and that the gallery has no browser errors.

The generator's `--check` flag verifies generated-file consistency. It does not replace XML parsing, geometry checks, or visual review. Icon-only changes need these focused asset checks rather than a .NET build.

## Use icons in documentation

Keep a guide's first Markdown heading plain so MCP documentation search can read its title. Place marked, decorative images immediately before that heading, with one blank line between them:

```markdown
<img class="bdk-doc-icon" src="assets/features/messaging.svg" width="32" height="32" alt="" align="right">

# Messaging
```

GitHub displays the image beside the title. The [MkDocs hook](../../site/hooks/feature_icons.py) moves it into the heading and inlines the SVG. The same title icons appear in the primary navigation. Use two images for a combined guide, such as Requester and Notifier.

For a subfeature heading, put a 24-pixel marked image before its text on the same line. Use 20-pixel images in feature index lists. Preserve the existing heading text and document structure.

The hook stages SVGs and catalog metadata into the ignored `docs/site/assets/features/` directory. It also builds the native [Feature Icons page](../../site/feature-icons.md) from `catalog.json`. Edit the canonical files in this folder; do not edit staged assets or imported `docs/site/reference/` files.

The [documentation stylesheet](../../site/assets/stylesheets/feature-icons.css) maps the SVG tokens to `--bdk-primary`, `--bdk-info`, and `--bdk-secondary`. Reuse these mappings when extending the site.

After editing source guides, run the repository's documentation sync before building MkDocs:

```powershell
pwsh -NoProfile -File docs/site/scripts/sync-docs.ps1
```

Check a guide title, its section anchors, primary navigation, and the icon catalog in both light and dark themes. Test an instant-navigation transition as well as a direct page load. Keep source Markdown titles free of SVG or image markup.
