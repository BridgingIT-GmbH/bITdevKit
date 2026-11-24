# ADR-0043: Integrate AppState With Native Blazor Persistence

## Status

Accepted. Recorded 2026-11-17.

## Context

AppState already owns typed models, revisions, subscriptions and optional feature preferences. Blazor recreates runtime owners between prerendering and interactivity and when restoring a persisted Server circuit. Reconstructing AppState from defaults can repeat initial reads or discard an accepted filter. Serializing a live state service would also transfer inappropriate implementation details and could bypass context, revision or history rules.

## Decision

Provide an explicit `AppStateLifecycle.Attach` handle over native `PersistentComponentState` callbacks. Hosts approve each model and native render mode. One owner reserves each logical instance within the originating native persistence service. Native callbacks capture copied model values and restore them into fresh owners. Logical application/module/state/instance/schema and host context must match; runtime IDs, versions, history, callbacks and services are rebuilt.

Accept restoration as guarded initialization. Prior edits or invalidated versions reject the snapshot. An accepted snapshot takes precedence over subsequent preference loads until reset; those loads can establish storage readiness without replacing the model. Initialization neither adds history nor schedules a save. Hosts coordinate authoritative URL values and initial reads around attachment. Circuit restoration can be disabled independently of prerender handoff.

The integration is opt-in and uses the repository's .NET 10 component APIs. Feature model serialization receives explicit JSON metadata. Native imperative persistence serializes a JSON string containing that payload and requires JSON reflection to be enabled in the host. The adapter does not promise reflection-disabled or AOT support. No new server-side package dependency is introduced into the client abstraction.

## Alternatives considered

- Annotate authored state properties with `[PersistentState]`. This is convenient for simple services but distributes restoration, ownership and precedence rules across every state. It does not supply a guarded initialization transition or named component-lease ownership.
- Implement handoff as another `IAppStateStoreProvider`. This reuses preference I/O but conflates a one-time lifecycle snapshot with repeated durable loads and saves. It also obscures native render-mode handling and circuit restore context.
- Leave lifecycle wiring to each application. This keeps the toolkit smaller but repeats callback cleanup, logical keys, serializer setup and stale-result handling. A reusable optional adapter centralizes those rules.

## Consequences

Existing in-memory and provider-backed consumers retain their current setup. Opted-in hosts can reuse initial data and restore circuit snapshots without sharing live objects. They must establish trusted context before attachment, select only transfer-safe values and give independent views distinct instance keys.

Native handoff deliberately wins over stored preferences. A host that wants browser preferences to replace server defaults must choose a different initialization order or omit handoff for that state. Durable business writes and draft recovery remain feature responsibilities. Circuit persistence must be configured in the host and is not guaranteed delivery across refreshes or failures.

Native serialization adds payload and allocation costs only when opted in. The adapter's string envelope also adds JSON encoding overhead. Runtime and instance tests, native activation/pause-resume checks and allocation benchmarks qualify the supported paths; they do not establish every Auto or mixed-root arrangement.

## References

- [AppState feature guide](../features-presentation-appstate.md)
- [Presentation framework specification](../specs/spec-blazor-presentation-framework.md#appstate-implementation-status)
- [Native prerender persistence](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0)
- [Native circuit persistence](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/server?view=aspnetcore-10.0#circuit-state-persistence)
