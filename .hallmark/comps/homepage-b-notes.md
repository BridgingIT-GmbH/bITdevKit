# Homepage mock using approved variant B

Status: homepage composition for review, application files unchanged. The user selected variant B for the redesign.

Source: examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/Home.razor.

Keeps B's dark palette, Chivo Mono, top navigation, square borders, cobalt actions and amber dates. The desktop navigation highlights Home; mobile uses the menu button.

Proposes replacing the automatic welcome carousel with a static welcome section. The existing welcome heading and Get Started destination (/todos) stay. The existing organizing sentence becomes the introductory text. The three feature descriptions remain verbatim in flat ruled rows. Track Progress and the actual New/InProgress/Completed statuses remain in a lifecycle strip. The original 100% carousel graphic is treated as an illustration, not a user statistic, and replaced by the status sequence.

The example task preview uses the same explicitly illustrative tasks as the Todo mocks; it is a static product illustration, not a proposal to load personal tasks on the public homepage.

Getting Started retains the existing hosted demo and QR material. The mock removes the accidental trailing greater-than character from the link's href. The original QR SVG is reused with a viewBox so it scales rather than crops. Its destination was not decoded or independently verified.

Achievable with MudAppBar, MudGrid, MudText, MudButton, MudTable or simple rows, MudDivider and the existing QR SVG. Proposed application files for later implementation: client Pages/Home.razor and its new scoped stylesheet, plus the previously approved shared B shell/theme files. No runtime changes made.

Validation: desktop render at 1280px and mobile at 390px, inspected PNGs and corrected QR scaling. Static Hallmark check: zero FAIL, zero REFLEX, zero WARN. The requested linter render tier could not run because puppeteer-core is absent; HTML preview produced the inspected images. No application build or functional tests run for the mock.
