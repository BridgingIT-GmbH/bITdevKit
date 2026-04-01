# AGENTS.md

Use the [`revealjs` skill](/mnt/f/projects/bit/bIT.bITdevKit/.agents/skills/revealjs/SKILL.md) for presentation work in this folder.

## Presentation Rules

- Create decks as `docs/presentations/features-<topic>.html`
- Reuse `docs/presentations/styles.css`; do not create a one-off theme unless truly necessary
- Match the existing decks in structure and tone: clear, practical, feature-centered, developer-usage focused, no marketing language
- Use Reveal.js with `RevealMarkdown`, `RevealHighlight`, and `RevealMenu`
- Use Monokai for code highlighting
- Keep the left-side menu enabled with title-based entries
- Use realistic C# examples with `language-csharp` where code helps explain the feature
- Include a concrete flow slide when the feature has a runtime flow
- Cross-reference only the most relevant docs
- Update `docs/presentations/index.html` whenever a new deck is added

## Style Notes

- Keep the shared dark mono presentation look from `styles.css`
- Visual direction: minimal brutalist mono
- In this presentation context, brutalist means hard-edged, flat, dense, technical, and intentionally plain rather than polished or decorative
- Typography: IBM Plex Mono throughout, uppercase headings, strong hierarchy, no decorative font mixing
- Surfaces: dark background, slightly lighter panels, hard borders, no rounded corners, no soft shadows
- Color use: restrained accents for feature categories and emphasis, not decorative color washes
- Layout: structured grids, boxed panels, code windows, flow rows, and clear section dividers
- Prefer the existing layout classes and presentation patterns already used in this folder
- It does not need to be exactly 20 slides; add slides when clarity benefits
