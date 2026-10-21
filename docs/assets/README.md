# Brand assets

[bITDevKit_Icon.svg](bITDevKit_Icon.svg) is the original 3D icon.

The dashboard and documentation home page render its theme-aware geometry from [the shared dashboard logo](../../src/Presentation.Web/Pages/_DashboardLogo.cshtml), using info-colored blocks and a danger-colored accent.

[bITDevKit_Icon_flat.svg](bITDevKit_Icon_flat.svg) is the flat variant: four outlined blocks joined by a bridge. [View its dark and light preview](bITDevKit_Icon_flat_preview.png).

[bITDevKit_Icon_flat_alt.svg](bITDevKit_Icon_flat_alt.svg) is the alternate flat variant: two stacked pillars joined by a filled diamond. The pillar blocks are 7 × 7; the diamond is 10.5 × 10.5, rotated 45 degrees and raised over the pillar tops. [View its dark and light preview](bITDevKit_Icon_flat_alt_preview.png).

Both flat variants follow the [feature icon style](features/README.md), with transparent backgrounds, 32 × 32 view boxes, 1.6-unit strokes, and rounded ends and joins.

## Use the flat icons

Inline the SVG to inherit dashboard theme colors. The blocks use `--dashboard-info-text`; the bridge or diamond uses `--dashboard-primary-text`. Standalone images use the embedded Tokyo Night fallback colors.

For a decorative icon beside a label, remove the `<title>`, `role`, and `aria-labelledby`, then add `aria-hidden="true" focusable="false"`. For a named icon, retain its title and give the title ID a unique value for each instance.

Edit the SVG as vector geometry. Keep its proportions, transparent background, stroke weight, and theme tokens consistent with the feature icons.
