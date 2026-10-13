---
name: update-packages
description: Update all NuGet packages and dotnet tools in the bITdevKit repo to the latest stable versions, then restore, build, test, fix breakages and check for vulnerabilities. Use this when asked to update, upgrade or bump packages, dependencies or tools, fix NU1109/NU1103 restore errors, or check for new CVEs after an update.
---

# Update packages

Repeatable process for upgrading the repo's dependencies (Central Package Management in `Directory.Packages.props`, local tools in `.config/dotnet-tools.json`). Updating is easy. Most of the work is finding what must be held back and proving the result still works.

## Rules

- Latest stable only. No previews, unless a package has no stable release at all (then keep the newest pre-release on the same release train as its siblings, e.g. the OpenTelemetry Prometheus exporter).
- Keep version constraints intact: bracketed pins (`[3.18.1]`) and ranges (`[6.8.1,7.0.0)`) in `Directory.Packages.props` must not change. Check the diff for them at the end.
- Use `dotnet outdated` and the dotnet CLI for the bulk upgrade. Direct edits of `Directory.Packages.props` are only for things the tool can't do: aligning a package family and the transitive pin group.
- Run restore, build and test commands one at a time in the same worktree (parallel top-level runs give misleading `obj/ref` errors).
- Never use `git stash` as a probe. Don't commit unless asked. `packages.lock.json` files change on restore, that is expected.
- Every package held back or pinned needs a one-line comment saying why.

## Process

### 1. Preflight

- `git status --short` to see what is already modified.
- `dotnet tool restore`, then `dotnet outdated --version` (the `dotnet-outdated-tool` is in the local manifest).
- If `dotnet restore` already fails (typically NU1109 downgrades), fix that first (step 3), otherwise `dotnet outdated` can't evaluate anything.

### 2. Bulk upgrade

```bash
dotnet outdated bITdevKit.slnx --no-restore            # review first
dotnet outdated bITdevKit.slnx --no-restore -u         # apply
git diff Directory.Packages.props                      # confirm bracketed/range pins are untouched
```

`dotnet outdated` only sees packages that some project references. Entries that exist only as central versions (for example transitive pins or packages kept for later) are skipped, so check them separately against NuGet:

```bash
curl -s https://api.nuget.org/v3-flatcontainer/<id-lowercase>/index.json | python3 -c "import sys,json; print([v for v in json.load(sys.stdin)['versions'] if '-' not in v][-4:])"
```

### 3. Restore and fix conflicts

```bash
dotnet restore 2>&1 | grep -oE "(error|warning) NU[0-9]+: .{0,200}" | sort | uniq -c | sort -rn
```

- **NU1109 (downgrade).** A direct or pinned version is lower than what a dependency needs. Raise the central version, usually in the `upgrade some packages due to transitive vulnerabilities` group at the bottom (for example `MimeKit` behind `MailKit`, `OpenTelemetry.Api` behind the OpenTelemetry packages). Read `dotnet nuget why <csproj> <package>` to see who pulls it.
- **NU1103 (version not found).** A servicing release can be only partly published or not yet indexed. Align the whole Microsoft 10.0.x family (AspNetCore, EF Core, `Microsoft.Extensions.*`, `System.*`) on one patch version. Before bumping, verify every package in the family has that version on NuGet. If some are missing, use the previous patch version and retry later.
- **NU1608 (outside dependency constraint).** A major bump violates an upper bound of another package. Example: `Microsoft.OpenApi` 3.x vs `Microsoft.AspNetCore.OpenApi` 10.x which requires below 3.0. Hold back and comment.
- Look at the nuspec to confirm a constraint before holding something back: `curl -s https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec | grep -oE '<dependency id="[^"]*"[^>]*>'`

### 4. Build

```bash
dotnet build --no-restore
```

- A restore that succeeds does not prove the packages work together. Runtime type-load problems can show up at build time, because `Microsoft.Extensions.ApiDescription.Server` loads the web assemblies to generate `openapi.json`. Seen once: `TypeLoadException` for `System.ClientModel.AsyncStreamingClientResult` because `System.ClientModel` 1.16 (via `Azure.Core` 1.63) removed a type that `OpenAI` 2.14 needs. The fix was holding back `Azure.Core`, `Azure.Monitor.OpenTelemetry.Exporter` and `Microsoft.ApplicationInsights.AspNetCore`. Re-check this hold-back each time: if `OpenAI` now targets the newer `System.ClientModel`, upgrade them again.
- API breaks from major bumps: fix the source, not the package, unless the package version is the problem (see step 5).

### 5. Deprecated and unlisted versions

```bash
dotnet list bITdevKit.slnx package --deprecated
```

If a "latest" version in `Directory.Packages.props` is not on the NuGet versions tab, it may be unlisted. Open `https://www.nuget.org/packages/<id>/<version>` and read the deprecation note. Seen once: `SharpCompress` 1.0.0 was an owner-deprecated build mistake, the real latest was 0.50.5. Move back to the newest listed version. Find the commit that adapted the code to the bad version with `git log -S'<id>" Version="<bad>"' -- Directory.Packages.props` and reverse the API changes by hand (the old patch may not apply cleanly). `xunit` 2.x is reported as legacy (replaced by `xunit.v3`), that is known and not a CVE.

### 6. Vulnerabilities

```bash
dotnet list bITdevKit.slnx package --vulnerable --include-transitive 2>&1 | grep -vE "has no vulnerable packages"
```

Empty output means clean. If something shows up, raise it through the transitive pin group (with a comment naming the advisory), then rebuild.

### 7. Tests

```bash
dotnet test bITdevKit.slnx --no-build --filter "FullyQualifiedName~UnitTests"
```

- Re-run any failing test alone. If it passes alone it is flaky, say so in the report, don't chase it.
- Run the integration tests for areas touched by a bump or code fix (for example the in-memory and local file storage tests for compression changes). Integration tests that need Docker (Testcontainers) may not be runnable, say which ones were skipped.

### 8. Dotnet tools

`dotnet outdated` does not cover `.config/dotnet-tools.json`. Compare each tool's current version with NuGet (same flat-container call as above, keyed by tool package id), then:

```bash
dotnet tool update <package-id>      # per outdated tool, updates the local manifest
dotnet tool restore
```

Keep tools aligned with the packages they pair with: `dotnet-ef` with the EF Core version, `minver-cli` with `MinVer`, `kiota` with `Microsoft.Kiota.Bundle`, `microsoft.dotnet-openapi` with the Microsoft 10.0.x family.

### 9. Kiota client after a tool bump

The DoFiesta build regenerates the Kiota client, so a new `kiota` version changes the generated models and request builders under `examples/DoFiesta/DoFiesta.Presentation.Web.Client/ApiClient/`. Build the whole solution again after the tool update and fix call sites that use changed types (for example a nested model type that became a plain enum). Review the generated diff before committing.

### 10. Final check and report

- `dotnet restore` shows no errors, `dotnet build bITdevKit.slnx` shows 0 errors and 0 warnings, tests pass, vulnerability scan is empty.
- `git diff Directory.Packages.props` still shows the bracketed and range pins unchanged.
- Report briefly: what moved, what is held back and why (these reasons also live as comments in `Directory.Packages.props`), which tests were flaky or skipped, and which generated files changed.

## Common pitfalls

- Bumping every `10.0.x` package to the newest patch without checking that each exists. A partial release breaks restore.
- Trusting a green restore. Build, and run tests, before declaring success.
- Assuming "latest on the NuGet API" equals "latest listed". Unlisted or deprecated versions still resolve.
- Forgetting packages with no project reference, they never show up in `dotnet outdated`.
- Letting a tool update silently change generated code. Rebuild after tool bumps.
