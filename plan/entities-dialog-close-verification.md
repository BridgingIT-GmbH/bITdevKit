# Entity dialog close alignment

2026-11-04.

The shared dialog title rule resets margins, overriding the entity title's auto margin. The close button was therefore placed beside the title rather than at the far edge. The entity header now gives its direct close button an auto inline-start margin and prevents it shrinking. The rule is scoped to entity dialogs and covers detail, error and editor headers.

## Verification

Before the fix, the live detail header left 563 pixels between the close button and the right edge. The rendered title's computed right margin was zero.

## Captures

- [Before, supplied by the user](assets/entities-dialog-close/before-user.png)

`dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Web.Entities." --nologo /p:UseSharedCompilation=false`: 277 passed, zero failed. The affected Razor project compiled.

The shared T3 preview's snapshot step failed twice, including `Preview automation snapshot timed out after 15000ms.` The final visual check used the repo-local Playwright skill against the same sample URL.

- Inspected actual screenshots at 1440 x 1000 and 390 x 844.
- The close button is 8 pixels from the header's right edge in both layouts, matching its padding. It stays vertically centered, remains inside the viewport and does not shrink.
- Clicking the header X closes the detail dialog at both widths. No browser page errors or document overflow.
- [Desktop after](assets/entities-dialog-close/after-1440.png)
- [Phone after](assets/entities-dialog-close/after-390.png)
- [Measured browser checks](assets/entities-dialog-close/checks.json)

`git diff --check` passed for touched files. The owned preview and Playwright browser were closed. The existing sample server remains restored on HTTPS 5211 / HTTP 5210.
