---
goal: Add portable Profiling session and snapshot archives
version: 1
date_created: 2026-08-08
last_updated: 2026-08-08
owner: bITdevKit maintainers
status: 'Implemented'
tags: [feature, profiling, json, import, export]
---

# Profiling session portability implementation plan

## Introduction

Add a bounded portability layer so a complete terminal Profiling session or one immutable snapshot can be exported as JSON and imported later as a fresh terminal session. Imported sessions use the existing list, inspection, metadata, retention, deletion, and one-session/one-node evaluation behavior.

Session-to-session comparison is explicitly not part of this addition. Existing same-session two-snapshot comparison remains unchanged.

## Requirements and constraints

- **REQ-001**: Export a complete terminal session graph.
- **REQ-002**: Export one immutable snapshot with minimum source context, including while the source session runs.
- **REQ-003**: Import atomically into both in-memory and Entity Framework stores.
- **REQ-004**: Generate fresh internal GUIDs and fresh eight-character lowercase session, node, and snapshot keys.
- **REQ-005**: Never overwrite, merge, resume, or activate imported sessions.
- **REQ-006**: Preserve full-session evidence and archive-local segment relationships; exclude evaluations and private identifiers.
- **REQ-007**: Expose caller-owned stream APIs, console file commands, and dashboard browser upload/download.
- **REQ-008**: Preserve the existing raw snapshot export as a separate, non-importable contract.
- **CON-001**: JSON format `bitdevkit.profiling.archive`, version `1`, kinds `session` and `snapshot`.
- **CON-002**: Maximum archive size 25 MiB; reject unknown properties, unsupported enum values, invalid references, and incomplete required fields.
- **CON-003**: No new physical table, migration, package, hosted service, background backup, file watcher, compression, encryption, or cloud integration.
- **CON-004**: Complete-session export is terminal-only; snapshot export may read an immutable snapshot from an active session.
- **CON-005**: Console replacement requires `--overwrite` and uses a temporary sibling file.
- **CON-006**: Dashboard import accepts a browser upload and never a server filesystem path.

## Architecture

- `IProfilingArchiveService` owns archive mapping, fixed JSON serialization, bounded validation, identity remapping, and orchestration.
- `IProfilingStore.ImportSessionAsync` inserts one already-remapped terminal graph atomically.
- Dedicated archive wrappers use archive-local integer references for segment parents and metric-to-segment links.
- Domain model `[JsonIgnore]` boundaries and identity converters prevent internal GUID and Broadcast-correlation export.
- Imported sessions are ordinary terminal store aggregates and therefore require no schema change.

## Implemented phases

### Phase 0 — Specification alignment

- [x] Separate raw snapshot export from portable archives in the canonical spec.
- [x] Preserve the explicit prohibition on session-to-session comparison.
- [x] Define both archive kinds, fixed limits, identity remapping, atomicity, and lifecycle behavior.

Checkpoint: canonical spec and PRD agree that portability does not add comparison behavior.

### Phase 1 — Archive contract and service

- [x] Add fixed public archive models, format constants, import result, and typed archive error.
- [x] Add caller-owned stream APIs for session export, snapshot export, and import.
- [x] Add strict JSON handling, 25 MiB bounds, relationship validation, and source-to-fresh identity remapping.
- [x] Register the archive service through `AddProfiling()`.

Checkpoint: Common Utilities builds and focused round-trip/negative tests pass.

### Phase 2 — Store atomicity

- [x] Add one graph import operation to the provider contract.
- [x] Implement lock-protected all-or-nothing in-memory insertion.
- [x] Implement serializable transactional Entity Framework insertion in the existing aggregate model.
- [x] Add shared contract coverage for both providers.

Checkpoint: imported graphs survive provider reads; duplicate import data fails without partial state.

### Phase 3 — Console commands

- [x] Add `profiling export` / `prof export` for complete sessions and paired node/snapshot selection.
- [x] Add `profiling import` / `prof import`.
- [x] Enforce explicit overwrite and temporary-sibling finalization.
- [x] Add registration and file workflow tests.

Checkpoint: Presentation builds and console tests pass.

### Phase 4 — Dashboard

- [x] Add complete-session and arbitrary selected-snapshot browser downloads.
- [x] Add bounded multipart JSON upload and return the imported session key.
- [x] Select the imported session after a successful upload.
- [x] Keep existing raw JSON download and exactly two primary charts unchanged.

Checkpoint: endpoint and rendered UI tests cover download filenames, upload, controls, and authorization inheritance.

### Phase 5 — Documentation and hardening

- [x] Update canonical specification and feature documentation.
- [x] Verify no cross-session comparison code, route, command, or UI was added.
- [x] Run final repository build and focused Profiling suites.

## Validation matrix

- Full session round-trip preserves evidence but replaces public and internal identities.
- Snapshot round-trip creates a completed one-snapshot session.
- Internal GUIDs and private Broadcast identities are absent from JSON.
- Parent segments and segment-linked metrics remap correctly.
- Running complete-session export fails; running snapshot export succeeds.
- Invalid, unknown, incomplete, and oversized archives mutate nothing.
- Repeated valid imports create independent terminal sessions.
- In-memory and Entity Framework providers implement the same import contract.
- Raw snapshot export and existing evaluation/comparison remain compatible.
- Console writes only completed archives to the requested destination.
- Dashboard uses browser streams and exposes no server path input.

## Files

- `docs/specs/spec-performance-snapshot-dashboard.md`
- `docs/prd/profiling/prd-0000-PROFILING-portable-session-archives.md`
- `src/Common.Utilities/Profiling/Archive/*`
- `src/Common.Utilities/Profiling/ProfilingAbstractions.cs`
- `src/Common.Utilities/Profiling/InMemoryProfilingStore.cs`
- `src/Infrastructure.EntityFramework/Profiling/EntityFrameworkProfilingStore.cs`
- `src/Presentation.Web/Profiling/ConsoleCommands/ProfilingArchiveConsoleCommands.cs`
- `src/Presentation.Web/Profiling/Dashboard/*`
- Profiling-focused Common, Infrastructure, and Presentation tests
