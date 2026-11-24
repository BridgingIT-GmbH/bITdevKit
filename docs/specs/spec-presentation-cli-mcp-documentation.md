---
created: 2026-11-23
status: implemented
---

# Specification: Bundled MCP Documentation

The MCP assembly contains the DevKit documentation as embedded resources. MCP operations search and read that content without calling GitHub or the online documentation site. There is no fallback source.

Developers install the MCP tool locally in their repository alongside the matching DevKit version. That setup determines which docs are available. Version differences are ignored: this feature adds no version comparisons, compatibility checks, warnings, or lookup restrictions.

This specification extends the [DevKit STDIO MCP specification](spec-presentation-web-mcp-diagnostics.md). The [MCP feature guide](../features-cli-mcp.md) describes the implemented behavior. Implementation and platform qualification are tracked in the [implementation plan](../../plan/pln-feature-mcp-embedded-documentation-1.md).

## Current behavior

- [`GitHubMcpDocumentationSource`](../../src/Presentation.Cli/Mcp/McpDocumentationTools.cs) recursively lists Markdown under GitHub's `docs/` directory and downloads every discovered file from `main`.
- Its ten-minute in-memory cache is specific to one process. A first lookup, restart, or expired cache causes another enumeration and download pass.
- [`GitHubPagesMcpApiReferenceSource`](../../src/Presentation.Cli/Mcp/McpApiReferenceTools.cs) reads generated local Pages output when present and otherwise requests the public API index and symbol details.
- [`McpGuidanceTools`](../../src/Presentation.Cli/Mcp/McpGuidanceTools.cs) already contains local guidance with repository-relative documentation paths.

Bundling removes these documentation requests and the resulting dependency on GitHub availability and rate limits.

## Scope

The implementation belongs to Presentation CLI and documentation packaging. Reuse `IMcpDocumentationSource`, `IMcpApiReferenceSource`, `McpResponse`, `McpNextCall`, and the existing tool catalog.

Documentation remains available before an application starts. Application-side handlers, IPC contracts, runtime selection, and operation permissions remain unchanged. No project scanning, package resolution, version selector, or documentation download service is required.

## Bundled content

Include the following content in `BridgingIT.DevKit.Cli`:

- Public Markdown documentation, preserving paths such as `docs/features-jobs.md`.
- The existing generated API index and symbol details, preserving API UIDs.

Use the same public-guide selection as the website, currently defined in [`sync-docs.ps1`](../site/scripts/sync-docs.ps1). Share that selection rather than maintaining another independent list. Include matching root-level guides and exclude `docs/adr/`, `docs/specs/`, `docs/prd/`, presentations, generated website copies, binary assets, and full website HTML.

Authored Markdown remains the source of truth. Generate packaged content from it instead of maintaining a second copy. References to other bundled documents retain paths and anchors that agents can use through MCP. External links remain references and are never fetched automatically.

## Embedded resources

Include the selected Markdown and generated API metadata as `EmbeddedResource` items in `Presentation.Cli.csproj`. Read them from the CLI assembly through `Assembly.GetManifestResourceStream`.

Use explicit logical resource names that map to the existing `docs/...` document paths and API metadata paths. Keep the mapping inside the documentation source implementations so tool arguments remain unchanged.

The documentation travels with the assembly. There is no separate archive, content directory, extraction step, or documentation package. The repository already uses embedded resources for dashboard feature icons in [`Presentation.Web.csproj`](../../src/Presentation.Web/Presentation.Web.csproj).

Replace the HTTP-backed source registrations in [`McpCliCommandModule`](../../src/Presentation.Cli/CliHost/CliCommandModules.cs) with implementations backed by embedded resources. Remove the GitHub and Pages lookup paths, including the local Pages override. Curated guidance stays in `McpGuidanceTools` and refers to the embedded documents.

## MCP operations

Keep the existing tool names and required arguments:

- `bdk_docs_search` and `bdk_docs_get` search and read bundled Markdown.
- `bdk_api_search` and `bdk_api_get` search and read bundled API metadata.
- `bdk_guidance_list` and `bdk_guidance_get` retain their current guidance and provide document paths or suggested local calls.

Tool descriptions state that documentation comes from the installed MCP package. Built-in operations that recommend DevKit documentation use the same document paths and suggested `bdk_docs_get` calls. A new tool per document or feature is unnecessary.

Preserve existing response fields and the `McpResponse` envelope. The content does not change when the agent selects another runtime.

### Search and bounded reads

Preserve the existing result limits. Documentation search defaults to five results with a maximum of twenty. API search defaults to eight with a maximum of twenty-five. Return deterministic ordering and set `truncated` only when additional matches exist.

`bdk_docs_get` retains `source` and `maxChars`, with the existing default of 6,000 and maximum of 20,000 characters. Add an optional non-negative `offset`, defaulting to zero, so agents can read beyond the first chunk of a long document.

Return `offset`, `nextOffset`, and `totalChars` alongside the existing document fields. Offsets use .NET UTF-16 string indices. Chunks do not split Unicode surrogate pairs. An offset inside a pair, or a limit too small to return one complete character, returns `operation_failed`. `nextOffset` is null at the end. When more content exists, `next` suggests another `bdk_docs_get` call with the following offset.

An offset equal to the document length returns empty content. Invalid types, negative offsets, and offsets beyond the length return `operation_failed`. API tools keep their current response shape and bounds.

### Document paths and URLs

Repository-relative document paths are the canonical identifiers. Existing official GitHub blob and raw URLs remain accepted as aliases for their bundled document paths. The URL's branch, tag, or version does not select content or trigger a comparison. The tool always returns the installed bundle's content.

Unknown hosts, traversal paths, absolute filesystem paths, and documents outside the bundle are rejected. A URL argument never causes an HTTP request. Any returned `url` is a reference link rather than a retrieval source.

### Status and errors

`bdk_mcp_status` reports documentation source `embedded` and whether the resources are readable. `bdk_mcp_self_test` can exercise local documentation lookup independently of runtime IPC availability. Neither retrieves online documentation.

Missing or unreadable documentation and unknown documents or symbols return `documentation_unavailable` with a clear local reason. They do not trigger fallback, refresh, or repeated retry. Compiled guidance and unrelated runtime operations remain usable when documentation lookup fails.

Load search indexes once per process and reuse them. There is no TTL refresh or writable documentation cache.

## Build integration

Reuse [`build-agent-index.ps1`](../api/scripts/build-agent-index.ps1) and the existing API metadata models. Separate metadata generation from full website publication so packaging does not require MkDocs, Docker, or a published site.

Markdown resources are included automatically in every CLI build. Generate the complete API metadata only for Release builds or when packaging the tool, keeping normal Debug builds fast.

The Release and packaging workflow is:

1. Build the assemblies used to generate API metadata.
2. Generate the API index and symbol details locally.
3. Build the CLI with the selected Markdown and generated API files embedded as resources.
4. Pack the resulting CLI assembly into the tool package.

The current API metadata project includes the CLI itself. If this inclusion is retained, use an initial CLI build for metadata generation and a final CLI build to embed the generated files. Keep these stages explicit so resource generation does not recursively invoke the CLI build.

Debug builds skip API metadata generation. They can embed existing generated API files when present. When none are available, only API lookup returns `documentation_unavailable`; embedded Markdown, curated guidance, and runtime operations remain usable.

The existing CI uses `dotnet pack --no-build`, so complete the final Release build with embedded resources before packing. Source development runs use the resources in their built assembly and need no content files beside it. Normal SDK and build-tool restoration remains a build prerequisite.

### Completeness marker

Release builds and packaging check only for the presence of `api/agent-index.json` as the completeness marker. A missing index fails with a clear error. For `dotnet pack --no-build`, check for that embedded index in the already-built CLI assembly.

Do not validate individual Markdown links, guidance targets, or API detail references. There are no version, commit, hash, or fingerprint checks. If a requested document or symbol detail is missing at runtime, return the local unavailable response described above.

### Add or update documentation

Documentation discovery runs automatically as part of the CLI build, using the shared public documentation patterns. New matching files are included through MSBuild resource globs or equivalent build-time discovery. There is no per-file registration in the project, a resource catalog, or an MCP tool.

The authoring flow is:

1. Add or edit a public document under `docs/`, for example `docs/features-example.md`.
2. Build the CLI. The build embeds matching documents and picks up additions, edits, renames, and removals without requiring a clean build or a manual documentation sync command.
3. Pack and install the rebuilt local MCP tool, then restart the MCP process. For source development, restart using the rebuilt CLI output.

The initial shared selection includes root-level `INDEX.md`, `introduction-ddd-guide.md`, `common-*.md`, `development-*.md`, `features-*.md`, `storage-*.md`, and `testing-*.md`. Files outside these patterns and excluded directories such as `docs/specs/` are not automatically included. A new public documentation category requires a change to the shared selection, rather than a separate MCP-only list.

Matching documents become available through search and get automatically. A new curated guidance topic still requires authoring its guidance separately. Existing installed or running MCP tools continue to use their embedded content until replaced and restarted. `dotnet pack --no-build` packages the last built assembly and does not pick up later documentation edits on its own.

## Acceptance criteria

1. An installed MCP tool searches and reads Markdown and API metadata without a running application, repository checkout, credentials, or warmed cache.
2. Documentation lookup, initialization, error recovery, status, and self-test make zero outbound documentation requests.
3. Documentation, API, and guidance tool names and existing required arguments remain compatible.
4. Guidance retains public document paths and suggests `bdk_docs_get` calls. Documentation and API operations use those paths and existing API UIDs without per-link build validation.
5. Successive document chunks reproduce the full document without gaps or duplication. Invalid offsets return a bounded validation error.
6. Existing official documentation URLs resolve locally regardless of their branch, tag, or version. Version differences never block lookup or produce warnings.
7. Missing content returns a local error without trying another source or disabling unrelated operations.
8. The packed tool contains the required Markdown and API metadata as assembly resources and works offline in a consuming workspace on Windows and Linux, without separate documentation files.
9. Repeated lookups reuse loaded indexes. Cancellation, response bounds, and protocol-clean stdout remain supported.
10. Adding, editing, renaming, or removing a matching public document is reflected in the next CLI build without per-file registration, manual sync, or a clean build. Files outside the shared selection remain excluded.
11. Release builds and packaging generate and embed complete API metadata. Debug builds skip that generation and keep Markdown and guidance usable even when generated API files are absent.
12. Release builds and packaging fail when the API index marker is absent. They do not check individual links or detail references. Debug builds do not require the marker.

## Verification and rollout

Use focused tests for local search and retrieval, guidance's suggested tool calls, continuation, accepted URL aliases, invalid paths, and missing content. Include a URL with a different version to prove that it is treated only as a path alias. Verify the missing-index build failure without adding a link-validation pass.

Verify the packed tool in an isolated consuming workspace with outbound network access denied, without repository files, separate documentation files, or a documentation cache. Exercise documentation, API, guidance, status, and self-test.

The MCP feature guide, client configuration guide, CLI package README, and original MCP specification describe bundled content and local error behavior. Implementation and Linux package checks are complete. Windows execution qualification remains open in the implementation plan.
