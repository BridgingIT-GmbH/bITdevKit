---
id: PRD-0000
title: Profiling Portable Session Archives
slice: PROFILING
status: Implemented
ticket:
---

# Product Requirements: Profiling Portable Session Archives

## Overview

Profiling sessions held by the in-memory provider disappear when the process stops. Developers need a manual JSON archive that preserves a useful session or snapshot and can be imported into a later process. Imported evidence shall appear in the ordinary session list and use the existing inspection and one-session/one-node evaluation behavior.

This addition does not implement session-to-session comparison. The existing same-session snapshot comparison and selected-node session evaluation remain unchanged.

## Scope

- Complete terminal-session JSON archive export.
- Single immutable-snapshot JSON archive export, including from a running source session.
- Atomic import into both in-memory and Entity Framework stores without schema changes.
- Programmatic caller-owned stream APIs.
- `profiling`/`prof` console file commands.
- Dashboard browser downloads and browser file upload.
- Existing raw snapshot export remains unchanged and non-importable.

Automatic backup, file watching, compression, encryption, cloud storage, archive editing, merge, resume, configurable formats, and cross-session comparison are out of scope.

## User stories and acceptance criteria

### Story 1: Export a complete session

As a developer, I want to export a terminal Profiling session so that I can preserve the complete diagnostic run outside the process.

1. A terminal session exports as one JSON archive containing session metadata, nodes, participations, runtime contexts, snapshots, markers, segments, and custom metrics.
2. Computed evaluations, internal GUIDs, Broadcast correlation values, provider details, and stack traces are absent.
3. Exporting a running complete session fails and leaves the destination unchanged.
4. Segment and metric relationships use archive-local references.

### Story 2: Export one snapshot

As a developer, I want to export one snapshot so that I can preserve a lightweight point-in-time observation.

1. The archive contains exactly one immutable snapshot plus the minimum source session, node, participation, and runtime context.
2. A stored snapshot may be exported while its source session is running.
3. The session, node, and snapshot selection must be consistent.
4. Import creates a `Completed` one-snapshot session named `Imported snapshot — <source session name> — #<sequence>`.
5. Existing evaluation reports insufficient timeline evidence rather than inventing data.

### Story 3: Import a portable archive

As a developer, I want to import a preserved archive so that it appears in the normal session list for inspection and evaluation.

1. Import validates the complete fixed contract before one atomic provider mutation.
2. Fresh internal GUIDs and fresh eight-character lowercase session, node, and snapshot keys are generated.
3. Source-to-imported node and snapshot key mappings plus the imported session key are returned.
4. Repeated import creates independent terminal copies and never overwrites or merges.
5. Invalid, oversized, inconsistent, running-session, unsupported-format, or unsupported-version archives leave storage unchanged.
6. Imported sessions never become active or resumable, and normal pinning, retention, metadata, deletion, selection, and evaluation behavior applies.

### Story 4: Use every operator surface

As a developer, I want equivalent archive operations in code, the console, and the dashboard so that the workflow fits my local development loop.

1. `IProfilingArchiveService` accepts caller-owned streams for session export, snapshot export, and import.
2. `profiling export` accepts `--session`, optional paired `--node` and `--snapshot`, `--output`, and optional `--overwrite`.
3. Console export uses a temporary sibling and moves it into place only after success; `profiling import --file` imports one local file.
4. The dashboard downloads `profiling-session-<key>.json` or `profiling-snapshot-<key>.json` and uploads a multipart browser file; it never accepts a server path.
5. A successful dashboard import selects the new session.

## Fixed constraints

- Format identifier: `bitdevkit.profiling.archive`.
- Version: integer `1`.
- Kinds: `session` and `snapshot`.
- Maximum size: 25 MiB.
- JSON only; unknown properties and enum values are rejected.
- No new table, migration, package, hosted service, or collection-path work.
- Archive processing is manual and off the collector hot path.
- Evaluation results remain computed and are never persisted in archives.

## Validation evidence

- Common unit tests cover full round-trip, running snapshot export, identity remapping, relationship remapping, invalid/oversized rejection, and terminal-session enforcement.
- Shared store contract tests run atomic import against in-memory and Entity Framework providers.
- Presentation tests cover command registration/file workflows, archive routes, upload/download responses, path builders, and dashboard controls.

