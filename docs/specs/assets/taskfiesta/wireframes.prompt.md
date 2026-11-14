# TaskFiesta Wireframe Prompts

## CoreModule naming revision

Selected diagnostics asset: [preferences-diagnostics-v3.png](preferences-diagnostics-v3.png). This built-in image-generation edit changes only the displayed provider name; earlier prompt text below remains generation history.

```text
Edit only one value in the right-hand Presentation diagnostics drawer: change the value of Effective provider from Tasks module to CoreModule. Preserve every other pixel, text, form value, control, layout, canvas, monochrome sketch style, line and spacing. Keep Contribution as Tasks settings, since that is a feature label; the owning application module class is now CoreModule. Do not alter Live settings sections 2 or Active task views 0.
```

These conceptual assets accompany [Appendix A: TaskFiesta validation application](../../spec-blazor-presentation-framework.md#appendix-a-taskfiesta-validation-application). They were generated with the built-in `image_gen` tool. They are design illustrations, not runtime screenshots or executable evidence.

Current architecture/design clarification: TaskFiesta has one application module named `CoreModule`, logical identity `core`, following DoFiesta's convention. The encouragement panel mentioned as Motivation in the original generation prompts is a local component, not another module. Preferences likewise belongs to `CoreModule` while reusing framework components. Cross-module tests use a separate technical fixture host. The images retain their monochrome structural style; the implemented visual direction follows DoFiesta's Wayfind styling as specified in the appendix. Earlier prompts are retained verbatim as generation history; the selected diagnostics revision uses the current module name.

The requested direction matches the existing ShopFiesta specification wireframes: monochrome paper, readable dark text, thin hand-drawn outlines, grayscale panels and light hatching. One screen per asset. TaskFiesta is a small task/list application developed alongside the complete presentation framework; implementation phases belong in a later plan. Public list sharing is read-only and its URLs below are fictional fixtures.

## public-board

Asset: [public-board.png](public-board.png).

```text
Use case: ui-mockup. Create a single landscape desktop application wireframe in the established TaskFiesta/ShopFiesta documentation style: white paper, grayscale panels, crisp large dark text, thin slightly hand-drawn black outlines and subtle diagonal pencil hatching. No color, photography, gradients or decorative shadows. One complete readable screen, not a collage. TaskFiesta is a playful task/to-do validation app, not an e-commerce shop. No implementation jargon inside the product UI. All text must be legible and spelled correctly. Keep wide margins so nothing clips. Screen: public website home/task board. Header TaskFiesta wordmark, navigation Public board, My tasks, Lists, Sign in. Breadcrumb Home > Public board. Main heading Tiny victories. A short plain line Small tasks, questionable importance. GET-style filter row Search tasks [empty text field], Status [Ready], Sort [Title], and Apply filters. Three clean task cards: Polish the invisible trophy, list Office mysteries, status Ready, link View task; Teach the rubber duck to take minutes, list Office mysteries, status Ready, link View task; Water the office cactus politely, list Tiny victories, status Ready, link View task. Below cards show Page 1 of 1 and disabled Previous / Next. A clearly separated modest native form headed Suggest a task, with Task title text input, optional Note textarea, and Submit suggestion button. Footer TaskFiesta. This is normal server-delivered content; do not draw loading overlays or runtime labels.
```

## task-workbench

Asset: [task-workbench.png](task-workbench.png).

```text
Use case: ui-mockup. Create a single landscape desktop application wireframe in the established TaskFiesta/ShopFiesta documentation style: white paper, grayscale panels, crisp large dark text, thin slightly hand-drawn black outlines and subtle diagonal pencil hatching. No color, photography, gradients or decorative shadows. One complete readable screen, not a collage. TaskFiesta is a playful task/to-do validation app, not an e-commerce shop. No implementation jargon inside the product UI. All text must be legible and spelled correctly. Keep wide margins so nothing clips. Screen: signed-in task workspace. Header TaskFiesta, Tasks, Lists, Preferences, and account Demo editor. Left navigation All tasks, Office mysteries, Tiny victories, Compare tasks selected. Breadcrumb Tasks > Compare. Heading Compare tasks. External toolbar ABOVE and separate from two task panels: Active view selector Ready, button Complete (2), Move to list, Refresh, Export matching. Two equal task tables side by side labeled Ready and Needs review. Ready table columns checkbox, Task, Priority, Status. Selected row TF-101 Polish the invisible trophy / Normal / Ready; selected TF-102 Teach the rubber duck to take minutes / High / Ready; unselected TF-103 Water the office cactus politely / Low / Ready. Ready header checkbox indeterminate, count 2 selected. Right Needs review table independently selected row TF-201 Count the spare paperclips / Normal / Proposed, plus unselected TF-202 Label the mystery drawer / Low / Proposed; indeterminate header checkbox and 1 selected. Each panel has its own Status filter, Search field and Page 1 of 2 navigation. Show native local three-dot action menus per panel. The header Complete (2) clearly corresponds to Ready, never Complete (3). Footer status Ready: 2 selected. Needs review: 1 selected. No developer annotations.
```

## task-detail

Asset: [task-detail.png](task-detail.png).

```text
Use case: ui-mockup. Create a single landscape desktop application wireframe in the established TaskFiesta/ShopFiesta documentation style: white paper, grayscale panels, crisp large dark text, thin slightly hand-drawn black outlines and subtle diagonal pencil hatching. No color, photography, gradients or decorative shadows. One complete readable screen, not a collage. TaskFiesta is a playful task/to-do validation app, not an e-commerce shop. No implementation jargon inside the product UI. All text must be legible and spelled correctly. Keep wide margins so nothing clips. Screen: TaskFiesta signed-in task detail editor. Header TaskFiesta, Tasks, Lists, Preferences, Demo editor. Sidebar All tasks, Office mysteries selected, Tiny victories. Breadcrumb Tasks > Office mysteries > TF-102. Heading Edit task TF-102. Tabs Overview selected, History. Local toolbar Save, Cancel, Complete. A compact unsaved changes label. Form fields: Task title with an EMPTY input and visible inline validation Task title is required. List dropdown Office mysteries. Status dropdown Ready. Priority dropdown High. Due date input 18 Nov 2026. Notes multiline field Bring a notebook for the duck. A validation summary repeats Task title is required. A slim sidebar Summary panel lists task TF-102, list Office mysteries, last saved title Teach the rubber duck to take minutes. Under the summary show a small optional Motivation panel heading Small encouragement with text One tiny victory at a time and a Show encouragement button. The task title required field must actually be empty. Keep the last saved title only in the summary. This screen illustrates native forms, revision-safe feedback, edit tabs and a cross-module region, not a commerce flow.
```

## preferences-diagnostics

Asset: [preferences-diagnostics-v3.png](preferences-diagnostics-v3.png).

```text
Use case: ui-mockup. Create a single landscape desktop application wireframe in the established TaskFiesta/ShopFiesta documentation style: white paper, grayscale panels, crisp large dark text, thin slightly hand-drawn black outlines and subtle diagonal pencil hatching. No color, photography, gradients or decorative shadows. One complete readable screen, not a collage. TaskFiesta is a playful task/to-do validation app, not an e-commerce shop. No implementation jargon inside the product UI. All text must be legible and spelled correctly. Keep wide margins so nothing clips. Screen: TaskFiesta Preferences with a diagnostics drawer visibly attached to the same page. Header TaskFiesta, Tasks, Lists, Preferences selected, Demo editor. Breadcrumb Home > Preferences. Heading Preferences. Main content left two thirds: section Appearance and language, Theme dropdown System, Language dropdown English (UK), button Save appearance; independent section Task display, Rows per page dropdown 25, Compact rows checked, button Save task display. Right third is a clearly separated developer drawer headed Presentation diagnostics with close X and Open popout link. Read-only fields: Profile Connected; Runtime Server; Active owner Ready task view; Contribution Motivation; Effective provider Host replacement; State Idle; Capture Off. Below small rows for Ready task view: 2 selected, Needs review: 1 selected. Drawer has a compact Live scopes tab selected and Contributions tab. Bottom drawer muted label Read-only inspection. No button implies that inspection runs tasks. Clear ownership and independent save sections. Grow the canvas if necessary, do not crowd text.
```

## lists-sharing

Asset: [lists-sharing-v2.png](lists-sharing-v2.png).

```text
Use case ui-mockup. Generate one landscape desktop TaskFiesta app wireframe, matching monochrome ShopFiesta documentation sketches: white paper, black crisp readable text, grayscale panels, thin hand-drawn outlines, light diagonal hatching, no colors, no photography or gradients. Single complete screen. Header TaskFiesta, Tasks, Lists selected, Preferences, Demo editor. Sidebar All tasks, Office mysteries, Tiny victories, Inbox. Breadcrumb Tasks > Lists. Main heading My lists with Create list button. Table columns List, Tasks, Sharing, Actions. Rows: Office mysteries / 2 / Link enabled / Rename, Share, Delete disabled; Tiny victories / 1 / Private / Rename, Share, Delete disabled; Inbox / 2 / Private / Rename, Share, Delete disabled; Weekend nonsense / 0 / Private / Rename, Share, Delete. Office mysteries row selected. Under table small clear note Move tasks before deleting a non-empty list. Show right side panel heading Share Office mysteries with read-only link access label Anyone with this link can view this list. URL field https://taskfiesta.example/shared/lists/demo-7F2Q and Copy link button. Include Revoke link button. Below show a short preview with Polish the invisible trophy and Teach the rubber duck to take minutes, both Ready. In preview no editing controls. A small separate inline Create list form below the main table contains Name empty field, Create and Cancel buttons. Keep all texts legible, no code or framework jargon. This is list CRUD and read-only sharing, not commerce.
```


## Selected refinements

The selected list-sharing revision uses plain read-only bullets instead of checked controls. The diagnostics revision inspects only the mounted Preferences page and shows zero active task views, preserving owner cleanup semantics. Original generated variants remain outside the documentation tree.

### lists-sharing revision

Selected asset: [lists-sharing-v2.png](lists-sharing-v2.png).

```text
Edit only the two small checked checkbox icons in the right-side Preview (read-only) task rows. Replace those two checked boxes with simple small neutral round bullet marks, with no checkbox outline and no tick. These are Ready tasks in a read-only projection, not completed tasks or editable checkboxes. Preserve all other text, URL, numbers, row statuses, layout, controls, white/grayscale hand-drawn sketch style, and canvas exactly.
```

### preferences-diagnostics revision

Selected asset: [preferences-diagnostics-v3.png](preferences-diagnostics-v3.png).

```text
Correct only the right-side diagnostics drawer data so it describes the currently mounted Preferences page, not disposed task views. Keep the left preference forms, every existing save button/value, header, layout, canvas, monochrome sketch style, drawer title/tabs/Open popout/close unchanged. In drawer use rows: Profile Connected; Runtime Server; Active owner Preferences page; Contribution Tasks settings; Effective provider Tasks module; State Idle; Capture Off. Replace the two lower rows that currently say Ready task view / 2 selected and Needs review / 1 selected with Live settings sections / 2 and Active task views / 0. Keep the Read-only inspection footer. All text must remain crisp, large and legible; do not add more rows or editing controls.
```