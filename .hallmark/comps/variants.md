# Wayfind night variants

All three use the same palette, typography and illustrative tasks. Application code is unchanged.

- A: existing workbench proposal, b-departure-board.png.
- B: variant-b.png, full-width timetable with top navigation and denser rows. Implement with MudAppBar navigation, MudTable Dense, existing quick-add and Create Task dialog. Operations destinations remain available from their menu.
- C: variant-c.png, list with inline priority below each title and detailed creation in a right panel. Implement with MudGrid or a persistent MudDrawer and the existing create form fields/callbacks. The panel can close; on narrow screens it becomes a full-width form. Row attachments/history/delete move to MudMenu. The mock shows the creation panel open, not a task detail editor.

These are static composition proposals, not functional Blazor screens. Existing task fields and backend behavior remain in scope for preservation. B favors scanning/editing; C favors creating detailed tasks without covering the list.

Verification: rendered both at 1280px and inspected resulting PNGs. Static Hallmark check passes with zero FAIL and zero WARN. Its render tier is unavailable because puppeteer-core is absent; HTML preview produced the inspected images. No application build or functional tests apply at this design stage.
