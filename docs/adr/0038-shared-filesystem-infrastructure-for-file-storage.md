# ADR-0038: Share filesystem infrastructure across storage features

## Status

Accepted and implemented as of 2026-11-02. Deployment qualification remains separate; see the [qualification checklist](../storage-filesystem-qualification.md).

## Date

2026-11-02

## Context

`Infrastructure.FileSystem` now owns Windows and Linux filesystem primitives used by Blob Storage and Document Storage. Ordinary File Storage still placed `LocalFileStorageProvider`, local monitoring and their registrations in `Application.Storage`. That placement prevented reuse of the new infrastructure without reversing the dependency direction.

File Storage exposes caller-selected paths, directories and streams. Blob and document providers own hashed identities, manifests, generations, retention cursors and conditional publication. Their storage layouts cannot serve as an interchangeable base for ordinary files.

## Decision

- Move `LocalFileStorageProvider` into `Infrastructure.FileSystem/Files` and `LocalLocationHandler` into `Infrastructure.FileSystem/Monitoring`, under the `BridgingIT.DevKit.Infrastructure.FileSystem` namespace.
- Replace the Application builders' local-registration methods with `UseLocal` extensions supplied by Infrastructure in the existing `Microsoft.Extensions.DependencyInjection` namespace.
- Keep `IFileStorageProvider`, its factory, behaviors, monitoring orchestration and the provider-neutral `OpenWriteFileStream` in Application. Azure, in-memory, Entity Framework and permalink implementations also use that stream wrapper.
- Share `FileSystemFileOperations` for instrumented file opens, `FileSystemPaths` for path normalization and link checks, `FileSystemDurability` for native publication, and `FileSystemResults.FromException` for operating-system error classification.
- Preserve File Storage's existing errors and add shared typed filesystem errors for caught filesystem failures. Its existing metrics behavior remains available; shared counters expose lower-level opens and publication operations.
- Keep the Windows credential/impersonation adapter in `Infrastructure.Windows`. It references `Infrastructure.FileSystem` and inherits the moved local provider. The shared package has no dependency on Windows-specific impersonation services.

The dependency direction is `Infrastructure.Windows -> Infrastructure.FileSystem -> Application.Storage`. Application has no reference to either infrastructure package.

## Alternatives considered

Keeping the concrete provider in Application would require moving infrastructure mechanisms inward or introducing a reverse reference. Moving the provider and watcher gives those mechanisms one infrastructure owner.

Implementing File Storage through `FileSystemStore` would impose manifests, generated filenames, ownership markers and retention state on ordinary directories. Shared low-level operations preserve the distinct file-storage contract.

Moving Windows impersonation into the shared package would couple Linux consumers to Windows services or create a reference cycle. The Windows adapter remains in its platform package.

## Consequences

Applications using local File Storage or local monitoring must reference `BridgingIT.DevKit.Infrastructure.FileSystem`. Existing `UseLocal(...)` call shapes remain available through extensions after recompilation. Direct construction needs the new namespace. This is an assembly and namespace migration; a compatibility type in Application would reintroduce the forbidden dependency.

Ordinary paths and existing File Storage coordination semantics remain distinct from blob/document key authority. Direct writes retain direct-write behavior. Temporary writes publish through the native replacement helper when the returned stream closes successfully, with Buffered acknowledgement. This does not grant the Blob Storage durability, TTL, multi-host ownership or recovery contract to File Storage.

Open file readers permit deletion sharing so a selected reader can survive replacement. Required native replacement operations can fail on unsupported filesystems or servers. Shared-storage and power-loss qualification remain pending.

## Related decisions

- [ADR-0037: Direct file I/O for filesystem storage providers](0037-filesystem-storage-provider-composition.md)
- [File Storage](../features-storage-files.md)
- [Storage Monitoring](../features-storage-monitoring.md)
