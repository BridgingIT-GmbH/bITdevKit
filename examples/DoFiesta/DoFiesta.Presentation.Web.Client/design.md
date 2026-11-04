# DoFiesta app design

Approved direction: Hallmark Wayfind, Option B, night only. Scope is the Blazor application. The DevKit identity provider and dashboard UI remain independent.

Use Chivo Mono at weights 400, 500 and 800. Shared CSS tokens live in wwwroot/styles/tokens.css; the Mud palette is composed in Layout/Themes.cs. Use blue-black backgrounds, neutral ink/rules, cobalt primary actions, and amber figures on dark surfaces. Semantic errors may use the theme error color.

Use the capped 1216px shell with horizontal navigation and a temporary mobile menu. Keep one header link to /_bdk/dashboard. Do not restore Operations or Streaming navigation.

The Todo screen uses a full-width dense MudTable with inline editing. Preserve the existing status, priority, due date/time, attachment, history, transfer and bulk actions. Narrow screens stack labeled rows and retain all controls.

Use square controls and surfaces, horizontal table rules, no card shadows or gradients. Include visible keyboard focus, disabled states and reduced-motion support. Dialogs, menus and date pickers share the system through the app stylesheet.

Homepage: static welcome, Get Started to /todos, explicitly illustrative task preview, existing feature guidance, lifecycle strip and hosted-demo QR. Never present the preview as personal or live data.
