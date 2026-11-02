---
title: Worktree Development Isolation
status: Pending
---

# Worktree Development Isolation

This specification defines predictable ports and isolated local resources for concurrent Git worktrees. It describes future behavior. The configuration keys, launcher commands, and bootstrap types below are proposed contracts, not available APIs.

The application detects a linked Git worktree during startup. An ordinary checkout keeps its configured URLs, including the existing HTTPS port 5001, and its existing database catalogs. Working on a different branch in that ordinary checkout does not enable isolation. No slot configuration or special launcher is required for normal development.

The requirements define scope, data, failure behavior, dependencies, and acceptance tests. Implementation remains pending.

## Scope

Developers and coding agents must be able to start multiple development hosts without interrupting another checkout or changing its data. Windows and Linux are required. The initial SQL Server adapter uses the existing database service and credential sources. Core host isolation does not require a database provider.

This specification defines an opt-in DevKit capability for applications built with the kit. It supports developers and coding agents running independent application instances from linked worktrees. The repository's two web development applications (dofiesta/weatherfiesta) are the first adopters, not the boundary of the public feature. The fictional applications TaskHarbor and ForecastHub illustrate consumer applications throughout this specification.

The public capability must be reusable without copying sample startup code, referencing a sample project, or using a repository-specific launcher. Existing applications do not acquire worktree behavior merely by updating DevKit packages.

Container-based tests, Testcontainers fixtures, integration test hosts, and their existing startup configuration remain unchanged. They continue to use their current port allocation, connection strings, database creation, reset, seed, and disposal behavior, even when the test runner executes inside a linked worktree. Developers must not add a new opt-out flag or slot assignment to existing test commands.

Included:

- A reusable, explicitly enabled DevKit host integration and consumer-facing configuration contracts.
- Extensible resource integration with no mandatory SQL Server or sample-project dependency in the core resolver.
- Automatic linked-worktree detection during application startup.
- Automatic first-start slot assignment with persistent, predictable addresses.
- Unchanged ordinary-checkout ports, database catalogs, and development workflow.
- Fixed port offsets for each application.
- Consistent host, identity-provider, dashboard, and browser-client URLs.
- Separate databases, local files, authentication cookies, and data-protection identities.
- Shared launch behavior for run, watch, editor tasks, and direct host startup.
- Non-destructive startup, explicit reset, status, and resource ownership checks.
- Sequential and concurrent verification on both supported operating systems.
- An adoption guide delivered with the implementation. This specification defines that guide's requirements; the guide is not written in this planning phase.

Excluded:

- Automatically enabling isolation in applications that have not adopted the feature.
- Automatic detection of whether an AI agent is present or permission to mutate resources on an agent's behalf.
- Changes to container-based tests, test fixtures, or test-resource lifecycle.
- Changes to container-hosted application startup.
- Random ports, port zero, and automatic fallback to another port.
- Production deployment configuration or production database administration.
- A SQL Server container per worktree.
- Remote worktrees, shared development directories, and multiple machines coordinating one slot registry.
- Automatic deletion of resources when a branch or worktree disappears.
- Automatic namespacing of arbitrary external cloud services.
- Hardware isolation for benchmarks. Concurrent hosts still share CPU, memory, disks, and SQL Server capacity.

## Current constraints

Changing a listening URL does not update the fake identity issuer, registered callback URLs, or static browser authentication configuration. Development hosts also need protection against destructive database initialization.

The current dashboard cookie registration uses `.Bdk.Dashboard`. The fake identity provider uses `.Bdk.FakeIdentityProvider`. Cookies on the same hostname require separate names even when ports differ.

Existing DevKit building blocks remain the foundation:

- `DevKitWebApplication.CreateBuilder(args)` owns host construction.
- `AddConfiguration()` composes application configuration providers.
- `AddFakeIdentityProvider()` and `WithIssuer()` configure the development identity provider.
- Dashboard OpenID Connect registration configures authority, callbacks, and cookies.
- Database creator services and startup tasks initialize persistence and seed data.

The implementation must inspect these components before adding helpers. It must preserve Clean Architecture boundaries. Application and Domain projects must not reference the development bootstrap or Infrastructure projects.

## Decisions

### D1: Automatic worktree detection and persistent slots

Each linked worktree owns one integer slot. On first startup, the resolver assigns the lowest unclaimed slot under the registry lock and persists it. Assignments persist across process restarts, branch switches, and application launches. Startup never chooses a new slot because a port is busy. An optional command can request a particular slot before first startup.

This makes addresses recognizable without a manual setup step. Allocation uses a fixed sequence, not random numbers, branch hashes, or scanning for available TCP ports. The ordinary checkout does not participate in slot allocation.

### D2: One configuration resolver

A reusable DevKit development bootstrap resolves application identity, worktree identity, URLs, and resource names. Applications explicitly enable the host integration and supply their own application and resource declarations. All launch paths consume that resolver. Keep sample-specific conventions outside the public feature's defaults.

Use one .NET implementation for detection, validation, and derivation in application startup. A thin PowerShell 7 tool may provide optional management commands on Windows and Linux. Ordinary `dotnet run`, `dotnet watch`, and IDE launches must work without that tool. Do not duplicate port or database formulas in shell scripts and host projects.

### D3: Shared SQL Server, separate databases

Retain each configured server, authentication mechanism, and connection options. Change only the database catalog for managed local development. Separate databases isolate schema changes, jobs, queues, auditing, and storage records that use those catalogs.

### D4: Preserve resources until an explicit operation

Startup in an isolated worktree preserves existing owned resources. Reset requires a separate command, verified ownership, and an exclusive application lease. Releasing a slot never deletes its databases or files. The ordinary checkout retains its existing configured initialization and seed behavior. This feature does not redesign that lifecycle.

### D5: Keep the Development environment

Isolation does not require a new ASP.NET Core environment. Do not set container or cloud environment flags to suppress database deletion. Configure database initialization and reset directly.

### D6: Public opt-in capability with separate resource adapters

Place reusable contracts and host integration in appropriate DevKit packages. Keep sample registration in sample composition roots. The core resolver owns detection, slots, deterministic naming, and instance context. Resource adapters own provider-specific connection rewriting, ownership validation, provisioning, and reset.

The first persistence adapter supports the SQL Server behavior specified below. Filesystem resource integration supports the declared local payload, log, and key-ring roots. Consumers that do not use SQL Server must be able to adopt host and URL isolation without taking a SQL Server dependency. Other database engines and cloud resource adapters are extension points, not implied first-release implementations.

The feature makes applications suitable for concurrent agent-driven development through predictable addresses, isolated declared resources, and machine-readable status. It does not infer agent identity, bypass authentication, grant reset permission, or automatically expose an HTTP or MCP administration endpoint.

## Public adoption and extension contracts

These are required capabilities of the future API. Final symbol names and package placement must follow the existing DevKit builder conventions during implementation.

### Host registration

Provide an explicit fluent registration for a DevKit web application builder. A consuming application enables it after normal configuration composition and before module, authentication, or resource registrations consume managed values. Registration must not create a second service provider or require a custom application base class.

Applications declare a stable application ID, application catalog entry or equivalent validated port offsets, resource mappings, and any public authentication or client configuration integrations. The existing default port scheme is the initial convention. A consumer may configure a repository-wide base port, block size, and slot count if the complete mapping is validated and all applications using the common registry agree. Changing an established mapping requires explicit reconfiguration rather than silently moving an existing instance.

Registering the feature is the application's opt-in. `Auto` subsequently selects linked-worktree behavior only for eligible local development. Ordinary-checkout, production, test, build-time, and container-hosted paths retain the compatibility rules in this specification. The sample applications use this same public registration, with no private alternate resolver.

### Resolved instance context

Expose a documented, immutable instance context for startup consumers and dependency injection. It reports whether isolation is active, the selection reason, application identity, and the resolved values needed by declared integrations. In inactive mode it must not fabricate a worktree ID or slot. Provide access early enough to configure authorities, callback URLs, and resource registrations without repeating Git detection.

Separate the full local context from the sanitized status and browser configuration projections. Credentials and signing material are never context diagnostics. Local filesystem paths do not belong in the browser projection.

### Resource declarations and adapters

An application explicitly declares resources it wants isolated. Do not rewrite every connection string or writable path discovered in configuration. Each declaration identifies its resource ID, provider adapter, source configuration key or supplied options, and generated-name policy.

Adapters must support planning the target, validating ownership, initializing absent resources where supported, and explicit reset with the existing lease and confirmation rules. Recoverable failures use established DevKit result conventions. Initialization and reset support cancellation, bounded operations, and retry after partial failure. Adapters must not log credentials.

An unsupported provider must fail registration or validation when isolation for that resource is requested. Never report a resource as isolated merely because its name was displayed. Intentionally shared resources must be declared and identified as shared in status. An undeclared resource is not covered by the isolation guarantee.

Core contracts must not depend on Infrastructure implementations. SQL Server and filesystem adapters live in their owning infrastructure packages or other suitable optional integrations. The host composition root connects them. Application and Domain layers remain independent of these adapters.

### Optional management tooling

Status, reset, configure, and release use the same public context, registry, and resource contracts as startup. A consumer must not require scripts from this repository to start its application. Optional tool packaging must have a supported consumer distribution path and documented version compatibility. The script forms later in this specification illustrate workflows, not a mandatory public dependency.

Machine-readable status includes a schema version, active or inactive mode and reason, application ID, slot when active, URLs, resource IDs and sanitized targets, shared-resource declarations, and verified process ownership. Keep credentials and secret-bearing connection strings out of both text and JSON output.

## Identity and configuration data

### Startup mode detection

The shared bootstrap runs before settings are consumed by dependent registrations. Its automatic mode distinguishes these cases:

| Startup location | Required behavior |
|---|---|
| Ordinary checkout on any branch | Use existing launch URLs, database catalogs, authentication settings, and startup lifecycle. Do not allocate a slot or write registry state. |
| Linked Git worktree | Allocate or reuse a persistent slot and apply all isolation rules below. |
| Source directory outside Git | Preserve ordinary configuration. Do not invent a worktree identity. |
| Non-development environment | Preserve ordinary configuration without consulting the registry. |
| Container-hosted application | Preserve existing configuration and lifecycle without claiming worktree resources. |
| Container-based test or integration fixture, including one inside a linked worktree | Preserve existing fixture configuration, ports, databases, and lifecycle without claiming interactive resources. |
| Build-time generation | Preserve existing build configuration without claiming interactive resources. |
| Ambiguous or unreadable linked-worktree metadata | Fail before resource initialization. Do not fall back to the ordinary database. |

Resolve the containing checkout from the host content root, not an arbitrary shell working directory. Use Git's resolved per-worktree directory and common directory, together with worktree metadata, to distinguish a linked worktree from the primary checkout. A `.git` file alone is insufficient because submodules can also use one. Branch name, detached HEAD state, and a directory name containing `worktree` are not detection rules.

### Read Git metadata during startup

For eligible local development, the resolver invokes the installed Git executable through .NET's `Process` API. Use structured argument lists, capture standard output and standard error, check exit codes, and enforce a five-second timeout per command. Never construct a shell command from an application path. Spaces and special characters in paths must remain literal arguments.

The read-only commands are:

```text
git -C <host-content-root> rev-parse --show-toplevel
git -C <host-content-root> rev-parse --absolute-git-dir
git -C <host-content-root> rev-parse --path-format=absolute --git-common-dir
```

Resolve and normalize the returned paths. In an ordinary checkout, the per-checkout Git directory and common Git directory coincide. In a linked worktree, the per-worktree directory is separate from the shared common directory. Validate that relationship against Git worktree metadata when necessary. Do not assume a particular branch name or parse `.git` files as the sole detection mechanism.

These commands read local metadata. They do not contact a remote, fetch commits, use Git credentials, or modify the repository. Cache the resolved context once per host startup. Do not invoke Git per request or storage operation.

If Git is missing, fails, or times out, preserve ordinary configuration only when the location is conclusively an ordinary checkout or outside Git. If linked-worktree metadata exists or the mode cannot be established safely, report `worktree_detection_failed` before database initialization. Never fall back to the ordinary catalog from an uncertain worktree context. Report installation or metadata recovery instructions without changing Git configuration.

Evaluate application eligibility, test-host execution context, build-time execution, and container hosting before linked-worktree detection or registry access. Reuse existing host and fixture context where available. Any necessary guard belongs in application-side bootstrap integration, without changing existing fixture callers. A linked source directory alone never enables isolation in a test host. New resolver tests may explicitly exercise managed startup against disposable resources.

Ordinary-checkout applications may continue to share their existing port 5001 convention when run one at a time. Concurrent ordinary-checkout applications on identical configured ports are outside this feature's scope. A linked-worktree application must coexist with that ordinary host without changing or stopping it.

### Worktree assignment

Store this non-secret document in the gitignored worktree file `.dev/instance.json`:

```json
{
	"version": 1,
	"worktreeId": "6a2177e5-8854-4b64-b0da-2b57e5bc5d10",
	"slot": 2
}
```

| Field | Constraint |
|---|---|
| `version` | Integer, initially `1`. Unknown versions fail explicitly. |
| `worktreeId` | Generated UUID, stable for this worktree. It is not derived from the branch name. |
| `slot` | Integer from `1` through `49`, assigned once on first startup or explicitly reserved beforehand. |

Slot zero is outside the managed pool. The primary checkout has no slot assignment and retains each application's existing configuration. Linked worktrees use slots one through forty-nine. Branch names never contribute to ports, database names, or file paths.

Resolve the shared Git directory through Git, rather than assuming `.git` is a directory. Store a versioned registry at `<git-common-dir>/bdk-dev/instances.json`. Each assignment records the worktree ID, slot, canonical worktree path, and assignment timestamp.

The registry also records the shared port-allocation policy. Consumers declaring different base ports, block sizes, slot counts, or conflicting application offsets for that registry fail validation before allocation. The numeric limits and formulas below describe the default policy. A customized policy must preserve the same validation, persistence, and conflict behavior.

Registry updates must use an exclusive cross-process lock and atomic replacement. A lock wait exceeding ten seconds fails with the registry path and a retry instruction. Invalid JSON, duplicate slots, or conflicting local assignments must fail without rewriting the registry.

Two applications starting for the first time in the same worktree must converge on one assignment. Two different worktrees starting concurrently must receive different slots. Treat the registry as authoritative and the local file as its persisted worktree copy. If startup stops after committing the registry but before writing the local file, the next startup recovers that same assignment. It must not allocate another slot. A conflicting local file fails explicitly. If all forty-nine slots are claimed, fail with an instruction to inspect and explicitly release unused assignments.

Assignments belong to a worktree, not a running process. Stopping a process does not free a slot. Canonical path comparison follows host filesystem semantics, including case sensitivity. Reject duplicate canonical paths and copied assignment files that claim another worktree's ID.

### Application catalog

A tracked catalog gives each supported host a stable application ID and port offset. IDs match `^[a-z][a-z0-9-]{0,23}$`. Database prefixes match `^[A-Za-z][A-Za-z0-9_]{0,31}$`. Validate uniqueness of IDs, prefixes, and all allocated port offsets.

The following two entries are illustrative consumer declarations. The repository's first two adopters need unique entries. Other applications may supply their own entries through the public registration contract. No application is enabled merely because its name appears in a catalog.

| Application ID | Database prefix | HTTP offset | HTTPS offset |
|---|---|---|---|
| `taskharbor` | `TaskHarbor` | 0 | 1 |
| `forecasthub` | `ForecastHub` | 10 | 11 |

For slot `s`, the base port is `5000 + 100 * s`. Offsets must remain within that slot's block. Both HTTP and HTTPS ports are checked and bound to loopback only.

| Slot | TaskHarbor HTTP and HTTPS | ForecastHub HTTP and HTTPS |
|---|---|---|
| 1 | 5100 and 5101 | 5110 and 5111 |
| 2 | 5200 and 5201 | 5210 and 5211 |

The table applies only to linked worktrees. It does not change either fictional application's ordinary-checkout HTTPS port 5001 or its fixed database catalogs.

HTTPS is the canonical public origin. HTTP redirects to the resolved HTTPS port. The existing trusted localhost development certificate remains in use.

### Resolved development context

The resolver produces an immutable context before host URLs, authentication, module configuration, or storage registrations are consumed. It includes:

- Application ID, worktree ID, slot, and canonical worktree path.
- HTTP URL, HTTPS URL, and canonical public origin.
- Module-to-database catalog mappings.
- Application state, storage, log, and data-protection paths.
- Cookie names and data-protection application discriminator.
- Assignment and resource-ownership status.

Do not change ASP.NET Core's application assembly identity to implement this discriminator.

Proposed configuration inputs are `DevelopmentInstance:Mode` with values `Auto` and `Disabled`, and optional `DevelopmentInstance:Slot`. Explicitly registered host integrations default to `Auto` for eligible local startup. Applications without that registration retain existing behavior. `Disabled` is an optional administrative override, not a new requirement for fixture or build callers and not a fallback after failed detection. Existing tests and build-time hosts bypass managed startup through execution-context eligibility. An ordinary checkout in `Auto` remains unchanged. Supplying a slot in an ordinary checkout is a configuration error, not a way to activate isolation there.

In an eligible linked-worktree development host, startup creates a missing assignment automatically. An explicit slot requests that exact slot on first startup and fails if another worktree owns it. For an existing assignment, an environment or command-line slot may agree with the file, but may not silently replace it. Assignment changes belong to `configure`. Non-development environments keep existing application configuration and do not touch the registry. Build-time OpenAPI generation, container-based tests, and integration test hosts retain their existing paths without claiming slots or provisioning worktree resources.

Load normal JSON and secret providers first. In a linked worktree, use the ordinary configured database server and credentials as inputs and derive a separate catalog. Treat tracked fixed URLs, authorities, and catalogs as baseline values to transform, not configuration errors. Apply managed values before dependent registrations. A later provider must not restore those ordinary values. Conflicting caller-supplied overrides for managed settings fail with their key names and expected non-secret values. Outside linked-worktree mode, do not apply those transformations.

## User stories and acceptance criteria

All requirements are pending implementation. Each has observable behavior and a failure case.

Unless a requirement explicitly describes ordinary-checkout compatibility, isolation, ownership checks, resource reset, and slot management apply only in linked-worktree mode.

### R1: Detect a worktree and assign a predictable slot

As a developer, I want startup to recognize my linked worktree so that it gets isolated resources without manual preparation.

1. Given an unassigned linked worktree, when an application starts normally, then it claims the lowest unassigned slot and persists the same identity in the registry and local file.
2. When the worktree restarts or changes branch, then its ports and resource names remain unchanged.
3. When an explicit requested slot belongs to another worktree, or no slots remain, then startup fails without taking another slot or changing another assignment.
4. When startup sees a copied, corrupt, or conflicting assignment, then it fails before resource initialization. A missing local file with a valid registry entry recovers that entry.
5. Given an ordinary checkout on any branch, when the application starts, then its fixed URLs and catalogs remain unchanged and no assignment or registry entry is created.

### R2: Start independent applications

As a developer, I want to start different hosts and worktrees together so that development sessions do not interrupt each other.

1. Given two assigned worktrees, when their hosts start, then each listens on its catalog-derived URLs and uses its own resource names.
2. Given one worktree, when two different applications start, then their ports, databases, files, and browser authentication state differ.
3. When an assigned port is occupied, then startup fails without selecting another port or stopping another process.
4. When the same application is started twice in one worktree, then the second start fails before database initialization.
5. When a host exits or crashes, then its application lease becomes available while its slot remains assigned.

### R3: Authenticate against the correct instance

As a dashboard or browser-client user, I want login to stay inside the selected instance so that another checkout cannot redirect or sign me out.

1. When login starts, then discovery, authorization, token, logout, and callback URLs use the resolved public origin.
2. When the hosted browser client loads, then its public authentication configuration agrees with the server's issuer and allowed callback URLs.
3. When a user signs in or out of one instance, then a second instance's session remains usable in the same browser profile.
4. When a callback origin differs from the resolved origin, then startup validation rejects the inconsistent registration.
5. When public browser configuration is retrieved, then it contains no database connection strings, signing keys, or credentials.

### R4: Preserve development data

As a developer, I want startup to preserve my worktree's data so that restarting the host does not erase test scenarios.

1. When an assigned database is absent, then startup creates it, records ownership, initializes its schema, and runs idempotent seed tasks.
2. When an owned database already exists, then startup preserves its data and performs only supported schema initialization or migration.
3. When a target database exists without matching ownership, then startup fails without adopting, altering, or deleting it.
4. When a local storage root belongs to a different worktree, then startup fails without changing its contents.
5. When a schema is incompatible with the current branch, then startup fails with a reset or migration instruction and never resets automatically.

### R5: Reset explicitly owned resources

As a developer, I want an explicit reset operation so that I can recreate disposable data without affecting another instance.

1. When `reset --app forecasthub --confirm-instance forecasthub:w02` runs, then it first displays and validates the complete target inventory.
2. When the application is running or any resource ownership check fails, then reset changes no resource.
3. When all checks pass, then reset removes only that application's owned database catalogs and payload roots. The next startup recreates them.
4. When reset partially fails, then it reports completed and remaining targets, returns failure, and supports a safe retry.
5. When reset completes, then the slot, worktree ID, logs, and authentication key ring remain unchanged.

### R6: Inspect and manage local sessions

As a developer or coding agent, I want reliable status and lifecycle commands so that I can operate the intended instance.

1. When `status` runs, then it lists assignments, application URLs, sanitized resource names, and verified running-process information.
2. When run, watch, editor tasks, or direct host startup are used, then they resolve identical settings.
3. When stop is requested, then it stops only the verified process tree belonging to that worktree and application.
4. When a worktree is missing or a PID was reused, then status marks the record stale and stop refuses to terminate an unrelated process.
5. When release is requested for an application with retained resources or a running process, then it refuses to free the worktree slot until every catalog application has been checked and handled explicitly.

### R7: Adopt the feature in a consuming application

As an application developer, I want a supported DevKit integration so that developers and agents can run my application safely in parallel worktrees.

1. Given an application outside this repository, when it explicitly registers the public feature and declares its resources, then its ordinary run and IDE paths obtain the same worktree behavior without sample code or repository scripts.
2. Given an application that has not registered the feature, when DevKit packages are updated, then its existing startup, ports, databases, and tests remain unchanged.
3. When an application adopts host isolation without SQL Server, then the core registration requires no SQL Server package or database adapter.
4. When a declared resource has no supported adapter or conflicts with another application declaration, then validation fails before initialization and identifies the declaration.
5. When implementation is delivered, then the consumer guide uses the shipped public APIs and demonstrates ordinary-checkout compatibility, linked-worktree startup, and unchanged fixture behavior.

## Detailed behavior

### Authentication and public URLs

The resolved origin is the single source for fake identity `WithIssuer()`, bearer authentication authority, dashboard OpenID Connect authority, registered login and logout callbacks, API documentation OAuth callbacks, and browser-client authority.

Do not derive the issuer from an unvalidated request Host header. Bind to the known loopback origin. Validate discovery metadata against that origin in acceptance tests.

The hosted browser client receives a small same-origin public configuration document before authentication initialization. Prefer an additive endpoint such as `/_dev/instance-config`, available only for managed development. Its complete payload is the application ID, slot, public origin, authority, and public client ID. It must not expose worktree paths or resource inventories. Existing non-development client configuration remains supported. Avoid rewriting tracked `wwwroot/appsettings.json` or generating a file shared by checkouts.

Use cookie names such as `.Bdk.Dashboard.forecasthub.w02` and `.Bdk.FakeIdentityProvider.forecasthub.w02`. Review every authentication cookie, including antiforgery, correlation, and nonce cookies. Configure host-specific data-protection application identity and a persistent key ring under the application's state root. Do not share authentication tickets between instances. Protect keys using the host's supported storage permissions and key-protection facilities.

If current library APIs lack cookie configuration hooks, add narrowly scoped options or use supported options configuration. Do not make production defaults depend on Git or a development slot.

### Database naming and ownership

Each application declares all database connections it owns. Distinct module catalogs use distinct suffixes. Shared connections within that application resolve to the same catalog. Use a SQL Server connection-string builder to replace `Initial Catalog`, preserving all other connection options and credentials without logging them.

The naming formula is `<DatabasePrefix>_w<slot:00>_<ModuleId>`. Module IDs use validated letters, digits, and underscores. Enforce SQL Server identifier length limits before connecting. Example: `ForecastHub_w02_Core`.

Each database carries ownership metadata containing purpose `LocalDevelopment`, application ID, worktree ID, slot, and module ID. SQL Server database extended properties are a suitable implementation. Creating a new database does not authorize adopting a pre-existing database. Ownership must be recorded before seed or destructive operations. If provisioning fails before ownership is recorded, report the incomplete target and require explicit recovery.

Reset requires both the expected generated name and matching ownership metadata. A name prefix alone is insufficient. Do not offer a force option that bypasses ownership validation. Existing unsuffixed development databases remain untouched. Data import or adoption requires a separate future workflow.

Disable unconditional development `DeleteOnStartup(true)` behavior only in isolated worktree mode. Ordinary checkouts retain their existing configured behavior and fixed catalogs. Keep test fixture lifecycle separate from interactive host lifecycle. Worktree reset must never target the ordinary checkout's catalog.

### Files, logs, and other resources

Resolve writable roots beneath `<worktree>/.dev/state/<appId>/`. Use separate subdirectories for payload storage, logs, and data-protection keys. Record ownership in the application state root. Credentials never belong in this file.

Validate resolved paths against the intended root. Reject symlink or reparse-point escapes before recursive deletion. Reset removes declared payload subdirectories only. Retain logs and keys for diagnosis and stable sessions. Configure file logging and filesystem-backed providers to consume these paths.

Shared diagnostic sinks may remain shared, but emitted events must carry application ID, worktree ID, and slot. Any enabled external service must either have an explicit per-instance namespace or be declared intentionally shared in status output. Destructive reset never touches those external services in this release.

### Process ownership and locking

Use a separate per-application lifetime lease in the common Git state directory. The host acquires the lease before resource initialization and holds it until exit. Direct IDE startup must acquire the same lease as launcher startup.

Run, reset, and slot reassignment coordinate through that lease. Reset holds it across validation and mutation. Release acquires every affected application's lease in catalog order and holds them through final validation and registry update. This prevents a new start between a running-process check and a destructive operation.

For ordinary `dotnet watch`, each host child acquires its own lifetime lease and reuses the persisted assignment after a rebuild. The assignment survives the gap between child processes. Reset may acquire the lease while no host child runs. A new child must acquire that lease before initialization, so reset and startup cannot mutate resources concurrently. Do not require a custom watch supervisor or SDK modification to achieve automatic startup detection.

Runtime records contain process ID, process start time, application ID, worktree ID, URLs, and lease ownership. PID existence alone is insufficient for stop. Normal shutdown requests graceful termination and reports timeout before any explicit force-stop option.

Port checks are advisory. Another process may bind after a preflight check. An eventual bind failure must still fail startup and release the application lease. Never kill the conflicting process or retry with different ports.

### Proposed launcher commands

These optional management command forms define intended behavior. They are not existing tooling. Normal startup automatically assigns a slot and does not require `configure`, `run`, or `watch` wrappers:

```text
pwsh ./scripts/dev-instance.ps1 configure --slot 2
pwsh ./scripts/dev-instance.ps1 status
pwsh ./scripts/dev-instance.ps1 run --app forecasthub
pwsh ./scripts/dev-instance.ps1 watch --app taskharbor
pwsh ./scripts/dev-instance.ps1 stop --app forecasthub
pwsh ./scripts/dev-instance.ps1 reset --app forecasthub --confirm-instance forecasthub:w02
pwsh ./scripts/dev-instance.ps1 release --confirm-slot 2
```

`configure` optionally reserves a specific slot before first startup. Repeating the same assignment is idempotent. It is unavailable for ordinary checkouts. Changing the slot requires all application leases and no remaining owned databases or payload roots. The command reports what must be stopped or reset first.

`release` covers the whole worktree and all catalog applications. It never deletes resources. Refuse release while owned databases or payload roots remain. Retained logs and key rings do not block release because they remain under the old worktree path. A stale assignment with inaccessible resources requires manual recovery instructions, not automatic recycling.

Optional `run` and `watch` wrappers use supported project launch paths, preserving development static web assets. They must not launch a built web server DLL as a substitute. Existing workspace tasks and IDE launches can continue to invoke their normal project commands. The application bootstrap, not the wrapper, owns detection, assignment, and configuration derivation.

Before starting, display the application, canonical worktree path, slot, URLs, database catalog names, and storage roots. Never print complete connection strings or authentication material.

### Startup sequence

The application still starts through its normal project command or IDE action. The DevKit integration changes the effective startup settings before Kestrel, EF Core, and authentication consume them. Git supplies checkout metadata. The resolver supplies configuration values, and the existing hosting and provider APIs apply those values.

For the fictional ForecastHub application, worktree slot two resolves the default scheme as follows:

```text
HTTP URL:             http://localhost:5210
HTTPS URL:            https://localhost:5211
Core database:        ForecastHub_w02_Core
Identity issuer:      https://localhost:5211
Dashboard callback:   https://localhost:5211/_bdk/dashboard/signin-oidc
```

The host integration applies the resolved URLs to Kestrel's effective listening configuration. Tracked launch settings remain baseline inputs and must not restore port 5001 after resolution. A conflicting explicit caller override fails validation rather than silently selecting another address.

The SQL Server adapter uses a connection-string builder to replace only the declared connection's database catalog before EF Core registration. The database server, credentials, and unrelated connection options remain unchanged. Worktree startup initializes that owned catalog without running the ordinary checkout's delete-on-start policy.

For an in-process fake identity provider, startup applies the resolved public origin through `WithIssuer()` and uses that same origin for bearer and dashboard authorities and registered callback URLs. The provider runs inside the application on the resolved HTTPS listener. No second identity-provider process is launched. The hosted browser client receives the matching public configuration before authentication initialization.

An external identity provider retains its externally configured issuer. Such an integration requires valid per-instance registered callbacks and explicit application integration. The resolver must not replace an external authority with localhost or claim support for automatic remote identity-provider administration.

The resulting configuration flow is:

```text
Normal project or IDE launch
    -> read baseline application configuration
    -> check eligibility and detect the linked worktree
    -> allocate or reuse its persistent slot
    -> derive URLs, declared resource names, and authentication values
    -> apply one resolved context before dependent registrations
    -> initialize owned resources and start the listener
```

An ordinary checkout follows its existing configuration and initialization path. The worktree context does not replace its port, catalog, or identity settings.

1. Identify the catalog application and detect whether the host is in a linked local-development worktree. Otherwise continue normal application startup unchanged.
2. Resolve Git identity, create or recover the persistent assignment under the registry lock, and read normal configuration and secrets.
3. Derive and validate the immutable context and apply its managed settings.
4. Acquire the application lease and verify port availability and the localhost certificate.
5. Validate database and filesystem ownership. Provision absent resources only after validation.
6. Register consistent authentication and browser-client configuration.
7. Initialize schema and run idempotent seeds without deleting existing data.
8. Start the listener, publish verified runtime status, and report readiness.

Configuration derivation occurs early enough for host construction. Ownership verification and provisioning may occur in ordered startup services before the application becomes ready. A failed validation must not leave the host advertising readiness.

## Error contract

Errors identify the application and slot when available. Include a recovery action and return a nonzero process exit code. Machine-readable status must use a stable error code.

| Code | Condition | Required outcome |
|---|---|---|
| `worktree_detection_failed` | Linked-worktree metadata cannot be resolved reliably | Fail without connecting to the ordinary database. |
| `slots_exhausted` | All managed slots are claimed | Report how to inspect and release unused assignments. |
| `assignment_invalid` | Corruption, copied identity, or unsupported version | Preserve files and identify the disagreement. |
| `slot_in_use` | Slot belongs to another worktree | Report its canonical path. |
| `registry_locked` | Lock timeout | Report the registry path without modifying it. |
| `instance_running` | Application lease unavailable | Report verified session details. |
| `port_in_use` | Assigned port unavailable | Report port and verified owner if accessible. |
| `configuration_conflict` | Managed settings disagree | Report key names and sanitized expected values. |
| `resource_not_owned` | Missing or mismatched ownership | Leave the resource unchanged. |
| `schema_incompatible` | Existing schema cannot be initialized safely | Explain reset or migration options. |
| `reset_incomplete` | Some owned targets could not be removed | List completed and remaining targets. |

Unknown or inaccessible process owners remain unknown. Never infer permission to stop them.

## Implementation boundaries and compatibility

Implement the resolver and launcher without a new general-purpose infrastructure framework. Reuse existing configuration, logging, result, and host integration conventions where they fit. Public C# APIs require XML documentation and usage examples. Follow `.editorconfig`, use braces for conditionals, and preserve the repository's member spacing.

Do not automatically enable isolation for production hosts, libraries, or arbitrary consumers of `DevKitWebApplication`. Applications adopt it through explicit public registration. The repository's two development applications are the first consumers of that registration. Existing integration tests and container-based fixtures retain their configuration and lifecycle without new opt-out settings, changed connection strings, or interactive slot claims. Generic database creator and Testcontainers defaults must not change as part of this feature.

Tracked launch settings remain useful editor entry points, but must not override the resolved URLs. Existing REST request files need selectable instance origins or generated local environment settings. Do not rewrite their tracked base URL on every launch.

The initial rollout requires no action for ordinary branch-based development. Preserve its existing fixed port 5001, database catalogs, authentication configuration, and startup lifecycle. Linked worktrees allocate slots automatically on first startup and use separate catalogs. Explicit reset replaces delete-on-start behavior only in worktree mode. A failure in worktree detection or allocation must never reconnect to the ordinary database as a fallback.

## Verification plan

Use temporary Git repositories, disposable SQL Server catalogs, and OS temporary directories. Tests own every resource they create. Tests must not delete a developer's current database, change their slot registry, or terminate their hosts.

| Area | Required proof |
|---|---|
| Detection | Ordinary primary checkout, branch switch, detached HEAD, linked worktree, submodule `.git` file, source archive, and unreadable metadata select the required mode. |
| Git invocation | Paths containing spaces remain literal arguments. Missing Git, nonzero exit, timeout, and invalid output follow the detection error policy. Git is invoked only during eligible startup, with no remote access. |
| Ordinary checkout | Startup preserves configured port 5001, catalog names, authentication, and initialization behavior without creating assignment files. |
| Formulas | All forty-nine managed slots and catalog entries produce unique ports in valid ranges and stable names. |
| Assignment | Duplicate claims, copied files, branch switches, moved paths, and corrupt registries behave as specified. |
| Registry concurrency | Concurrent first starts for one worktree share one assignment. Different worktrees receive different slots. Explicit duplicate slot requests produce one winner. |
| Allocation recovery | A crash between the registry commit and local-file write recovers the same assignment. Exhaustion fails without fallback. |
| Cross-platform paths | Windows case handling, Linux case handling, spaces, and separate linked-worktree Git directories work. |
| Launch parity | Run, watch, editor, and direct host startup resolve the same context. |
| Port conflict | A disposable listener causes explicit failure without fallback or process termination. |
| Lifetime leases | Duplicate starts fail. Crashes release leases. Ordinary watch children reuse the assignment and acquire the lease before initialization. |
| Database isolation | Two worktrees seed and change independent catalogs on one SQL Server. |
| Preservation | Restart preserves an inserted sentinel row and an uploaded payload. |
| Ownership | Missing or mismatched database markers and file markers refuse startup mutation and reset. |
| Reset race | Start cannot acquire the lease while reset validates or mutates resources. |
| Reset retry | Partial failure reports targets accurately and a retry converges without touching foreign resources. |
| Path safety | Symlink and reparse-point escapes are refused before recursive deletion. |
| Authentication | Two instances log in independently in one browser profile. Signing out of one preserves the other. |
| Identity metadata | Issuer, discovery endpoints, callbacks, and public browser configuration agree with each origin. |
| External identity | An externally configured issuer remains unchanged. Unsupported callback integration fails explicitly without remote administration. |
| Secret handling | Status, public config, errors, and logs omit credentials and private key material. |
| Build and tests | Existing OpenAPI generation, container-based tests, and integration fixtures run unchanged from both ordinary checkouts and linked worktrees. Their ports, connection strings, database lifecycle, and cleanup remain fixture-controlled. They claim no slots and create no interactive resources. |
| Consumer adoption | A fictional consumer application outside the sample projects adopts the public packages and registrations without repository scripts or sample references. |
| Opt-in compatibility | Unregistered applications and generic DevKit host consumers remain unchanged, including when their sources reside in linked worktrees. |
| Optional dependencies | Host isolation works without SQL Server references. Declared unsupported resources fail explicitly. |
| Context and status | Startup and DI observe the same immutable context. Sanitized, versioned status reports mode, resources, and ownership without secrets. |
| Compatibility | Non-development hosts retain their original configuration and do not read the Git registry. |
| Coexistence | An ordinary host on port 5001 and linked-worktree hosts run together. Worktree reset never changes the ordinary database. |

Run real browser acceptance checks for the dashboard and hosted browser client on both Windows and Linux. Use at least two worktrees and two applications. Keep process ownership evidence outside `docs/`. Stop only hosts started by the verification and confirm their ports are released.

Performance acceptance concerns correctness of isolation, not a throughput threshold. Any later throughput report must identify competing workloads because the SQL Server and hardware remain shared.

## Suggested implementation sequence

1. Define public registration, instance-context, and resource-adapter contracts with package boundaries. Add automatic startup mode detection, the application catalog, pure derivation rules, and ordinary-checkout regression tests.
2. Add first-start allocation, assignment persistence, crash recovery, atomic registry updates, and multiprocess locking tests.
3. Integrate early host resolution and lifetime leases for native run, watch, and IDE startup.
4. Add database and filesystem ownership, preservation, reset, and recovery tests.
5. Apply resolved identity URLs, cookie names, key-ring isolation, and browser public configuration.
6. Wire run, watch, status, stop, configure, release, and workspace launch tasks.
7. Verify external consumer adoption, complete Windows and Linux acceptance runs, and deliver the consumer guide specified below under `docs/`.

The feature is complete only when every participating host detects linked worktrees automatically, ordinary-checkout development remains unchanged, worktree restart preserves data, and concurrent browser sessions pass. Requiring a separate launcher or manual slot assignment for normal first startup does not satisfy this specification.

## Documentation deliverable

Write the adoption guide as part of implementation, after the public API and package boundaries are established. Do not publish proposed signatures as working APIs in the planning phase. The intended location is `docs/features-presentation-worktree-development.md`, linked from the documentation index and the host guide when it exists.

The guide must serve developers building their own DevKit applications, not only repository contributors. Use fictional, self-contained applications and real shipped APIs. Do not reference project READMEs or rely on sample-project source paths for required steps.

Required content:

1. Explain explicit application opt-in, automatic linked-worktree selection, and the absence of AI-agent detection or extra permissions.
2. Identify required and optional packages, supported host types, Windows and Linux support, and the initial adapter support matrix.
3. Provide one complete web-host registration using normal configuration, a stable application ID, deterministic ports, and declared resources. Show the required order relative to modules and authentication.
4. Contrast unchanged ordinary-checkout startup with first and subsequent linked-worktree starts. Explain persistent slot allocation, reserved port blocks, conflict errors, and optional explicit slot selection.
5. Show SQL Server catalog mapping and ownership, filesystem roots, cookie names, authorities, callbacks, data-protection configuration, and hosted browser-client public configuration.
6. Explain consuming the immutable instance context and sanitized status from application integrations or developer tools. Include versioned machine-readable output without secrets.
7. Describe the supported resource-adapter extension contract, intentionally shared resources, and isolation limits. Never imply that undeclared external resources are isolated automatically.
8. Cover status, verified stop, explicit reset, release, incomplete provisioning, crash recovery, and safe handling of stale assignments. Separate automatic startup from optional management tooling.
9. State that existing container-based tests, integration fixtures, build-time generation, and production configuration retain their lifecycle without new opt-out flags.
10. Include troubleshooting for port conflicts, identity redirects, unsupported resources, registry disagreement, database ownership failures, and exhausted slots.

Validate guide snippets against shipped public APIs and test the documented adoption path in an independent fictional consumer host. The guide is a completion requirement, not a substitute for automated compatibility tests. No guide is added during this specification-only task.

## Design alternatives

- Random ports reduce assignment work but make addresses and identity callbacks unpredictable. They are excluded by requirement.
- Branch-name or path hashing avoids a registry but needs collision handling and changes identity when paths or branches change. Persisted sequential slots provide stable addresses.
- A SQL Server per worktree isolates engine resources but adds service startup time and memory use. Separate catalogs meet the current data-isolation requirement.
- Hostnames and a local reverse proxy could isolate browser origins further. They add DNS, certificate, and proxy setup that is unnecessary for the first implementation.

## Related documentation

- [DevKit web host specification](./spec-presentation-devkit-web-host.md)
- [Presentation configuration](../features-presentation-configuration.md)
- [Fake identity provider](../features-identityprovider.md)
- [Dashboard feature guide](../features-presentation-dashboard.md)
- [Startup tasks](../features-startuptasks.md)
