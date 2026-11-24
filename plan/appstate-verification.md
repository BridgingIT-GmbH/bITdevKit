# AppState implementation verification

Implementation date: 2026-11-17. Source baseline: `2493219fee1576b98be8e672209d4d2e8d73a5fa`. Changes are uncommitted. The [implementation plan](pln-refactor-appstate-1.md) defines the requirements; the [feature guide](../docs/features-presentation-appstate.md) documents the supported APIs.

## Commands and environment

The validation machine uses Linux Omarchy, an Intel Core i5-7600K with four physical/logical cores, .NET SDK 10.0.404 and runtime 10.0.15. Its SDK is installed through mise. Set `DOTNET_ROOT=/home/vip32/.local/share/mise/dotnet-root` and put that directory first in PATH so child processes find the same runtime. Builds, tests, package operations and performance runs execute sequentially.

- `dotnet build bITdevKit.slnx --nologo`: passed with zero warnings and errors in the earlier full-solution run. Includes the unchanged example server builds. The initial run without DOTNET_ROOT failed during OpenAPI generation because its child process could not locate ASP.NET Core; the corrected environment passed.
- `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-build --no-restore --logger "console;verbosity=minimal"`: passed after final-review mitigation, 2,016 tests, zero failures/skips, including 48 AppState cases. The current client and test assemblies were built during the preceding focused run.
- `dotnet build src/Presentation.Web.Client/Presentation.Web.Client.csproj --no-restore --nologo -p:BuildProjectReferences=false -p:GenerateDocumentationFile=true -p:WarningsNotAsErrors=CS1591`: passed with zero warnings and errors, including no CS1591 diagnostics.
- `python tests/Presentation.UnitTests/Web/Client/AppState/verify-appstate-package.py`: passed, including the feature-guide state/generated-JSON example, old custom-provider overloads and the clean dependency-graph check. It packs the actual project-reference closure, uses a source-hash package version to avoid stale cache entries, restores a package-only consumer and checks its actual transitive dependency graph. This driver and its PowerShell wrapper are local, Git-ignored verification helpers. The wrapper invokes the same driver. Documentation sync later runs with a portable PowerShell runtime under ignored `.tmp`.
- `dotnet publish tests/Presentation.WasmTestHost/Presentation.WasmTestHost.csproj -c Release --nologo -p:PublishTrimmed=true -p:EnableTrimAnalyzer=false -p:ILLinkTreatWarningsAsErrors=false -p:EnableConfigurationBindingGenerator=false`: passed with the listed overrides.

The default trimmed publish exposed existing IL2026/IL2075 diagnostics in Common.Abstractions filtering and SYSLIB1104 configuration-binding-generator diagnostics in Common.Options. The listed overrides permit managed IL trimming and browser execution with source-generated state JSON metadata. They do not prove a warning-clean default trimmed package closure or AOT support. The optional wasm-tools workload is absent, so native WASM optimizations are not enabled.

## Review mitigation regressions

`AppStateCoordinationTests` adds eight cases for the four review findings and related reset interleavings. The six original regression cases failed against the reviewed implementation before the fixes. A separate immediate-save case also demonstrated an obsolete transition writing defaults after its observer reset and reloaded the state.

- Reentrant context changes from either legacy event stop obsolete notifications and projected diagnostic retention. Automatic persistence also checks the originating generation after observers finish.
- Loads requested during reset notification wait for captured-key cleanup. Nested resets retain admission order. These cases control the yielded load continuation and copy timing without debounce sleeps.
- Undo and redo change both history stacks only inside an accepted state commit. Conflicting edits leave rejected operations' stacks untouched, and old-state capture shares the commit lock.
- A provider-cancelled save restores the preceding settled status only for its current context. Cancellation still reaches the caller, and a subsequent save can retry.

The focused suite passed 40 cases after the initial fixes. That mitigation's complete Presentation run passed all 2,010 cases, including the eight new AppState regressions. XML documentation builds report zero warnings and errors. The feature guide and presentation framework specification describe the resulting behavior. All existing 17 C# and Razor guide blocks remain unchanged, and the PowerShell docs sync copies the AppState illustration and all 47 dashboard assets unchanged.

This follow-up reruns the client/XML and benchmark builds, complete Presentation tests, focused performance workloads, allocation workload, and docs sync. Native browser, package-only, and managed-trimmed WASM results are from the earlier foundation validation.

The focused performance command selects `*AppStateTransitionBenchmarks.RestoreAndNotify*` and `*AppStateHistoryBenchmarks.UpdateUndoRedo*` with the existing three-launch, five-warmup, fifteen-iteration jobs. All six cases completed without crossing the documented timing/allocation flag thresholds against the preceding correctness baseline. The finite allocation workload completed all twenty batches with matching subscription creation/disposal counts and zero remaining registrations. Timing variability still limits conclusions. These checks cover the affected paths, not the complete 51-case timing matrix or browser performance. Raw reports, source fingerprint, comparison output, and allocation JSON remain Git-ignored under `.tmp/appstate-benchmarks/mitigation/`.

## Final-review mitigations

`AppStateManagerCoordinationTests` adds six regression cases. All six failed against the reviewed implementation before the fixes. They verify nested context changes, reuse of the original context object after an intervening change, a concurrently superseded default commit, delayed obsolete owner calls, immediate cross-instance token invalidation, and recovery from rejected synchronous disposal of an async-only state.

The manager copies each newly accepted context into a distinct immutable acceptance instance. It stops an older propagation loop when that acceptance is superseded. Attached states verify the accepted instance under their own lock, and default commits check their captured state version. Version and generation checks consult the accepted owner context before a later instance's notification runs. Observer callbacks and asynchronous cleanup remain outside the manager's lock.

For states implementing only `IAsyncDisposable`, synchronous lease disposal throws before changing ownership or registration. `DisposeAsync` remains available and cleans the state once. Synchronous disposal of states implementing `IDisposable` keeps its existing behavior.

The spec now preserves the derived-state authoring pattern without promising unchanged consumer method names. The approved operation renames remain documented in the feature guide. The guide also describes superseded context transitions and async-only lease cleanup.

The focused AppState suite passed all 48 cases, and the full Presentation suite passed all 2,016 cases with no failures or skips. The client XML documentation build completed with zero warnings and errors. The XML example audit found no missing examples across 142 public declarations; all 115 executable snippets passed, and the authoring/generated-JSON sample compiled. All 17 existing feature-guide code blocks remain unchanged. Documentation sync preserved the AppState asset and all 47 dashboard assets.

Focused `AppStateLifetimeBenchmarks` and `AppStateSnapshotBenchmarks.VersionCheck` jobs completed all six cases without triggering the documented timing or allocation regression thresholds. Version checks retain zero allocated bytes per operation. The process-wide allocation workload completed twenty batches with matched subscription cleanup and zero remaining registrations. Raw reports, comparison data, source fingerprint, and allocation JSON remain ignored under `.tmp/appstate-benchmarks/mitigation-final/`. These checks cover the changed owner and disposal paths; earlier native browser, package-only, and trimmed WASM evidence was not rerun in this follow-up.

## Functional and privacy requirements

Test paths below are relative to [the AppState test directory](../tests/Presentation.UnitTests/Web/Client/AppState/).

- REQ-001: `AppStateCompatibilityTests` resolves the default authoring pattern using validated DI without JS/auth/debug configuration. The package consumer repeats this through nupkgs.
- REQ-002: `AppStateOwnershipTests.ResetAll_TwoScopes_DoesNotResetOtherRuntime` verifies owner-local inventory/reset. Scoped managers, debuggers and mutable providers use the originating services.
- REQ-003: `Create_TwoInstances_IsolatesValuesAndDisposal` verifies separate runtime IDs, values, inventory and individual removal. Remount cleanup also runs in the browser fixtures.
- REQ-004: Compatibility and registration tests verify concrete/interface identity, idempotent infrastructure registration, duplicate rejection, provider resolution and singleton rejection.
- REQ-005: Ownership tests exercise independent leases and verify that lease disposal retains a DI-owned provider until scope disposal. Factory activation uses the existing scope without opening another one.
- REQ-006: Compatibility tests verify independently mutable snapshots with history/persistence disabled and observer isolation. Revisions and copied payloads are exercised by ownership, subscription and diagnostics tests.
- REQ-007: Compatibility history tests verify bounded oldest eviction and undo/redo. Persistence/context tests cover load/reset semantics. Ordinary restore retains the original virtual `SetCurrentState` entry point.
- REQ-008: `AppStatePersistenceTests` uses controlled I/O and `FakeTimeProvider` for immediate writes, coalescing and save/reset ordering. No debounce sleep delays accepted memory transitions.
- REQ-009: Persistence tests hold a load, edit newer state, share load attempts and cancel one caller. A late read cannot commit across a revision/context boundary. Generation-sensitive loaded flags also recheck under the state lock.
- REQ-010: Ownership/persistence tests verify origin-local reset, live inventory retention and clear-after-write ordering. Manager reset never calls provider-wide clear or deletes user context.
- REQ-011: Context tests invalidate values and tokens immediately while old I/O remains gated. Custom legacy reset failure is aggregated and detaches that object from usable inventory; its owner must successfully reset and register it again.
- REQ-012: `AppStateProviderTests` covers anonymous stability/reload, application partitioning, encoded key collisions and declared legacy ownership. Canonical dimensions include application, module, state, stable instance, principal, workspace and schema.
- REQ-013: Provider/persistence tests cover deferred JS and explicit retry, corrupt preferences, failed manual saves and continued in-memory use. Corrupt canonical preferences cannot revive a legacy value.
- REQ-014: `AppStateSubscriptionTests` covers no initial replay, changed-only equality, explicit structural comparison with independent callback values, queued renderer work after disposal, callback cancellation and reentrant notification ordering. Native components dispatch through `InvokeAsync` and release subscriptions.
- REQ-015: Ownership/subscription tests reject stale async results even when producers ignore cancellation. Runtime, state revision and context revision participate in the token.
- REQ-016: Existing events and direct selector callbacks remain available without Requester/Notifier registration. No automatic network request, remote bus or query dependency inference is introduced.
- REQ-017: `AppStateDiagnosticsTests` verifies bounded metadata, isolated explicit projection, sanitized defaults and context-reset privacy. Logging does not retain exception messages, model values, mutation reasons or raw partition values by default.
- REQ-018: Compatibility fixtures and the package consumer preserve state constructors, derived-state authoring and provider parameter shapes. State operations use `Save`, `Load`, `Reset`, `Undo` and `Redo`; managers use `ResetAll` and `ChangeContext`. Storage providers use `SaveAsync`, `LoadAsync`, `ClearAsync` and `ClearAllAsync`. Calls and custom implementations/overrides must use these names. Optional interface capabilities have defaults. `IAppStateRuntime` is an additive public advanced contract to follow the user's modifier preference.
- REQ-019: Native Server, standalone managed-trimmed WASM and package-only verification results are recorded below. Generated JSON metadata is exercised by both browser fixtures. Publishing limitations are explicit above.
- REQ-020: The pre-refactor full run completed before production edits. The correctness-complete 51-case job also completed; its local evidence is ignored as requested.
- REQ-021: A finite process-wide allocation experiment covers awaited async selectors, coalesced saves and owner/subscription cleanup separately from BenchmarkDotNet hot paths. The experiment completed with matched subscription disposal and zero final registrations in all batches.
- REQ-022: Benchmark source and local comparison rules are retained. Environment fingerprints and measured JSON remain local and ignored. Raw reports are retained under ignored `.tmp/appstate-benchmarks/`. Compact measured values and uncertainty are retained in local ignored `baseline.json`.
- SEC-001: Separate DI scopes, lease IDs, captured partitions and individual unregister behavior are verified in ownership/persistence tests. Persisted preferences are never authorization evidence.
- SEC-002: Bounded metadata-only defaults and explicit independent projections are verified in diagnostics tests. Context-reset events omit the old model.
- SEC-003: Context invalidation occurs synchronously before awaiting serialized storage cleanup. Gated-provider tests reject stale reads and keep preceding writes confined to their captured keys. Browser closure can still prevent cleanup; no delivery guarantee is claimed.

## Native browser and package evidence

The fictional catalog fixtures use native .NET 10 components, local storage and generated JSON metadata, without databases or an identity server. The Server fixture runs through `dotnet run --project tests/Presentation.TestHost --no-launch-profile --urls http://127.0.0.1:0`. The browser test uses the published WASM files served by a loopback static HTTP server, rather than inferring published behavior from a development build.

The local, Git-ignored browser helper was run with `node .agents/skills/playwright-skill/run.js <absolute-path>/tests/Presentation.UnitTests/Web/Client/AppState/appstate-browser.test.cjs` with `APPSTATE_SERVER_URL` and `APPSTATE_WASM_URL` set to the two loopback addresses.

Final browser run passed on Server at `http://127.0.0.1:41079` and published managed-trimmed WASM at `http://127.0.0.1:38489`. JS-disabled SSR also passed, with no browser page errors recorded. The script verifies independent left/right owners, explicit save and reload, stable anonymous partitioning, three mount/dispose cycles without inventory growth, separate browser contexts, explicit context invalidation and JS-disabled SSR markup. It closes all browser contexts. Validation servers are stopped before performance measurement.

The package driver copies only a consumer-authored state fixture. Production implementation comes from packed packages, with no production project reference in the consumer. It exercises legacy provider methods through the additive default overloads and rejects MudBlazor/Common.Modules in the restored graph.

## Performance validation status

The full 51-case BenchmarkDotNet job completed, followed by the finite process-wide allocation experiment. The [benchmark instructions](../benchmarks/Presentation.Benchmarks/AppState/README.md) define BEN-001 through BEN-010 and the repeatable commands. Detailed outcomes, machine metadata, uncertainty, GC measurements and comparator checks are stored locally in ignored `plan/evidence/appstate-benchmarks.md` and generated benchmark JSON. This follows the user's request to ignore benchmark evidence, collections and one-off verification scripts. Formal tests, benchmark C# sources and instructions remain tracked; one-off verification scripts and wrappers are local and ignored.

The selector timing cases use a scalar page value. Structural-comparer behavior and callback-value isolation have a functional test; those timing figures do not describe an arbitrary consumer comparer. Worker allocations, retained heap and native browser costs remain distinct measurements.

## Scope and remaining integration

Production changes stay in `Presentation.Web.Client`; there are no new production packages or Domain/Application/Infrastructure edits. The user requested public contracts where practical. Coordination details remain private partial-class implementation, and all public/protected symbols have XML documentation.

The [presentation framework spec](../docs/specs/spec-blazor-presentation-framework.md#appstate-implementation-status) now records implemented AppState capabilities. Authoritative host identity adapters, UI action scopes, shell initialization, automatic refresh policy, diagnostics presentation, URL restoration and cross-runtime integration remain framework work. Native Server/WASM tests do not close the complete TF-13/15/16/17 matrix or Auto/mixed-root behavior. AOT is excluded from the requested scope.

`git diff --check` passed. `git diff -- examples/DoFiesta` is empty after solution/OpenAPI generation. Validation hosts were stopped and their loopback ports released before benchmarks.

## Review follow-up

Review requested short operation names. State now exposes `Save`, `Load`, `Reset`, `Undo` and `Redo`; provider operations use `SaveAsync`, `LoadAsync`, `ClearAsync` and `ClearAllAsync`; managers use `ResetAll` and `ChangeContext`. Task return types, cancellation and persistence behavior are unchanged. Custom implementations and virtual overrides must adopt the new names. One-off verification helpers and wrappers are local and Git-ignored.

After the rename, the focused AppState suite passed all 34 tests. The Client XML build passed with zero warnings/errors. The packed-package consumer compiled and executed the renamed state/provider operations and the guide example. The final solution build passed with zero warnings/errors in 1m38s. Earlier full-suite, browser and performance outcomes above describe the initial implementation; those runs were not repeated for this naming-only follow-up.

## Shared Presentation host names

The native fixture projects are now `Presentation.TestHost` and `Presentation.WasmTestHost`. Their directories, project files, C#/Razor namespaces, solution entries and documentation references use those names. They are shared Presentation test hosts, with AppState as the initial scenario and room for later presentation-framework scenarios.

After the rename, `dotnet build bITdevKit.slnx --nologo` passed with zero warnings/errors in 2m47s. Publishing `tests/Presentation.WasmTestHost/Presentation.WasmTestHost.csproj` in managed-trimmed Release mode also passed with the previously documented Common-package overrides. Both Debug assemblies use the new project names. Existing functional/browser/performance scenarios were not rerun for this project/namespace rename. `git diff --check` passed and the example application sources remain unchanged.

## Storage provider operation names

A subsequent provider review specified `SaveAsync`, `LoadAsync`, `ClearAsync` and `ClearAllAsync`. All provider overloads, built-in implementations, custom test/benchmark stores and storage callers now use those names. The state facade retains the previously reviewed operation names. The guide and framework spec describe both contracts.

After this provider rename, all 34 focused AppState tests passed. The Client XML documentation build and Presentation benchmark project build passed with zero warnings/errors. The local, Git-ignored package consumer compiled and executed the `Async` provider methods through their cancellation overloads, checked the guide example and confirmed the package dependency boundary. `git diff --check` passed. Performance workloads were compiled without rerunning measurements for this method rename.

## XML usage examples

The removed registration example has been restored with current diagnostics properties, `AsInMemoryScoped`, generated model metadata and the current fluent API. Every public AppState type and declared public method/constructor now has a direct or inherited usage example. Interface examples supply shared implementation documentation without duplicating the contract.

The source audit covers 30 public types, 104 public methods and eight explicit public constructors. There are 115 direct examples and 27 inherited method examples, with no missing coverage. An independent reflection audit of the built client assembly confirmed the 30 types and 104 declared public methods.

A local, Git-ignored fixture extracts code directly from the XML comments. All 115 snippets compiled against the current SDK. It executed 114 snippets successfully; the remaining snippet defines the fictional model, authored state and generated JSON context used and exercised by every fixture. The fixture uses real state/persistence/DI behavior with an in-memory JS boundary and renderer-dispatch stand-in; this is not a new browser/render-mode validation run.

The final Client XML documentation build passed with zero warnings/errors. `git diff --check` passed. Comparing production files before/after with XML comment lines removed confirmed that no implementation code changed. Stale descriptions of enabled/history behavior, component persistence lifetime and the builder return value were corrected. All extraction, execution and coverage helpers remain under ignored `.tmp/appstate-xml-review/`.

## Presentation feature guide refresh

The Presentation AppState guide now includes a standalone quickstart, a complete leased Razor component, sibling instance keys, selector ownership, guarded HTTP results, generated JSON metadata, interactive preference loading, explicit saves/resets, current provider methods and bounded diagnostic configuration. The Presentation index description points to these capabilities. Compatibility wording now names the current contracts rather than claiming unchanged method names.

All 17 guide code blocks were extracted into a local ignored validation project. The 12 C# usage snippets executed successfully, and the model/state/JSON definitions compiled. Both Razor examples compiled. Native HtmlRenderer checks exercised component ownership, renderer dispatch, independent sibling state and disposal for nonpersisted and persisted variants. The persisted variant also executed the documented post-render load method and an acknowledged save through a controlled JS boundary. Static rendering performed zero JS calls, and component disposal returned live registration counts to zero. The fixture supplies the native host's anonymous authentication state before rendering; it starts no HTTP server and performs no external HTTP requests. This does not claim browser interaction or a new render-mode acceptance run.

The validation helpers remain under ignored `.tmp/appstate-feature-docs/`. `git diff --check` passed. No production code changed for this documentation update.

## Native Blazor lifecycle integration

AOT support is excluded from the requested scope. It is not pending implementation or an acceptance gate. Managed WebAssembly publishing and applicable trimming checks remain in scope.

The optional `AppStateLifecycle.Attach` handle now uses native `PersistentComponentState` capture and restore callbacks. `AppStateHandoff<TState>`, `CaptureHandoff`, `TryRestoreHandoff`, `HandoffIdentity` and `HandoffKey` provide guarded initial transfer under logical instance/context/schema keys. Runtime IDs, history, versions and subscriptions are rebuilt. Accepted native snapshots take precedence over optional preference loads until reset, without creating history or scheduling a save. Attachment reads the key without copying or retaining an initial model.

The final focused suite passes 80 AppState cases. The full Presentation suite passes 2,048 tests with no failures or skips. Two controlled-copy interleavings failed before the final version recheck and pass after it: feature code changing context or accepting an edit while the old model is copied cannot be overwritten by the pending handoff. Converter-based tests also cover context changes during native serialization and deserialization.

The Server fixture includes `/lifecycle` in the reusable `Presentation.TestHost`. Real prerender activation and actual `Blazor.pauseCircuit()` / `Blazor.resumeCircuit()` were exercised from prerendered and interactive-only pages. They preserve initialization markers and complete date-range/Active filters under new runtime IDs, rebuild subscriptions, retain empty initial history and reject stale browser preferences during subsequent loads. Browser testing caught a minute-only `datetime-local` format override that disagreed with native normalized events; the guide and fixture now use Blazor's default format.

The Client XML documentation build passes with zero warnings/errors. The public type/method/constructor example audit covers 148 declarations with 122 direct and 26 inherited examples. All 121 executable XML snippets pass; the authoring/generated-metadata example compiles and runs through the same consumer fixture. The feature guide now contains 27 C# and Razor blocks, including compiled native metadata, the lifecycle owner and the explicitly prerendered page. The shared-filter component checks still run with JSON reflection disabled. All four Mermaid diagrams parse and render.

Fourteen lifecycle, transition, history and version-check benchmark cases completed with the documented repeated-measurement jobs. A native-round-trip follow-up measured removal of the unnecessary attachment copy. The final run raised no timing or allocation flags against the first lifecycle run; version checks remained allocation-free. These comparisons qualify this implementation change and do not establish a universal latency guarantee. Raw measurements, comparisons, source fingerprints and one-off drivers remain Git-ignored under `.tmp/appstate-benchmarks/lifecycle*` and `.tmp/appstate-lifecycle-probe/`.

The guide explains how AppState complements native Blazor state, selective notifications and lifecycle persistence, including source precedence and owner cleanup. ADR-0043 records the integration boundary. The presentation framework spec now treats the generic native lifecycle adapter as delivered and retains framework-specific ownership, authoritative context, Auto/mixed-root composition and publishing acceptance as integration work. Native imperative serialization requires host JSON reflection; this follow-up makes no reflection-disabled/AOT claim for the lifecycle adapter.

Final packaging and cleanup checks also pass. The package driver packs the real dependency closure under a fresh source-hash version, and a package-only consumer executes the new handoff and native attachment APIs alongside the existing compatibility and guide samples. Its dependency graph still excludes MudBlazor and Common.Modules. All twenty process-wide allocation batches have matching subscription creation/disposal counts and zero final registrations. The Server browser scenarios were rerun against the final source and both pass; the owned test process was stopped and its port released. Raw outputs and helper scripts remain ignored.
