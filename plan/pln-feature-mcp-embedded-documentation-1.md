---
goal: Embed DevKit documentation and API metadata in the MCP CLI assembly
version: 1.0
date_created: 2026-11-23
last_updated: 2026-11-23
owner: bITdevKit maintainers
status: 'In progress'
tags: [feature, mcp, documentation, presentation, build]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

Implement [Bundled MCP Documentation](../docs/specs/spec-presentation-cli-mcp-documentation.md) in Presentation CLI and documentation build tooling. Embed public Markdown in every CLI build. Generate API metadata for Release builds and packaging, then embed it in the same assembly. Replace the online documentation providers while preserving the existing MCP tools, response envelope, and runtime operations.

Implementation and Linux verification are complete. Windows execution qualification remains pending because no Windows runner is available in this session. Task completion below records implemented code and completed local checks; TASK-017 remains open for the Windows result.

## 1. Requirements & Constraints

- **REQ-001**: Documentation and API tools read only embedded assembly resources. Remove GitHub HTTP requests, GitHub Pages requests, local Pages overrides, TTL refresh, and writable documentation caches. Reference URLs do not retrieve content.
- **REQ-002**: Share the website's public-guide selection. Initially include root-level `INDEX.md`, `introduction-ddd-guide.md`, `common-*.md`, `development-*.md`, `features-*.md`, `storage-*.md`, and `testing-*.md`. Exclude nested ADRs, specifications, PRDs, presentations, generated site content, and binary assets.
- **REQ-003**: Matching Markdown additions, edits, renames, and removals affect the next CLI build without per-file registration, manual sync, or a clean build. Preserve repository-relative IDs such as `docs/features-jobs.md`.
- **REQ-004**: Generate full API metadata only for Release builds or packaging. Ordinary Debug builds embed Markdown and any previously generated API files but never invoke the API generator. Absent API metadata affects API lookup only.
- **REQ-005**: Use `api/agent-index.json` as the only build/package completeness marker. Fail a final Release build or packaging when it is absent. For `--no-build`, inspect the already-built assembly for this resource. Do not check individual Markdown links, guidance targets, API detail references, versions, commits, hashes, or content fingerprints.
- **REQ-006**: Preserve `bdk_docs_search`, `bdk_docs_get`, `bdk_api_search`, `bdk_api_get`, `bdk_guidance_list`, and `bdk_guidance_get`. Keep existing required arguments and response fields. Add document continuation using optional `offset` and response fields `offset`, `nextOffset`, and `totalChars`.
- **REQ-007**: Documentation search defaults to 5 results and caps at 20. API search defaults to 8 and caps at 25. Document get defaults to 6,000 characters and caps at 20,000. Search ordering is deterministic, and `truncated` indicates additional matches rather than a full last page.
- **REQ-008**: Accept official GitHub blob and raw URLs as local path aliases regardless of their branch, tag, or version. Ignore MCP/DevKit version differences. Add no version comparison, warning, selector, or response identity metadata.
- **REQ-009**: Missing documents, missing symbol details, or unreadable resources return `documentation_unavailable`. Invalid document offsets return `operation_failed`. No error triggers online fallback. Compiled guidance and unrelated runtime operations remain usable.
- **REQ-010**: Status reports documentation source `embedded`. Self-test reports local documentation availability even when no runtime is selected. Load documentation and API indexes once per process and propagate cancellation.
- **SEC-001**: Normalize only supported repository paths and official repository URL aliases. Reject unknown hosts, path traversal, absolute filesystem paths, and resource names outside the embedded documentation prefixes. Never use tool input as a filesystem path or HTTP destination.
- **CON-001**: Changes belong to `src/Presentation.Cli/`, `docs/` build tooling, Presentation tests, and CI integration. Do not add documentation dependencies to Common, Domain, Application, or Infrastructure, and do not change app-side IPC or operation permissions.
- **CON-002**: Reuse `IMcpDocumentationSource`, `IMcpApiReferenceSource`, the API metadata records, `McpResponse`, `McpNextCall`, and `docs/api/scripts/build-agent-index.ps1`. Use BCL resource and JSON APIs. Add no NuGet dependency, documentation package, separate archive, or content files beside the CLI.
- **CON-003**: CI currently uses `Build_Configuration: Debug` in `.github/workflows/github-actions.yml`. Preserve that setting and explicitly enable API generation during package preparation. Do not change signing or global build configuration.
- **CON-004**: `docs/api/ApiReference.proj` includes the CLI. Bootstrap metadata generation with an explicit recursion guard and isolated intermediate/output paths. Keep top-level builds and tests sequential in a worktree.
- **CON-005**: Preserve unrelated working-tree changes. Follow `.editorconfig`, XML documentation conventions, and the repository's xUnit/Shouldly/NSubstitute test conventions. New tests verify observable behavior and build/package contracts, not every documentation link.

### Fixed implementation values

- **CFG-001**: Shared selection file: `docs/public-documentation.props`. Define the `DevKitPublicDocumentationGlobs` property with root-qualified REQ-002 patterns and `DevKitDocumentationRoot` as that file's directory. Use a property so wildcard patterns are not prematurely expanded relative to the CLI project.
- **CFG-002**: Logical resource prefix: `BridgingIT.DevKit.Cli.Documentation/`. Markdown resource names append the canonical path, for example `BridgingIT.DevKit.Cli.Documentation/docs/features-jobs.md`. API names append `api/agent-index.json` or `api/agent-symbols/<generated-name>.json`.
- **CFG-003**: Generated API files: `src/Presentation.Cli/obj/McpDocumentation/<Configuration>/api/`. The existing `obj/` ignore rule applies. Debug may reuse files from this directory or its sibling Release directory, preferring the current configuration. These files are build inputs only, never a runtime lookup source.
- **CFG-004**: `BdkGenerateMcpApiReference` defaults to `true` for Release and `false` for Debug. Packaging without `--no-build` enables it even for Debug. Internal `BdkMcpApiBootstrap=true` suppresses API generation, API resource inclusion, and the final index-marker requirement for the bootstrap compilation only.
- **CFG-005**: Bootstrap and DocFX staging root: `src/Presentation.Cli/obj/McpDocumentation/<Configuration>/bootstrap/`. Pass its absolute path through `UseArtifactsOutput=true` and `ArtifactsPath` to bootstrap child builds so they cannot overwrite the outer build's `bin/` or `obj/` files.

## 2. Implementation Steps

Each task lists its direct prerequisites. Tasks with no dependency within their phase may proceed independently, but top-level build/test commands remain sequential. Do not start later phases until their stated prerequisites are complete.

### Implementation Phase 1

- GOAL-001: Establish shared Markdown discovery and embedded-resource access.
- Dependencies: none.
- Completion criteria: a Debug CLI build exposes the selected Markdown resources, website selection is unchanged, and incremental document changes require no manual registration.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `docs/public-documentation.props` with CFG-001 properties. In `docs/site/scripts/sync-docs.ps1`, replace the private `$includedPatterns` list with patterns read from the XML property. Retain root-only discovery, destination renaming, asset copying, and website-specific link rewriting. The redundant explicit `features-cli-mcp-clients.md` entry is covered by `features-*.md`. Depends on: none. | ✅ | 2026-11-23 |
| TASK-002 | Import the shared selection in `src/Presentation.Cli/Presentation.Cli.csproj`. Add root-level Markdown `EmbeddedResource` globs using explicit CFG-002 logical names. Do not copy the files to output. Add `build/EmbeddedDocumentation.targets` under the CLI project for generated API resource collection after generation and before `AssignTargetPaths`. Resource discovery must run after generation so files missing at project evaluation are still included in the same compilation. Depends on: TASK-001. | ✅ | 2026-11-23 |
| TASK-003 | Add internal `EmbeddedMcpResources` in `src/Presentation.Cli/Mcp/EmbeddedMcpResources.cs`. Provide an assembly-taking constructor for tests and methods `GetDocumentPaths()`, `ReadTextAsync(string path, CancellationToken)`, and `Contains(string path)`. Use only `GetManifestResourceNames()` and `GetManifestResourceStream()`, preserve canonical IDs, and dispose streams. Use `EmbeddedMcpResources.ResourcePrefix` and `ApiIndexPath` constants for the CFG-002 resource spelling. Depends on: TASK-002. | ✅ | 2026-11-23 |

### Implementation Phase 2

- GOAL-002: Generate and embed API metadata for Release and packaging without slowing ordinary Debug builds.
- Dependencies: TASK-001 and TASK-002.
- Completion criteria: Release and both Debug/Release packaging include the API index resource; a fresh Debug build invokes no API generation; the build graph terminates without recursive generation or outer-output collisions; only the index is checked for completeness.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Add `docs/api/scripts/build-mcp-api.ps1` accepting mandatory `Configuration`, `OutputRoot`, and `BootstrapRoot` parameters. Bootstrap `docs/api/ApiReference.proj` with CFG-004/CFG-005 properties and XML documentation enabled. Keep its existing project inclusion/exclusion set. Stage each built project's assembly and adjacent XML documentation using its evaluated `TargetPath` under those same properties. Resolve relative paths from `$PSScriptRoot`, propagate nonzero child exit codes, and run bootstrap builds sequentially. Depend on: TASK-002. | ✅ | 2026-11-23 |
| TASK-005 | Complete `build-mcp-api.ps1` by generating a temporary DocFX metadata configuration beneath `BootstrapRoot`, using absolute staged assembly paths and the existing `docs/api/filterConfig.yml`. Invoke DocFX metadata generation only, then call `build-agent-index.ps1 -MetadataRoot <generated-metadata> -OutputRoot <OutputRoot>`. Produce CFG-003 `agent-index.json` and `agent-symbols/*.json`. Do not invoke `build-pages.ps1`, generate website HTML, run `test-agent-index.ps1`, or validate generated links/details. Depend on: TASK-004. | ✅ | 2026-11-23 |
| TASK-006 | In `src/Presentation.Cli/build/EmbeddedDocumentation.targets`, add target `BdkPrepareEmbeddedApiReference` before resource collection. When CFG-004 enables generation and bootstrap is false, execute TASK-005 before adding API `EmbeddedResource` items with CFG-002 names. In ordinary Debug builds, include only existing generated API files using CFG-003 precedence. In the final Release compilation, require only the generated index file to exist. Bootstrap compilation must neither generate API metadata nor require the marker. Depend on: TASK-005. | ✅ | 2026-11-23 |
| TASK-007 | Add `BdkPrepareMcpPackageApiReference` before `_WalkEachTargetPerFramework` and `BdkEnsureMcpPackageApiIndex` before `PackToolImplementation` in `EmbeddedDocumentation.targets`, so API preparation precedes publication and the marker check inspects the assembly selected for packing. With `NoBuild!=true` and `BdkGenerateMcpApiReference!=true`, invoke a sequential child `Build` of this project with `BdkGenerateMcpApiReference=true`, even if an older index exists. Never invoke child `Pack`. When generation was already enabled for the normal pack build, do not repeat it. For `NoBuild=true`, perform no build or generation. Add a BCL-only inline MSBuild task using `System.Reflection.PortableExecutable.PEReader` and `System.Reflection.Metadata` to inspect the published CLI assembly for the single embedded CFG-002 API index name without executing the CLI. Fail only when that marker or the assembly is absent. Inspect no other resource target or reference and compare no identity metadata. Depend on: TASK-006. | ✅ | 2026-11-23 |

### Implementation Phase 3

- GOAL-003: Route MCP documentation operations to embedded resources and preserve their bounded contracts.
- Dependencies: TASK-003. TASK-008 through TASK-012 can be developed with Debug builds and small embedded test fixtures; they do not require full API generation.
- Completion criteria: docs/API tools have no HTTP-backed or workspace-file-backed source path; existing tool names and required inputs remain stable; document continuation and local errors match the specification.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | Add `EmbeddedMcpDocumentationSource` in `src/Presentation.Cli/Mcp/EmbeddedMcpDocumentationSource.cs`, implementing `IMcpDocumentationSource.ListAsync` and `GetAsync` with `EmbeddedMcpResources`. Cache the successful immutable document set once and report `Name` as `embedded`. Normalize plain `docs/...` IDs and official `github.com` blob or `raw.githubusercontent.com` URLs for owners `bridgingit` and `bridgingit-gmbh`, repository `bitdevkit`, case-insensitively. Extract the `/docs/` suffix after the repository/ref portion, ignore URL ref/query/fragment for content selection, and retain reference-only `Url` fields. Reject decoded traversal segments, unsupported hosts/repos, absolute filesystem inputs, and paths outside discovered resources. Never fetch a URL. Depend on: TASK-003. | ✅ | 2026-11-23 |
| TASK-009 | Add `EmbeddedMcpApiReferenceSource` in `src/Presentation.Cli/Mcp/EmbeddedMcpApiReferenceSource.cs`, implementing `GetIndexAsync` and `GetSymbolAsync`. Deserialize the embedded index with `CliJson.Options`, reuse its cached symbols, and read the indexed detail only when requested. Support both the existing `McpApiReferenceSymbolPage` format and individual-symbol JSON. Missing details or a missing requested UID return unavailable content through the tool layer; do not synthesize a detail from the index or use another source. Cache successful page reads. Cancellation must not permanently cache a canceled load. Depend on: TASK-003. | ✅ | 2026-11-23 |
| TASK-010 | In `McpDocumentationTools.SearchAsync` and `McpApiReferenceTools.SearchAsync`, take `limit + 1` matching entries before constructing the returned page and set `truncated` only when the extra entry exists. In `McpDocumentationTools.GetAsync`, parse `offset` explicitly rather than using the clamping/default behavior of `McpJson.GetInt32`: absent means zero; an invalid JSON type, non-integer, negative, overflow, or value beyond content length returns `operation_failed`. Slice using existing `maxChars` bounds and UTF-16 indices; return continuation fields and an exact next-call argument set. At EOF return empty content and `nextOffset=null`. Preserve API get response shape. Depend on: TASK-008 and TASK-009. | ✅ | 2026-11-23 |
| TASK-011 | Register `EmbeddedMcpResources` and TASK-008/TASK-009 as singletons in `McpCliCommandModule.RegisterServices` in `src/Presentation.Cli/CliHost/CliCommandModules.cs`. Remove the docs-only `HttpClient` registration and the old `GitHubMcpDocumentationSource` and `GitHubPagesMcpApiReferenceSource` implementations from their existing files. Retain the source interfaces and metadata records. Update `McpToolCatalog` docs/API descriptions and add optional integer `offset` with minimum zero to `bdk_docs_get`; keep every existing tool name. Keep `McpGuidanceTools` compiled and usable independently, with local document next calls. Depend on: TASK-008, TASK-009, and TASK-010. | ✅ | 2026-11-23 |
| TASK-012 | Update `McpRuntimeTools.GetStatusAsync` to report source `embedded`, whether Markdown resources exist, and whether the API index is present, without enumerating link targets or parsing all details. In `SelfTestAsync`, perform one bounded local documentation read and include its result even on the existing no-runtime early return. Missing API metadata must not prevent Markdown guidance or IPC operations. Remove recovery suggestions that merely repeat a failed docs search/get when content is unavailable; suggest a local rebuild/reinstall without performing it. Depend on: TASK-011. | ✅ | 2026-11-23 |

### Implementation Phase 4

- GOAL-004: Verify resource loading and package behavior, then integrate the existing Debug CI packaging path.
- Dependencies: TASK-007 and TASK-012.
- Completion criteria: focused Presentation tests pass; installed-tool smoke checks pass offline on Windows and Linux; marker-only rejection works; CI prepares complete embedded API content before its existing `--no-build` pack loop.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | Add `tests/Presentation.UnitTests/Cli/EmbeddedMcpDocumentationTests.cs` and minimal Markdown/API fixtures beneath `Cli/Fixtures/EmbeddedDocumentation/`. Add explicit fixture `EmbeddedResource` names in `tests/Presentation.UnitTests/Presentation.UnitTests.csproj`, using a test-only prefix that cannot enter the CLI package. Cover TEST-001 through TEST-006 below. Replace `GitHubPagesMcpApiReferenceSource_WhenLocalPageDetailExists_ResolvesRequestedSymbol` in `CliFoundationTests.cs` with an embedded-page-detail equivalent; preserve existing ranking/filter/guidance/STDIO tests. Depend on: TASK-012. | ✅ | 2026-11-23 |
| TASK-014 | Add `docs/api/scripts/test-mcp-documentation.ps1` with `Configuration` and `PackageDirectory` inputs and evidence output under `plan/evidence/mcp-embedded-documentation/`. Implement TEST-007 through TEST-010 using sequential child commands, disposable source copies for resource-mutation checks, and a temporary local tool installation. Do not edit the active checkout's real documents or installed tool. Drive the installed `bdk mcp` with STDIO JSON-RPC, fail on unavailable expected content or malformed responses, and clean up child processes and temporary directories. Network denial is a verification-environment precondition after tool restore/install, not a new runtime setting. Depend on: TASK-007 and TASK-013. | ✅ | 2026-11-23 |
| TASK-015 | In `.github/workflows/github-actions.yml`, insert package preparation after package evaluation and before `Package pack (nuget)`, under the same publishable-ref condition. Restore build tools and rebuild only `src/Presentation.Cli/Presentation.Cli.csproj` using the existing `Build_Configuration` plus `-p:BdkGenerateMcpApiReference=true`. Keep the existing Debug setting, signing, publish guards, and package loop. That loop's `--no-build` then checks the compiled index marker without regenerating data. Perform local package smoke verification before any existing publish step; do not add publishing to the implementation verification commands. Depend on: TASK-014. | ✅ | 2026-11-23 |

### Implementation Phase 5

- GOAL-005: Document the implemented workflow and complete the required checks.
- Dependencies: TASK-015.
- Completion criteria: the four consumer/reference documents describe the shipped behavior; relevant build, test, and installed-package evidence is recorded; only then mark this plan completed.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-016 | Update `docs/features-cli-mcp.md`, `docs/features-cli-mcp-clients.md`, `src/Presentation.Cli/README.md`, and `docs/specs/spec-presentation-web-mcp-diagnostics.md` to describe embedded docs, automatic public Markdown inclusion, Release/package API generation, Debug behavior, index-only completeness checking, local URL aliases, continuation, and restarting after tool installation. Preserve the feature-guide structure. Use fictional consumer models and paths. Do not add version negotiation or a PRD. Depend on: TASK-015. | ✅ | 2026-11-23 |
| TASK-017 | Run the section 6 verification commands sequentially and record commands, exit codes, OS, and package smoke results in `plan/evidence/mcp-embedded-documentation/verification.md`. Run `git diff --check`, inspect the scoped diff, and update task completion fields and plan status only for work actually completed. Do not commit, tag, publish, or update the developer's installed MCP tool as part of verification. Depend on: TASK-016. | | |

## 3. Alternatives

- **ALT-001**: A packaged ZIP or loose documentation files were rejected in favor of assembly resources. The assembly must contain everything needed at runtime.
- **ALT-002**: HTTP fallback, larger caches, and GitHub authentication retain network dependence and were rejected.
- **ALT-003**: Generating API metadata in every Debug build adds unnecessary local build work. Generate it for Release or packaging instead.
- **ALT-004**: Recursively embedding all Markdown would include drafts, ADRs, and duplicate website sources. Reuse the website's public-guide selection.
- **ALT-005**: Version matching, fingerprints, and per-link validation were explicitly rejected. The API index is the sole completeness marker.
- **ALT-006**: Excluding the CLI from API generation would avoid its self-reference but would change API coverage. Preserve coverage and use an isolated, guarded bootstrap compilation.

## 4. Dependencies

- **DEP-001**: The accepted specification and the three recorded decisions are prerequisites: Release/package-only API generation, website public-guide selection, and index-only completeness checking.
- **DEP-002**: Resolve the .NET SDK from `global.json` and target settings from project files. Use the pinned DocFX tool from `.config/dotnet-tools.json`; restore it before Release/package generation. PowerShell 7 is required for the existing and new build scripts. Ordinary Debug builds must not invoke PowerShell or DocFX for this feature.
- **DEP-003**: API generation reuses `docs/api/ApiReference.proj`, `docs/api/filterConfig.yml`, and `docs/api/scripts/build-agent-index.ps1`. SDK/tool restoration may need network access; installed documentation lookup must not.
- **DEP-004**: Use existing Presentation unit-test dependencies. No database, running sample application, Testcontainers fixture, or application runtime is required for the documentation tests.
- **DEP-005**: Prepare Windows and Linux verification environments that can deny outbound network access after package installation. Package restore/build and offline runtime verification are separate stages.

## 5. Files

- **FILE-001**: New `docs/public-documentation.props`; modify `docs/site/scripts/sync-docs.ps1` to share the public Markdown selection.
- **FILE-002**: Modify `src/Presentation.Cli/Presentation.Cli.csproj`; add `src/Presentation.Cli/build/EmbeddedDocumentation.targets` for resource discovery, Release generation, package preparation, and the single marker check.
- **FILE-003**: New `docs/api/scripts/build-mcp-api.ps1` for bootstrap assemblies, metadata generation, and existing API index generation. Existing `ApiReference.proj`, `filterConfig.yml`, and `build-agent-index.ps1` remain the source inputs; do not change their API coverage or detail format.
- **FILE-004**: New `src/Presentation.Cli/Mcp/EmbeddedMcpResources.cs`, `EmbeddedMcpDocumentationSource.cs`, and `EmbeddedMcpApiReferenceSource.cs`.
- **FILE-005**: Modify `src/Presentation.Cli/Mcp/McpDocumentationTools.cs` and `McpApiReferenceTools.cs` to remove remote providers, retain shared models, and implement bounded local lookup behavior.
- **FILE-006**: Modify `src/Presentation.Cli/CliHost/CliCommandModules.cs`, `src/Presentation.Cli/Mcp/McpToolCatalog.cs`, and `McpRuntimeTools.cs` for registration, optional offset, status, and self-test. `McpGuidanceTools.cs` changes only where recovery text or recommended calls need local wording.
- **FILE-007**: Modify `tests/Presentation.UnitTests/Cli/CliFoundationTests.cs` and `tests/Presentation.UnitTests/Presentation.UnitTests.csproj`; add `Cli/EmbeddedMcpDocumentationTests.cs` and `Cli/Fixtures/EmbeddedDocumentation/` beneath that test project.
- **FILE-008**: New `docs/api/scripts/test-mcp-documentation.ps1`; modify `.github/workflows/github-actions.yml` for explicit API-enabled CLI preparation before Debug `--no-build` packaging.
- **FILE-009**: Modify the four documentation files named in TASK-016. Record implementation evidence under ignored `plan/evidence/mcp-embedded-documentation/`, outside `docs/`.

## 6. Testing

- **TEST-001**: With an assembly fixture, list selected document IDs, read known Markdown, and demonstrate that runtime lookup uses no checkout or `.github/pages` files. Fixture-injected assembly loading must not depend on the test working directory.
- **TEST-002**: Get the same document using its path and official blob/raw URLs with `main`, a different tag, and a slash-containing ref. Reject unknown host/repository, traversal, and absolute filesystem inputs. Assert that version differences neither warn nor change returned content.
- **TEST-003**: Request document chunks through EOF and concatenate them to the fixture text. Test offset absent, zero, equal to length, negative, too large, fractional, overflow, and wrong JSON type. Check 6,000/20,000 character bounds and exact next-call arguments. Verify `truncated=false` when exactly `limit` matches exist and true with one additional match for docs and API search.
- **TEST-004**: Read API page details and individual-symbol fixtures by UID. Missing index, missing detail, and absent UID produce local unavailable responses. Search ranking, filters, existing detail data, and cancellation remain supported. Do not synthesize missing details or read workspace API files.
- **TEST-005**: In a Debug fixture without API metadata, docs and curated guidance remain usable. Status says `embedded`; self-test includes documentation results when no runtime exists. Missing API content must not prevent unrelated runtime tool dispatch.
- **TEST-006**: Repeat concurrent lookups and a canceled lookup followed by a successful lookup. Verify successful indexes are reused without TTL reload and cancellation does not poison later calls. Keep existing STDIO catalog/protocol tests passing and stdout protocol-clean.
- **TEST-007**: In a disposable source copy, add a matching Markdown file, build Debug, read it through MCP, edit it, rename it, and remove it with incremental rebuilds. Verify changes without clean/sync commands. A file under `docs/specs/` stays excluded. These are resource-discovery tests, not a validation pass over documentation links.
- **TEST-008**: Build a fresh Debug CLI with no generated API files and observe no API generation. Build Release and pack Debug with generation enabled; verify both final assemblies contain the API index marker. Use isolated generation paths to detect recursion or output collisions.
- **TEST-009**: On a disposable Debug assembly with no API index, `dotnet pack --no-build` fails with the missing-marker error. Compile a disposable marker fixture with the exact CFG-002 index resource name and deliberately omit its referenced detail. The marker task must accept that assembly. Separately request a missing detail through the embedded API source and expect `documentation_unavailable`. This proves marker-only validation without a per-reference scan.
- **TEST-010**: Install the exact local package into a temporary tool path, then disable outbound network access. From a fictional consuming workspace without source docs, call all six documentation/API/guidance tools plus status/self-test using STDIO. Choose a returned document ID and returned API UID for get calls instead of hardcoding a potentially renamed symbol. Verify available content, local error cases, no fallback requests, and process cleanup on Windows and Linux.

Execute these commands sequentially from the repository root after implementation. Build-dependent checks use the properties defined above; no command publishes a package:

```sh
dotnet restore bITdevKit.slnx
dotnet build src/Presentation.Cli/Presentation.Cli.csproj --configuration Debug --no-restore
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --configuration Debug --no-restore --filter 'FullyQualifiedName~BridgingIT.DevKit.Presentation.UnitTests.Cli'
dotnet tool restore
dotnet build src/Presentation.Cli/Presentation.Cli.csproj --configuration Release
dotnet build src/Presentation.Cli/Presentation.Cli.csproj --configuration Debug -p:BdkGenerateMcpApiReference=true
dotnet pack src/Presentation.Cli/Presentation.Cli.csproj --configuration Debug --no-build --output artifacts/mcp-documentation/packages
pwsh -NoProfile -File docs/api/scripts/test-mcp-documentation.ps1 -Configuration Debug -PackageDirectory artifacts/mcp-documentation/packages
dotnet build bITdevKit.slnx --configuration Debug --no-restore
git diff --check
```

The package smoke stage requires the DEP-005 offline runtime environment. Disposable source-copy build checks use a small API-producer fixture to verify build ordering and normal Debug package contents; the installed main package smoke uses real DocFX metadata. Record that stage separately from network-enabled restore/build commands. Broaden testing only if the scoped checks reveal a failure outside the touched CLI/build paths.

## 7. Risks & Assumptions

- **RISK-001**: API generation includes the CLI that requests generation. The bootstrap guard must propagate through every child build, and bootstrap artifacts must use isolated paths. A recursion guard alone does not prevent competing writes to ordinary solution outputs.
- **RISK-002**: Generated API resources do not exist at initial project evaluation on a clean checkout. Collect them after generation before resource assignment; otherwise the first Release build could omit the marker.
- **RISK-003**: CI packages Debug artifacts with `--no-build`. Generation solely conditioned on Release would publish incomplete documentation; TASK-015 explicitly prepares the CLI in that same Debug configuration.
- **RISK-004**: New resource files add assembly size and index memory. Load indexes once and read API detail pages on demand. No speculative size budget or external search service is introduced.
- **RISK-005**: Index-only validation intentionally permits missing or stale individual details. Handle them locally at lookup time. Do not add exhaustive validation or fingerprints to compensate.
- **RISK-006**: `McpJson.GetInt32` defaults invalid inputs and clamps ranges. Reusing it for `offset` would violate the specified validation behavior; TASK-010 requires explicit offset parsing.
- **ASSUMPTION-001**: Developers install the repo-local MCP tool they intend to use. Any MCP/DevKit version difference is ignored.
- **ASSUMPTION-002**: New public documentation follows the shared root-level patterns. A new category changes only the shared selection; it does not introduce a per-document registration or automatic guidance-topic generator.
- **ASSUMPTION-003**: Local Debug API data may be absent or from a previous generation. This is accepted for fast development. Release/package generation prepares the intended complete metadata, with only marker presence checked.
- **QUAL-001**: Linux verification passed: Debug and Release CLI builds, the full Debug solution build, 155 focused CLI tests, real package smoke with network access disabled, and nine package/build checks. Windows script support is implemented but execution qualification remains pending. Evidence is in `plan/evidence/mcp-embedded-documentation/verification.md`.


### Implementation outcomes

- Source selection uses one root-qualified `DevKitPublicDocumentationGlobs` property. MSBuild item declarations expand wildcards too early relative to the CLI project; the shared property keeps all seven patterns intact and discovers 71 current public guides.
- API details use a private item type before assigning qualified logical-name metadata. This avoids target-time batching against existing Markdown resources. The real build contains 5,142 API detail resources and one API index.
- API extraction uses a single-processor DocFX child process. Default-parallel extraction crashed in this environment; serial extraction succeeded. Standard MSBuild inputs/outputs skip unchanged extraction. No documentation identity or per-link checks were added.
- Normal Debug package preparation runs before `_WalkEachTargetPerFramework`. The single index check runs before `PackToolImplementation` against the published assembly actually selected for packing. An installed Debug package regression check covers the ordering.
- Review fixes preserve Unicode scalar boundaries in document chunks and clean up failed MCP initialization. All 155 focused CLI tests pass.

## 8. Related Specifications / Further Reading

- [Bundled MCP Documentation specification](../docs/specs/spec-presentation-cli-mcp-documentation.md)
- [DevKit STDIO MCP specification](../docs/specs/spec-presentation-web-mcp-diagnostics.md)
- [DevKit MCP feature guide](../docs/features-cli-mcp.md)
- [MCP client configuration](../docs/features-cli-mcp-clients.md)
- [Public documentation selection and website sync](../docs/site/scripts/sync-docs.ps1)
- [Existing API agent index generator](../docs/api/scripts/build-agent-index.ps1)
- [Clean Architecture boundaries](../docs/adr/0001-clean-onion-architecture.md)
