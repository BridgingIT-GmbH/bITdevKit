# Phase 10 dashboard evidence

The focused test logs and `result.json` record the passing source and browser checks. Screenshots show desktop, mobile, error retention and operation/Runtime correlation views.

The browser fixture uses the real DI services, recorder, memory provider, query facade, routes and Razor pages. Runtime samples are synthetic fixture values tied to the fixture's actual process identity. This proves rendering and correlation/navigation behavior, not runtime sampler accuracy or load capacity. Existing Runtime sampler tests remain required.

To repeat the browser check, copy `BrowserHost.cs.txt` to `/tmp/bitdevkit-profiling-browser/Program.cs` and `BrowserHost.csproj.txt` to `BrowserHost.csproj` in the same directory. Adjust the project reference to this checkout's absolute `src/Presentation.Web/Presentation.Web.csproj` path. Run `dotnet run --project /tmp/bitdevkit-profiling-browser/BrowserHost.csproj --urls http://localhost:5243`. Copy the JavaScript into a writable temporary directory, then run `TARGET_URL=http://localhost:5243 node run.js /absolute/path/playwright-test-profiling.js` from `.agents/skills/playwright-skill`. Install its npm dependencies and Chromium as described by the skill. The fixture binds localhost and allows anonymous dashboard access only for this isolated test.

`/.tmp` is not writable by the local user on this host; the script was executed from `/tmp` using the skill's absolute-path runner. The script measures browser request lifetimes, including cancellation, and recorded a maximum of one active content request. It does not claim that a remote provider which ignores cancellation stops immediately.
