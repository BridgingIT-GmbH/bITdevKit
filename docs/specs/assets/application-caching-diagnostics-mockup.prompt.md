# Application Caching Diagnostics wireframe prompt

Generated with the built-in image-generation tool. All chart marks are illustrative placeholders rather than measurements.

```text
Use case: ui-mockup.
Create a SECOND, separate low-fidelity Dashboard wireframe for the Application Caching Diagnostics tab, matching an abstract black-and-white hand-drawn UX sketch. White paper, thin slightly uneven black pen/pencil outlines, light gray hatching, simple legible handwritten labels. No colors, gradients, glow, realistic application screenshot styling, devices, exact measurements, percentages or invented production values.

One complete focused content view, landscape canvas about 1440 by 1080. Compact title/breadcrumb "Application Caching / product-cards". A small read-only scope line "Provider: In-memory | Metrics scope: Node-local".
A refresh toolbar with outlined controls "Window: Last 15 min", "Refresh: 10s", and "Refresh".
Exact tab labels in order: "Overview", "Entries", "Shared Loads", "Diagnostics", "Operations", "Settings". Diagnostics is active, shown with light diagonal hatching.
Under tabs, a small heading "Diagnostics". A short note "Illustrative chart placeholders".

Six simple outlined chart cards in a readable two-column, three-row grid. Each has a clear title, rough axes without numeric ticks, schematic placeholder lines/bars, a short vertical-axis unit label, and a minimal legend:
1. "Hits & Misses" / unit "requests/s" / legend "Hits", "Misses".
2. "Entry throughput" / unit "entries/s" / legend "Reads", "Writes".
3. "Operation latency" / unit "ms" / legend "p50", "p95", "p99".
4. "Shared loads" / unit "events/s" / legend "Source calls", "Joined requests".
5. "Resident memory" / unit "MiB" / legend "Resident", "Transient". Do not conflate persisted storage with resident memory.
6. "Failures & Timeouts" / unit "events/s" / legend "Provider", "Source", "Wait timeout".

Keep the chart marks abstract and schematic, not precise real data trends. Use simple line style differences or hatching rather than color. All text must remain readable and correctly spelled. Use only the requested view; do not add mutation buttons, benchmark-run controls, raw JSON, live provider settings, or other tabs.
At the bottom, write "Wireframe concept - illustrative chart placeholders".
This is a concept sketch for a specification, not an implemented UI or performance evidence.
```
