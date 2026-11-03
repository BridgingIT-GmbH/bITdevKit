---
title: Worktree Development Isolation
status: Implemented (preview)
---

# Worktree Development Isolation

This specification defines predictable ports and isolated local resources for concurrent Git worktrees. The first implementation ships the bootstrap types, configuration keys and `bdk dev` commands described below. [Worktree Development Isolation](../development-worktrees-support.md) is the consumer guide. Outcomes that differ from the original proposal are listed under [Implementation outcomes](#implementation-outcomes).

The application detects a linked Git worktree during startup. An ordinary checkout keeps its configured URLs, including the existing HTTPS port 5001, and its existing database catalogs. Working on a different branch in that ordinary checkout does not enable isolation. No slot configuration or special launcher is required for normal development.

The requirements define scope, data, failure behavior, dependencies, and acceptance tests. Packaging validation, the template update and the Linux and two-worktree acceptance runs are still open.

## Scope

Developers and coding agents must be able to start multiple development hosts without interrupting another checkout or changing its data. Windows and Linux are required. The initial SQL Server adapter uses the existing database service and credential sources. Core host isolation does not require a database provider.

This specification defines a default-on DevKit capability for applications built with the kit. It supports developers and coding agents running independent application instances from linked worktrees. The repository's two web development applications (dofiesta/weatherfiesta) are the first adopters, not the boundary of the public feature. The fictional applications TaskHarbor and ForecastHub illustrate consumer applications throughout this specification.

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
- Startup that applies each application's configured database lifecycle only to its own instance catalogs, plus explicit reset and status.
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

### Existing infrastructure to extend

The following components already exist and must be extended rather than duplicated:

| Component | Current behavior | Use in this feature |
|---|---|---|
| `DevKitWebApplicationBuilder` | Wraps `WebApplicationBuilder`. Exposes a shared `Properties` bag, evaluates `DevKitLocalToolingPolicy`, and owns `Build()`. | Registration point, context storage in `Properties`, and final configuration validation in `Build()`. |
| `DevKitLocalToolingPolicy` | Enables local tooling only for eligible local development, with an explicit test override. | Pattern for the eligibility evaluation. The isolation decision is a separate, immutable result that also appears in startup diagnostics. |
| Host runtime descriptors (`HostRuntimeDescriptor`, `HostRuntimeDescriptorWriter`, `HostDescriptorLifecycleService`) | Writes a versioned JSON descriptor per running host to the per-user registry (`%LOCALAPPDATA%\bdk\hosts\runtimes`, or `$XDG_RUNTIME_DIR/bdk/hosts/runtimes` with a temporary-directory fallback). Readers ignore unknown fields. | Carries the runtime record. Add an optional `devAppInstance` object instead of creating a second runtime registry. |
| `bdk` CLI (`bdk hosts list/current/select/clean/kill`, `bdk host run`) | Discovers hosts per workspace, using the nearest `.slnx`, `.sln`, or `.git` ancestor. Each linked worktree is already a distinct workspace. | Hosts the optional management commands as a new `bdk dev` command group. |
| `EnvironmentExtensions` | Provides `IsLocalDevelopment()`, `IsContainerized()` (`DOTNET_RUNNING_IN_CONTAINER`), `IsCloud()`, and `IsBuildTimeOpenApiGeneration()`. `IsLocalDevelopment()` also returns `true` during build-time OpenAPI generation. | Input to the eligibility rule. Do not use `IsLocalDevelopment()` alone. |
| Dashboard authentication registration | Uses the fixed cookie `.Bdk.Dashboard` and exposes `ConfigureCookie` and `ConfigureOpenIdConnect` hooks. | Cookie name and authority are applied through the existing hooks. |
| Fake identity provider | Defaults `Issuer` to `https://localhost:5001`. Uses the fixed cookie `.Bdk.FakeIdentityProvider` without a configuration hook. Client registrations accept absolute redirect URIs. | Issuer comes from the resolved origin. Add a narrowly scoped cookie configuration hook. See [Authentication and public URLs](#authentication-and-public-urls). |
| Serilog `ConfigureLogging` | Builds the logger in a deferred host callback that reads the final host configuration. | Deferred sinks observe managed values. Eager reads still require early registration. |
| Database creator and migrator `DeleteOnStartup(bool)`, `EnsureTruncated` | Callers pass a development flag, for example `DeleteOnStartup(environment.IsLocalDevelopment())`. The registration extensions pass plain option instances to the services, not `IOptions<T>`. Startup runs delete, then truncate, then create. | No change. Managed mode keeps the caller's delete, truncate, create, and migrate options. They act on the instance catalog because the catalog is replaced before EF Core registration. Generic defaults do not change. See [Database naming](#database-naming). |

ASP.NET Core already sets the default data-protection application discriminator to the content root path. Separate worktrees therefore already use distinct protectors, and the antiforgery cookie name suffix derives from that discriminator. OpenID Connect correlation and nonce cookies already carry unique suffixes. Browser storage used by the hosted browser client's OpenID Connect library is origin-scoped, including the port. The remaining shared state is the named authentication cookies, the key-ring location when it is configured explicitly, and the issuer and callback configuration.

### First-adopter preconditions

The first adopters currently contain values that would defeat isolation if left unchanged. Adoption is incomplete until each one is addressed:

- One first adopter creates its host with `WebApplication.CreateBuilder(args)`. It must move to `DevKitWebApplication.CreateBuilder(args)` before it registers the feature.
- Fake identity-provider client registrations hardcode `https://localhost:5001` login, logout, and API documentation callbacks. They must derive from the resolved origin.
- The hosted browser client reads its authority from a tracked static `wwwroot/appsettings.json` that names port 5001. It must use a relative authority.
- Configuration-driven Serilog database sinks carry their own connection strings that name the ordinary catalog. These sinks are hidden database connections. See [Logging sinks](#logging-sinks).
- Composition roots read `Authentication:Authority` and similar values eagerly through `builder.Configuration[...]`.
- Launch settings set `ASPNETCORE_URLS` to ports 5001 and 5000. Workspace tasks, repository agent guidance, and REST request files also assume port 5001.
- Core modules call `DeleteOnStartup(environment.IsLocalDevelopment())`.
- The repository `.gitignore` does not ignore `.dev/`.

### External adopters

Most consumers live outside this repository. They reference DevKit only through NuGet packages, install `bdk` as a .NET tool, and are often generated from the published solution template (`BridgingIT.DevKit.Templates`). Everything an adopter needs must therefore ship in packages. No step may depend on files, scripts, tasks, or guidance from this repository.

The template's web host was reviewed as the reference external adopter. Its shape is typical, and each finding below is a requirement for every external adopter:

- It already uses `DevKitWebApplication.CreateBuilder(args).AddConfiguration()`, so the registration goes directly after `AddConfiguration()`. No builder migration is needed.
- Its module connection string lives at `Modules:<Module>:ConnectionStrings:Default`, not under `ConnectionStrings:*`. A configuration-driven SQL Server logging sink names the same catalog. Declarations must accept arbitrary configuration keys and map both keys to one resource.
- Its migrator calls `DeleteOnStartup(environment.IsLocalDevelopment() || environment.IsContainerized())`. In managed mode this deletes only the [instance catalog](#database-naming), without caller changes.
- App-owned extension methods compose the fake identity-provider issuer, client redirect URIs, the dashboard authority, and the API documentation OAuth redirect from `Authentication:Authority`. The managed source rewrites that key before these methods run, so they pick up the resolved origin unchanged, provided the registration precedes them. Validation still rejects any remaining absolute loopback callback on another port.
- Local CORS origin lists name port 5001. The host's own origin is same-origin and needs no entry. Separate front-end development servers keep their own ports and are not managed by this feature.
- Its OpenTelemetry setup is app-owned, with a fixed service name and a shared exporter. It stays unchanged in linked worktrees.
- Its launch settings contain a project profile with `launchBrowser: true` on port 5001 and an IIS Express profile. See the IIS hosting rule under [Read Git metadata during startup](#read-git-metadata-during-startup).
- It ships a design-time `SqlServerModuleDbContextFactory<T>` for `dotnet ef`. That path bypasses the host. See [Design-time database tools](#design-time-database-tools).
- Its integration tests use a non-`Development` test environment and need no change under eligibility condition 3.
- Its DevKit local tooling and MCP runtime are discovered per workspace, and each linked worktree is already its own workspace, so an agent in a worktree sees only that worktree's host.

Once the packages ship, the solution template adopts the feature, because the registration is inert outside linked worktrees. Every generated application includes it. There is no template option, because an option would double the template's test matrix. An adopter who does not want it removes the registration call or sets `DevAppInstance:Mode=Disabled`. That change lands in the template's own repository and includes the registration, resource declarations for its module catalog and logging sink, origin-relative local callbacks, and `.dev/` in its `.gitignore`.

## Decisions

### D1: Automatic worktree detection and persistent slots

Each linked worktree owns one integer slot. On first startup, the resolver assigns the lowest unclaimed slot under the registry lock and persists it. Assignments persist across process restarts, branch switches, and application launches. Startup never chooses a new slot because a port is busy. An optional command can request a particular slot before first startup.

This makes addresses recognizable without a manual setup step. Allocation uses a fixed sequence, not random numbers, branch hashes, or scanning for available TCP ports. The ordinary checkout does not participate in slot allocation.

### D2: One configuration resolver

A reusable DevKit development bootstrap resolves application identity, worktree identity, URLs, and resource names. Applications explicitly enable the host integration and supply their own application and resource declarations. All launch paths consume that resolver. Keep sample-specific conventions outside the public feature's defaults.

Use one .NET implementation for detection, validation, and derivation in application startup. Optional management commands extend the existing `bdk` CLI as a `bdk dev` command group and reuse the same .NET contracts. Do not add a repository PowerShell launcher. Ordinary `dotnet run`, `dotnet watch`, and IDE launches must work without the CLI. Do not duplicate port or database formulas in shell scripts, CLI code, and host projects.

### D3: Shared SQL Server, separate databases

Retain each configured server, authentication mechanism, and connection options. Change only the database catalog for managed local development. Separate databases isolate schema changes, jobs, queues, auditing, and storage records that use those catalogs.

### D4: The feature deletes resources only through explicit operations

The feature itself never deletes resources during startup. Database lifecycle stays under the control of the application's creator and migrator options. Those options act on the instance catalog, so `DeleteOnStartup(true)` recreates the worktree's own database and never touches the ordinary one. Files are preserved. Reset requires a separate command, a confirmed instance, and an exclusive application lease. Releasing a slot never deletes its databases or files. The ordinary checkout retains its existing configured initialization and seed behavior. This feature does not redesign that lifecycle.

### D5: Keep the Development environment

Isolation does not require a new ASP.NET Core environment. Do not set container or cloud environment flags to suppress database deletion. Configure database initialization and reset directly.

### D6: Default-on DevKit capability with separate resource adapters

Place reusable contracts and host integration in appropriate DevKit packages. Keep sample registration in sample composition roots. The core resolver owns detection, slots, deterministic naming, and instance context. Resource adapters own provider-specific connection rewriting, provisioning, and reset.

The first persistence adapter supports the SQL Server behavior specified below. Filesystem integration covers the feature's own log and key-ring roots. Application file paths are not rewritten. Consumers that do not use SQL Server must be able to adopt host and URL isolation without taking a SQL Server dependency. Other database engines and cloud resource adapters are extension points, not implied first-release implementations.

The feature makes applications suitable for concurrent agent-driven development through predictable addresses, isolated declared resources, and machine-readable status. It does not infer agent identity, bypass authentication, grant reset permission, or automatically expose an HTTP or MCP administration endpoint.

### D7: Baseline URL settings are replaced, explicit endpoint configuration conflicts

Launch settings supply `ASPNETCORE_URLS` as an environment variable. At runtime that value cannot be distinguished from a deliberate caller override. In managed mode, treat the generic URL inputs as baseline values: `urls`, `ASPNETCORE_URLS`, `--urls`, `http_ports`, `https_ports`, `https_port`, and their `ASPNETCORE_`/`DOTNET_` variants. Replace them with the resolved URLs and log the replaced values at Information level. Explicit `Kestrel:Endpoints` entries and `DevAppInstance:*` values that disagree with the assignment fail with `configuration_conflict`. `UseUrls` and `UseSetting` calls made after registration write through the configuration manager, so the `Build()` comparison of managed keys detects them. Code-level `Listen*` calls inside `ConfigureKestrel` are not observable through public API before the server starts. A lifecycle check therefore compares `IServerAddressesFeature.Addresses` with the resolved URLs when the application starts. On a mismatch it logs a `[BDK]` warning with `configuration_conflict` and keeps the host running. Hosted services that run before the server binds already use instance-scoped resources, so only the listener address is at risk.

This keeps existing launch profiles, workspace tasks, and documented `dotnet run --urls` commands usable from a linked worktree without editing them. It still rejects endpoint configuration that an application owns deliberately.

### D8: Require the DevKit web builder

Host registration is an extension on `DevKitWebApplicationBuilder`. The feature does not provide a plain `WebApplicationBuilder` overload. The DevKit builder already owns the properties bag, the eligibility diagnostics, the runtime descriptor, and the `Build()` hook that validation needs. A first adopter that uses `WebApplication.CreateBuilder(args)` migrates to `DevKitWebApplication.CreateBuilder(args)` as part of adoption.

### D9: Management commands belong to the `bdk` CLI and descriptor registry

Status and stop read the existing host runtime descriptors. The descriptor gains an optional, additive `devAppInstance` object. Existing readers ignore it, so the descriptor schema version does not change. Configure, release, and prune are `bdk dev` commands that use the same assignment, registry, and lease contracts as startup. Reset runs adapter code inside the application host in a one-shot reset mode, so the CLI needs neither provider packages nor application secrets. Consumers obtain the commands through the existing `bdk` tool distribution and its documented version compatibility. They do not need repository scripts.

## Public adoption and extension contracts

These are required capabilities of the future API. Final symbol names and package placement must follow the existing DevKit builder conventions during implementation.

### Host registration

Every `DevKitWebApplicationBuilder` activates the feature with default settings. Activation runs lazily at the first configuration consumer: `AddLogging`, `AddModules`, a `Add<Provider>DbContext` registration, `GetDevAppInstance` or `Build()`. `AddConfiguration()` does not activate it, so the managed source sits on top of the composed configuration. The optional fluent registration `AddDevAppInstance(...)` changes the defaults or turns the feature off with `Enabled(false)`. It must be called after `AddConfiguration()` and before the first consumer. A call after activation fails with `InvalidOperationException`. Configuration read by custom code before activation sees the ordinary values; an explicit `AddDevAppInstance()` call activates earlier. The gate is the Development environment plus a linked worktree. The feature does not use the `DEBUG` symbol, because DevKit ships as Release packages. Registration must not create a second service provider or require a custom application base class. The registration resolves the context synchronously, so values are available to the remaining composition-root code. It stores the context in the builder `Properties` bag and registers it as a singleton.

The required order is:

```text
DevKitWebApplication.CreateBuilder(args)
    -> AddConfiguration()
    -> optional AddDevAppInstance(...) (configure, or activate early)
    -> AddLogging() / ConfigureLogging(...)  (first consumer activates with defaults)
    -> AddModules(...), authentication, fake identity provider, dashboard, resources
    -> Build() (validate effective values, pin managed source)
```

Applications have a stable application ID and port offsets (see [Application catalog](#application-catalog)), declared or derived, plus resource mappings and any public authentication or client configuration integrations. The existing default port scheme is the initial convention. A consumer may configure a repository-wide base port, block size, and slot count if the complete mapping is validated and all applications using the common registry agree. Changing an established mapping requires explicit reconfiguration rather than silently moving an existing instance.

Building with `DevKitWebApplication.CreateBuilder` is the opt-in; `Enabled(false)` or `DevAppInstance:Mode=Disabled` opts out. `Auto` selects linked-worktree behavior only for eligible local development. Ordinary-checkout, production, test, build-time, and container-hosted paths retain the compatibility rules in this specification. The sample applications rely on the same default activation, with no private alternate resolver.

### Resolved instance context

Expose a documented, immutable instance context for startup consumers and dependency injection. It reports whether isolation is active, the selection reason, application identity, and the resolved values needed by declared integrations. In inactive mode it must not fabricate a worktree ID or slot. Provide access early enough to configure authorities, callback URLs, and resource registrations without repeating Git detection.

Separate the full local context from the sanitized status projection. Credentials and signing material are never context diagnostics.

### Resource declarations and adapters

Database adapters come with the DevKit EF Core provider packages (SQL Server, PostgreSQL, SQLite). In the Development environment activation loads the provider assemblies the host references and reads their `DevAppInstanceDatabaseAdapter` assembly attributes, so no explicit call is needed. `WithDatabases(adapter)` and the wrappers `WithSqlServerDatabases()`, `WithPostgresDatabases()` and `WithSqliteDatabases()` declare an adapter explicitly. Declared adapters run first, then SQL Server, PostgreSQL, SQLite and other attributed assemblies already loaded; the first adapter that accepts a key claims it. `Add<Provider>DbContext` also checks its connection string. When it brings an adapter kind the session lacks, the host plans that database late and applies the managed values; readers that ran earlier keep the ordinary value. Each adapter isolates every connection string of its provider that it discovers in configuration. Discovery considers keys whose path contains `ConnectionString`, case-insensitive, at any depth, so `Modules:<Module>:ConnectionStrings:Default` and `Serilog:WriteTo:<n>:Args:connectionString` qualify. Keys that point at the same server and database form one resource, so a logging sink shares the module database it logs into. Writable paths and other resources still need an explicit declaration that names its resource ID, adapter and source key. Keys declared with `ShareConnectionString(key)` stay unchanged.

Adapters must support planning the target, validating ownership where the adapter records it, initializing absent resources where supported, and explicit reset with the existing lease and confirmation rules. Recoverable failures use established DevKit result conventions. Initialization and reset support cancellation, bounded operations, and retry after partial failure. Adapters must not log credentials.

An unsupported provider must fail registration or validation when isolation for that resource is requested. Never report a resource as isolated merely because its name was displayed. Intentionally shared resources must be declared and identified as shared in status. An undeclared resource is not covered by the isolation guarantee.

Undeclared resources are not rewritten, but managed mode guards against reaching ordinary data through them. At `Build()`, an adapter that supports it scans the effective configuration for values that target any managed resource's ordinary baseline. A database adapter treats a value as a match when it parses as a connection string of its provider with the same server and database as a managed baseline. This catches keys outside discovery, such as `HealthChecks:Sql`. A match that is neither managed nor marked shared fails with `configuration_conflict`. The failure names the configuration key, never the value.

Core contracts must not depend on Infrastructure implementations. Database adapters live in their owning infrastructure packages or other suitable optional integrations. The host composition root connects them. Application and Domain layers remain independent of these adapters.

### Optional management tooling

Status, reset, configure, release, and prune use the same public context, registry, and resource contracts as startup. A consumer must not require scripts from this repository to start its application. Management commands ship as the `bdk dev` command group in the existing `bdk` tool, following that tool's distribution and version-compatibility rules. The command forms later in this specification define the intended workflows.

The CLI never loads application assemblies and never reads application secrets. Each command uses only the data it needs:

| Command | Data source |
|---|---|
| `status`, `configure` | Registry, including recorded catalog entries, local assignment file, runtime descriptors, and the sanitized resource inventory each host writes to its state root at startup. |
| `stop` | Runtime descriptors and process verification. |
| `release` | Registry, leases, and each application's persisted resource inventory. Reset updates that inventory. |
| `prune` | Registry, `git worktree list`, runtime descriptors, and the inventory copy kept in the registry. Dropping orphaned databases runs through the reset mode below, started from the current checkout. |
| `reset` | Runs inside the application's own composition root. `bdk dev reset` starts the application project through its supported launch path with a reserved reset argument and the confirmation token. The host resolves the context, acquires the lease, builds the service provider, and runs adapter reset instead of starting the listener. It then exits with the result code. This reuses the application's configuration, secrets, and declared adapters. |

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

Resolve the containing checkout from the host content root, not an arbitrary shell working directory. Branch name, detached HEAD state, and a directory name containing `worktree` are not detection rules. A `.git` file alone is not a detection rule either, because submodules and `--separate-git-dir` checkouts also use one.

### Read Git metadata during startup

One detector in the common implementation assembly serves application startup, the design-time database factories, and the `bdk dev` commands. It reads Git's documented on-disk repository layout (`gitrepository-layout`) and starts no process in the normal case:

1. Walk up from the start directory to the nearest `.git` entry. For a host this is the content root. For a design-time factory it is the factory's base path. No `.git` entry up to the filesystem root means outside Git.
2. A `.git` directory is an ordinary checkout. Its Git directory and common directory coincide.
3. A `.git` file must contain one `gitdir: <path>` line. Resolve that path relative to the file's directory.
4. The checkout is a linked worktree only when the target Git directory contains a `commondir` file and a `gitdir` file. The `gitdir` file must resolve to this checkout's `.git` file. Resolve `commondir` relative to the target Git directory to obtain the shared common directory.
5. A `.git` file whose target has no `commondir` is a submodule or separate-Git-directory checkout, and is ordinary. A linked worktree of a submodule has `commondir` and is a linked worktree whose common directory lies under the superproject's `modules` directory.

Detection follows the on-disk layout only. It ignores `GIT_DIR`, `GIT_WORK_TREE`, and `GIT_COMMON_DIR`, so the result does not depend on the launching shell. Normalize all resolved paths, compare them case-insensitively on Windows, and resolve symbolic links before comparing.

The files can be inconclusive: an unparseable `.git` file, a missing or unreadable target directory, or a `gitdir` back-pointer that names another location, as after a moved worktree. In those cases startup and the `bdk dev` commands ask Git once, through .NET's `Process` API, with structured argument lists, captured output, exit-code checks, a five-second timeout per command, and the Git environment variables above cleared for the child process. Never construct a shell command from an application path. Spaces and special characters in paths must remain literal arguments. The read-only commands are:

```text
git -C <start-directory> rev-parse --show-toplevel
git -C <start-directory> rev-parse --absolute-git-dir
git -C <start-directory> rev-parse --path-format=absolute --git-common-dir
```

Git confirms a linked worktree when the per-worktree and common directories differ and the top level matches the checkout. Design-time factories never start Git.

These reads do not contact a remote, fetch commits, use Git credentials, or modify the repository. Cache the resolved context once per host startup. Do not read Git metadata per request or storage operation.

A location outside Git, a conclusive ordinary checkout and a conclusive linked worktree need no Git executable and start no process. The fallback stops at the first failed command, so a missing or hanging Git costs at most one timeout. What happens next depends on the evidence:

- **No linked-worktree evidence.** The `.git` file is unreadable, has no `gitdir:` line, or points to a missing directory outside a `worktrees` folder. Git itself cannot use such a folder. When Git does not confirm a checkout, the result is outside Git with a warning, the host starts unchanged, and the warning appears in the startup log.
- **Linked-worktree evidence.** The `.git` file points to `<common>/worktrees/<name>`, or the worktree metadata is incomplete or has a stale back-pointer. If Git is missing, fails, times out, or disagrees, report `worktree_detection_failed` before database initialization. The host then falls back to its configured defaults and says so in the log, as for every activation failure. The recovery names `git worktree repair`, `DevAppInstance__Mode=Disabled`, and `--connection-string=` for design-time tools, without changing Git configuration.

Evaluate application eligibility, test-host execution context, build-time execution, and container hosting before linked-worktree detection or registry access. Any necessary guard belongs in application-side bootstrap integration, without changing existing fixture callers. A linked source directory alone never enables isolation in a test host. New resolver tests may explicitly exercise managed startup against disposable resources.

Git detection runs only when all of the following eligibility conditions hold. Otherwise the context is inactive and reports the first failed condition as its reason:

| Order | Condition | Existing mechanism | Inactive reason |
|---|---|---|---|
| 1 | The host uses the DevKit web builder, which activates the feature by default, and neither `Enabled(false)` nor `DevAppInstance:Mode=Disabled` turns it off. | Builder and configuration | `not_registered`, `disabled` |
| 2 | The process is not build-time OpenAPI generation. | `IsBuildTimeOpenApiGeneration()` | `build_time` |
| 3 | The host environment is `Development`. | `IHostEnvironment.IsDevelopment()` | `non_development` |
| 4 | The process is not containerized or cloud-hosted. | `IsContainerized()`, `IsCloud()` | `container`, `cloud` |
| 5 | The process is not a test host. | Test factories set a non-`Development` environment, for example `UseEnvironment("Testing")`. This is covered by condition 3. | `non_development` |

A failure after all conditions pass reports the reason `fallback` (see the [fallback policy](#error-contract)). Condition 2 precedes condition 3 because `IsLocalDevelopment()` returns `true` during build-time generation. Existing integration factories already select a non-`Development` environment, so they need no new setting. A test host that keeps `Development` and registers the feature must set `DevAppInstance:Mode=Disabled` itself. This narrow case is documented in the guide. It is not a change to existing fixtures. Tests of the feature itself may bypass conditions 3 to 5 through an internal test seam with disposable resources, similar to the `AllowOutsideDevelopmentForTests` pattern in `DevKitLocalToolingPolicy`.

IIS and IIS Express hosting, through the ASP.NET Core Module, is eligible but cannot be isolated, because IIS owns the listener ports. Treating it as inactive would connect a linked worktree to the ordinary catalog. When a linked worktree is detected and the process runs behind the ASP.NET Core Module, which sets environment variables such as `ASPNETCORE_IIS_PHYSICAL_PATH`, startup fails with `unsupported_host` before any resource registration and names the project launch profile as the supported alternative. Ordinary checkouts keep their IIS Express behavior.

### Inactive footprint

Deployed hosts keep the registration call in `Program.cs`, but they always fail eligibility through condition 3 or 4. An inactive context, whatever its reason, registers only the immutable context singleton with `IsActive = false` and its reason. Registration resolves eligibility synchronously and skips everything else:

| Component | Inactive behavior |
|---|---|
| Git detection, registry, assignment file, state root | Not accessed. No files or directories are created. |
| Managed configuration source | Not added. Configuration precedence is unchanged. |
| `Build()` validation and the ordinary-catalog guard | Not run. |
| Lifetime lease and hosted services | Not registered. |
| Resource adapters (database, files, logging, auth, cookies, data protection) | Not applied. Every declaration is ignored, and existing options stay as configured. |
| Runtime descriptor | Unchanged from today. The writer follows `DevKitLocalToolingPolicy` and omits `devAppInstance`. |
| Reset mode argument | Ignored unless isolation is active. An inactive host that receives it exits with a nonzero code and an error log entry, without touching resources or starting the listener. |
| Orphan-reset argument | Accepted only when eligibility conditions 1 to 4 pass and detection resolves a checkout of the same repository as the targeted registry entry, ordinary or linked. Build-time, non-`Development`, container, cloud, and test hosts refuse it like the reset argument. When accepted, registration adds only the orphan-reset runner and the declared adapters' drop operations. The host never activates managed mode, starts the listener, writes a state root, or changes its own configuration. It exits with the result code. |

Declaration APIs are pure data. They must not resolve services, open connections, or read files when they are called. The only runtime cost in production is the eligibility check and one singleton. Registration code needs no `#if` or environment guard.

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

Resolve the shared common directory through the detector described in [Read Git metadata during startup](#read-git-metadata-during-startup), rather than assuming `.git` is a directory. Store a versioned registry at `<git-common-dir>/bdk-dev/instances.json`. Each assignment records the worktree ID, slot, canonical worktree path, and assignment timestamp. It also keeps a copy of each application's sanitized resource inventory: application ID, project path relative to the repository root, sanitized server identity, and generated catalog names. Hosts update that copy at startup and after reset. The worktree-local inventory stays the primary copy. The registry copy exists so that `prune` can still find resources after the worktree directory is deleted.

One installed `bdk` tool version serves the ordinary checkout and all linked worktrees of a repository. Host packages can still differ between branches, so the registry, assignment file, and inventory evolve additively within a version. Readers ignore unknown properties and keep them when rewriting. A host or tool that finds a higher version than it supports fails with `assignment_invalid`, names the minimum package version, and writes nothing.

The registry also records the shared port-allocation policy. Consumers declaring different base ports, block sizes, slot counts, or conflicting application offsets for that registry fail validation before allocation. The numeric limits and formulas below describe the default policy. A customized policy must preserve the same validation, persistence, and conflict behavior.

Registry updates must use an exclusive cross-process lock and atomic replacement. A lock wait exceeding ten seconds fails with the registry path and a retry instruction. Invalid JSON, duplicate slots, or conflicting local assignments must fail without rewriting the registry.

Two applications starting for the first time in the same worktree must converge on one assignment. Two different worktrees starting concurrently must receive different slots. Treat the registry as authoritative and the local file as its persisted worktree copy. If startup stops after committing the registry but before writing the local file, the next startup recovers that same assignment. It must not allocate another slot. A conflicting local file fails explicitly. If all forty-nine slots are claimed, fail with an instruction to run `bdk dev prune` for deleted worktrees, or to inspect and explicitly release unused assignments.

Assignments belong to a worktree, not a running process. Stopping a process does not free a slot. Canonical path comparison follows host filesystem semantics, including case sensitivity. Reject duplicate canonical paths and copied assignment files that claim another worktree's ID.

Slot coordination is per repository. Linked worktrees of different repositories on one machine can hold the same slot and therefore the same ports. Allocation skips ports that are bound at that moment, but it cannot see a stopped host from another repository. When both hosts later run together, the second start fails with `port_in_use`. Recovery is an explicit `bdk dev configure --slot` before first start, or release and reallocation. A machine-wide port ledger is out of scope.

### Application catalog

The catalog has no separate file. Each host declares its catalog entry in code at registration, and every part of it is optional. Without an ID, the host derives one from its application name (entry assembly name) by dropping trailing layer segments such as `Presentation`, `Web`, `Server`, `Api` and `Host` and normalizing the last remaining segment. IDs match `^[a-z][a-z0-9-]{0,62}$`. A derived ID longer than 63 characters is cut and ends with an 8-character SHA-256 hash of the full name. Without offsets, the registry assigns the lowest free pair (`0/1`, `2/3`, ...) on the first managed start and reuses it afterwards. When only one offset is declared, the other is the adjacent port. Database names derive from the configured databases, not from the application ID.

The registry is the shared view of the catalog. On a host's first managed start, the registry records its entry under the registry lock, including the source application name of a derived ID. Later starts compare against the recorded entries. A host with automatic offsets reuses its recorded pair. Two hosts with different application names that derive the same ID fail with `configuration_conflict`, and the error asks for a declared ID. A different application that declares an ID or port offset already recorded for another application fails with `configuration_conflict` before allocation, and the error names both applications. A host whose declared offsets changed after they were recorded fails the same way and names `bdk dev configure --forget-app` as the explicit reconfiguration. `prune` removes an application's recorded entry only when no assignment still references it. Commands such as `status` and `release` read the catalog applications from the registry, because they do not load host assemblies.

The following two entries are illustrative consumer declarations. A repository with several hosts either relies on automatic offsets, which follow the order of first starts per clone, or declares fixed offsets per host. No application is enabled merely because its name appears in the registry.

| Application ID | HTTP offset | HTTPS offset |
|---|---|---|
| `taskharbor` | 0 | 1 |
| `forecasthub` | 10 | 11 |

For slot `s`, the base port is `5000 + 100 * s`. Offsets must remain within that slot's block. Both HTTP and HTTPS ports are checked and bound to loopback only. The 49-slot limit is intentional: ports stay below 10000, clear of common service ports above it, and `prune` reclaims slots that deleted worktrees leave behind.

| Slot | TaskHarbor HTTP and HTTPS | ForecastHub HTTP and HTTPS |
|---|---|---|
| 1 | 5100 and 5101 | 5110 and 5111 |
| 2 | 5200 and 5201 | 5210 and 5211 |

The table applies only to linked worktrees. It does not change either fictional application's ordinary-checkout HTTPS port 5001 or its fixed database catalogs.

HTTPS is the canonical public origin. HTTP redirects to the resolved HTTPS port. The existing trusted localhost development certificate remains in use.

Slot ports can collide with other local services or with OS port reservations. Examples are Seq on 5341, PostgreSQL on 5432, RabbitMQ on 5672, and Windows Hyper-V or WinNAT excluded port ranges (`netsh int ipv4 show excludedportrange protocol=tcp`). A collision with a persistent slot fails on every start, so the check runs during allocation. Before claiming a slot, the allocator computes every catalog port in that slot's block. It skips the slot when any port is in a built-in denylist of well-known development service ports, in an OS-reserved range where the platform exposes one, or not bindable on loopback at that moment. Skipped slots stay free and are reported at Debug level. A later reservation that blocks an already assigned port fails with `port_in_use`. The error says whether the port is OS-reserved and names `bdk dev release` followed by a fresh start as the recovery. Releasing the slot requires that the application has no instance databases, or that they were reset first.

### Resolved development context

The resolver produces an immutable context before host URLs, authentication, module configuration, or storage registrations are consumed. It includes:

- Application ID, worktree ID, slot, and canonical worktree path.
- HTTP URL, HTTPS URL, and canonical public origin.
- Module-to-database catalog mappings.
- Application state, storage, log, and data-protection paths.
- Cookie names and data-protection application discriminator.
- Assignment and resource status.

Do not change ASP.NET Core's application assembly identity to implement this discriminator.

Proposed configuration inputs are `DevAppInstance:Mode` with values `Auto` and `Disabled`, and optional `DevAppInstance:Slot`. Explicitly registered host integrations default to `Auto` for eligible local startup. Applications without that registration retain existing behavior. `Disabled` is an optional administrative override, not a new requirement for fixture or build callers. Existing tests and build-time hosts bypass managed startup through execution-context eligibility. An ordinary checkout in `Auto` remains unchanged. A slot supplied in an ordinary checkout does not activate isolation. It is ignored with a Warning, and an invalid `Mode` outside a linked worktree is handled the same way, so a configuration mistake never blocks an ordinary checkout. Inside a linked worktree an invalid `Mode` or `Slot` logs `configuration_conflict` and falls back to the configured defaults. In Development an inactive host writes one Debug entry with the inactive reason.

In an eligible linked-worktree development host, startup creates a missing assignment automatically. An explicit slot requests that exact slot on first startup and fails if another worktree owns it. For an existing assignment, an environment or command-line slot may agree with the file, but may not silently replace it. Assignment changes belong to `configure`. Non-development environments keep existing application configuration and do not touch the registry. Build-time OpenAPI generation, container-based tests, and integration test hosts retain their existing paths without claiming slots or provisioning worktree resources.

Load normal JSON and secret providers first. In a linked worktree, use the ordinary configured database server and credentials as inputs and derive a separate catalog. Treat tracked fixed URLs, authorities, and catalogs as baseline values to transform, not configuration errors. Apply managed values before dependent registrations. A later provider must not restore those ordinary values. Conflicting caller-supplied overrides for managed settings fail with their key names and expected non-secret values. Outside linked-worktree mode, do not apply those transformations.

### Applying managed configuration

Registration applies managed values as an in-memory configuration source appended to `builder.Configuration`. `ConfigurationManager` applies the source immediately, so eager reads such as `builder.Configuration["Authentication:Authority"]` after registration observe managed values. Deferred consumers observe them too, including Serilog's host logging callback, options binding, and EF Core registration callbacks.

Managed keys include:

- The URL settings named in [D7](#d7-baseline-url-settings-are-replaced-explicit-endpoint-configuration-conflicts), plus `https_port` set to the resolved HTTPS port. This keeps HTTPS redirection on the instance.
- `Authentication:Authority` and any further authority or issuer keys the application declares.
- Every configuration key mapped by a resource declaration, rewritten by its adapter.

Configuration sources added after registration could shadow managed keys. In managed mode, `Build()` first compares every managed key's effective value with the resolved value and fails with `configuration_conflict` on any difference. It then moves the managed source to the end of the source list before the host is built. Composition code that reads a managed key before registration cannot be detected reliably. The guide therefore prescribes the registration order, and the first adopters must follow it.

## User stories and acceptance criteria

All requirements are pending implementation. Each has observable behavior and a failure case.

Unless a requirement explicitly describes ordinary-checkout compatibility, isolation, resource reset, and slot management apply only in linked-worktree mode.

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
6. When a user signs in to one instance's fake identity provider, then another instance's provider still shows its own sign-in state, because each provider uses its own instance-suffixed cookie.
7. When a client registers origin-relative callbacks for local development and absolute callbacks for a deployed environment, then each environment accepts its own callbacks without code changes.

### R4: Apply the configured database lifecycle to instance catalogs

As a developer, I want my application's existing database options to behave in a worktree as they do in the ordinary checkout, so that I choose between recreating and preserving data in one place.

1. When an assigned database is absent, then startup creates it, initializes its schema, and runs idempotent seed tasks.
2. When an instance database exists and the caller enabled delete or truncate, then startup deletes or truncates only that instance catalog before initialization.
3. When an instance database exists and the caller enabled neither, then startup preserves its data. The creator skips table creation and the migrator applies pending migrations.
4. When the caller enabled delete or truncate, then the ordinary checkout's catalog stays unchanged.
5. When a local storage root belongs to a different worktree, then startup fails without changing its contents.

### R5: Reset an instance's resources

As a developer, I want an explicit reset operation so that I can recreate disposable data without affecting another instance. Reset stays necessary when the application uses `DeleteOnStartup(true)`. It is the only operation that clears payload roots, and it covers applications that preserve database data between starts.

1. When `bdk dev reset --app forecasthub --confirm-instance forecasthub:w02` runs, then the application's reset mode first displays and validates the complete target inventory.
2. When the application is running, a catalog is not recorded in the registry inventory for that application and slot, or a payload-root ownership check fails, then reset changes no resource.
3. When all checks pass, then reset removes only that application's instance catalogs and payload roots. The next startup recreates them.
4. When reset partially fails, then it reports completed and remaining targets, returns failure, and supports a safe retry.
5. When reset completes, then the slot, worktree ID, logs, and authentication key ring remain unchanged.

### R6: Inspect and manage local sessions

As a developer or coding agent, I want reliable status and lifecycle commands so that I can operate the intended instance.

1. When `status` runs, then it lists assignments, application URLs, sanitized resource names, and verified running-process information.
2. When run, watch, editor tasks, or direct host startup are used, then they resolve identical settings.
3. When stop is requested, then it stops only the verified process tree belonging to that worktree and application.
4. When a worktree is missing or a PID was reused, then status marks the record stale and stop refuses to terminate an unrelated process.
5. When release is requested for an application with retained resources or a running process, then it refuses to free the worktree slot until every catalog application has been checked and handled explicitly.
6. When a worktree directory was deleted, for example by `git worktree remove`, then `bdk dev prune` lists its slot and orphaned databases. With `--confirm` it drops only the catalogs the registry inventory records for that worktree, then frees the slot. Without confirmation it changes nothing.
### R7: Adopt the feature in a consuming application

As an application developer, I want a supported DevKit integration so that developers and agents can run my application safely in parallel worktrees.

1. Given an application outside this repository, when it explicitly registers the public feature and declares its resources, then its ordinary run and IDE paths obtain the same worktree behavior without sample code or repository scripts.
2. Given an application that has not registered the feature, when DevKit packages are updated, then its existing startup, ports, databases, and tests remain unchanged.
3. When an application adopts host isolation without SQL Server, then the core registration requires no SQL Server package or database adapter.
4. When a declared resource has no supported adapter or conflicts with another application declaration, then validation fails before initialization and identifies the declaration.
5. When implementation is delivered, then the consumer guide uses the shipped public APIs and demonstrates ordinary-checkout compatibility, linked-worktree startup, and unchanged fixture behavior.
6. Given an application generated from the published solution template that references DevKit only through NuGet packages and runs `bdk` as an installed .NET tool, when it adopts the feature, then linked-worktree startup, status, and reset work without files from this repository.
7. Given a linked worktree, when a developer runs DevKit design-time EF Core tooling, then the tooling targets the instance catalog or fails, and never targets the ordinary catalog.

## Detailed behavior

### Authentication and public URLs

The resolved origin is the single source for fake identity `WithIssuer()`, bearer authentication authority, dashboard OpenID Connect authority, registered login and logout callbacks, API documentation OAuth callbacks, and browser-client authority.

The fake identity provider's built-in default issuer, `https://localhost:5001`, must never take effect in managed mode. Registration applies the resolved origin through `WithIssuer()` when the application has not called it. When the application has called it with a different loopback value, startup fails with `configuration_conflict`.

Client registrations currently accept absolute redirect URIs, so composition roots hardcode port 5001. Add support for origin-relative redirect and post-logout URIs, such as `/authentication/login-callback`, resolved against the issuer origin. Absolute URIs remain fully supported in every mode. Deployed environments and clients on a different origin than the provider continue to register absolute URIs. Local development registers origin-relative URIs. A client may register both forms.

The provider compares redirect URIs by exact string match in three places today: the authorize endpoint, the logout endpoint, and the sign-in page. Introduce one shared normalizer that resolves each registered relative URI against the effective issuer origin and use it at all three sites and in discovery or client listings. Matching stays exact after normalization. Wildcards, prefix matching, and Host-header-derived origins are not allowed. When clients come from configuration arrays, the guide warns that environment-specific files merge arrays by index, so a deployed absolute URI list must replace the local relative list completely.

In managed mode, startup validation rejects any absolute loopback redirect URI whose origin differs from the resolved origin (R3.4). Non-loopback absolute URIs, such as deployed environment callbacks registered on the same client, are left unchanged and are not validated. When isolation is inactive, normalization still resolves relative URIs against the configured issuer, and absolute URIs behave exactly as before.

Do not derive the issuer from an unvalidated request Host header. Bind to the known loopback origin. Validate discovery metadata against that origin in acceptance tests.

A hosted browser client is served by the same host that issues its tokens, so its authority is the origin that served it. The client stores a relative authority such as `/` in `wwwroot/appsettings.json` and resolves it against `HostEnvironment.BaseAddress` before `AddOidcAuthentication` uses it. Each slot then reaches its own issuer without a request, endpoint, helper or generated file. The client ID is the same in every worktree and stays static. An absolute authority, such as a shared external identity provider, resolves to itself and is not isolated. The feature maps no browser configuration endpoint and does not rewrite tracked `wwwroot/appsettings.json`.

Append `.<appId>.w<slot:00>` to the name of every cookie authentication scheme, for example `.Bdk.Dashboard.forecasthub.w02`, `.Bdk.FakeIdentityProvider.forecasthub.w02` and `.AspNetCore.Identity.forecasthub.w02`. A scheme without a configured name gets `.AspNetCore.<scheme>` plus the suffix. The session, antiforgery and cookie temp-data cookies get the same suffix. Correlation and nonce cookies keep their names, because each challenge already gets a unique one. Configure host-specific data-protection application identity and a persistent key ring under the application's state root. Do not share authentication tickets between instances. Protect keys using the host's supported storage permissions and key-protection facilities.

The dashboard cookie name is applied through the existing `ConfigureCookie` hook. The fake identity provider sets its cookie name inside its service registration, with no hook. A per-instance provider cookie is required: with one shared `.Bdk.FakeIdentityProvider` cookie on `localhost`, signing in or out of one worktree's provider changes the provider session of every other instance, because browsers do not isolate cookies by port. Add a narrowly scoped cookie-configuration option there, with the current name as its default, and set it to the instance-suffixed name in managed mode. Set the data-protection application name to `<appId>.w<slot:00>` and persist keys under `<state-root>/keys`. Compose with other `AddDataProtection()` callers, such as auditing, through options configuration rather than a competing registration. Distinct names keep one instance's session, antiforgery and sign-in cookies from overwriting another's on `localhost`, even though the protectors already differ.

If current library APIs lack cookie configuration hooks, add narrowly scoped options or use supported options configuration. Do not make production defaults depend on Git or a development slot.

### Database naming

The database adapters discover the connections described under [Resource declarations and adapters](#resource-declarations-and-adapters). Each distinct server and database pair is one resource whose ID is the ordinary database name, with `_2`, `_3` and so on for duplicates. Only the database name changes. Preserve all other connection options and credentials, and never log them.

The naming formula appends the slot to the ordinary database name: `<database>_w<slot:00>`. Example: `forecasthub_core` becomes `forecasthub_core_w02`. Each provider applies it in its own way:

| Provider | Rewritten value | Name limit | Reset |
|---|---|---|---|
| SQL Server | `SqlConnectionStringBuilder.InitialCatalog`, which covers `Database` and `Initial Catalog` | 128 characters | `SINGLE_USER WITH ROLLBACK IMMEDIATE`, then `DROP DATABASE` through `master` |
| PostgreSQL | `NpgsqlConnectionStringBuilder.Database` | 63 UTF-8 bytes | `DROP DATABASE ... WITH (FORCE)` through the `postgres` database |
| SQLite | `Data Source`, with the suffix before the file extension: `data/forecasthub_w02.db` | none | Deletes the file and its `-wal`, `-shm` and `-journal` files |

A name over the limit fails with `configuration_conflict` before connecting. SQLite in-memory databases and `file:` URIs are not discovered. When an application registers several database adapters, the first registered adapter claims a key.

The generated name identifies an instance catalog. Startup neither records nor checks database ownership metadata. A database that already carries the generated name is treated as the instance catalog. Reset and prune target only exact catalog names that the registry inventory records for that application and slot. They never use name patterns and never target the ordinary catalog. Existing unsuffixed development databases remain untouched. Data import or adoption requires a separate future workflow.

Managed mode keeps the caller's `DeleteOnStartup`, `EnsureTruncated`, creator, and migrator options. Because the catalog was replaced before EF Core registration, they act on the instance catalog only. A creator without delete skips table creation for an existing database, so a branch that changes the EF model uses `DeleteOnStartup(true)` or the migrator service. This is the same choice an ordinary checkout makes. Keep test fixture lifecycle separate from interactive host lifecycle. Neither startup nor worktree reset may target the ordinary checkout's catalog.

Callers pass `DeleteOnStartup(environment.IsLocalDevelopment())`, which evaluates to `true` in a linked worktree. `DatabaseCreatorService<TContext>` and `DatabaseMigratorService<TContext>` do not change. They operate on whatever catalog the rewritten connection names. Caller code does not change.

Seed tasks run on every restart, against a preserved database when delete is off, and must be idempotent. The first adopters' seeders already check for existing rows. The guide states this requirement for consumers.

### Design-time database tools

DevKit ships `SqlServerModuleDbContextFactory<TContext>` and its PostgreSQL and SQLite variants as `IDesignTimeDbContextFactory<TContext>` implementations. EF Core tools prefer a design-time factory over the host, so `dotnet ef` never runs the registration. The factory reads `appsettings.json`, `appsettings.Development.json`, and environment variables directly. In a linked worktree, `dotnet ef database update` or `dotnet ef database drop` would therefore target the ordinary catalog.

The SQL Server, PostgreSQL and SQLite factories must apply the same isolation:

1. An explicit `--connection-string=` argument stays authoritative and is used unchanged.
2. Outside a linked worktree, the factory behaves exactly as today.
3. Inside a linked worktree, it finds the resource inventory that the host persisted under `<worktree>/.dev/state/<appId>/` and looks up the entry for its connection-string key to find the slot. It applies the provider's `_wNN` rewrite, so credentials still come from the configuration it already read. Several applications recording one key in different slots is an ambiguous match and fails. `Mode=Disabled` in the configuration keeps the value unchanged.
4. Inside a linked worktree with no matching inventory entry, it fails with a message saying to start the application once from this worktree or to pass `--connection-string=`. It never falls back to the ordinary catalog.

To support this, the persisted inventory records each managed source configuration key with its generated database name, without credentials. Linked-worktree detection uses the shared file-based detector and never starts a Git process, so a submodule or separate-Git-directory checkout stays ordinary. Consumer-written design-time factories are not covered. The guide tells adopters to derive from the DevKit factory, call `<Provider>DevAppInstanceAdapter.Instance.ResolveDesignTime(...)`, or pass `--connection-string=`.

### Files, logs, and other resources

Resolve writable roots beneath `<worktree>/.dev/state/<appId>/`. Use separate subdirectories for payload storage, logs, and data-protection keys. Record ownership in the application state root. Credentials never belong in this file.

Validate resolved paths against the intended root. Reject symlink or reparse-point escapes before recursive deletion. Reset removes declared payload subdirectories only. Retain logs and keys for diagnosis and stable sessions. Configure file logging and filesystem-backed providers to consume these paths.

Shared diagnostic sinks may remain shared. Log events carry the instance properties described under [Logging sinks](#logging-sinks). OpenTelemetry configuration, service names, resource attributes, and exporters are not adjusted for worktrees. Any enabled external service must either have an explicit per-instance namespace or be declared intentionally shared in status output. Destructive reset never touches those external services in this release.

Relative file paths already resolve beneath each worktree's content root. The feature does not rewrite file paths. Absolute, user-profile, or OS-shared writable paths stay shared by every worktree, and the consumer guide tells adopters to use relative paths for local development files. Add `.dev/` to the repository `.gitignore` as part of the first adoption. The consumer guide tells adopters to do the same.

### Logging sinks

Logging sinks configured through `Serilog:WriteTo` may carry their own database connection strings. These are hidden connections to the ordinary catalog. In managed mode, every such sink must be one of the following:

- Discovered, because its key path contains `ConnectionString` and it names the module database, so that it logs into the instance database.
- Declared shared, which status reports.
- Disabled for the instance.

Otherwise the ordinary-catalog guard fails startup. Logging enrichment adds `DevAppInstanceAppId`, `DevAppInstanceWorktreeId`, and `DevAppInstanceSlot` properties in managed mode only. The enrichment comes from the managed configuration source, so it applies to the deferred logger configuration without a second logger.

### Process ownership and locking

Use a separate per-application lifetime lease in the common Git state directory. The host acquires the lease before resource initialization and holds it until exit. Direct IDE startup must acquire the same lease as CLI, task, and reset-mode startup. When the lease is held, startup retries for up to 5 seconds and logs each attempt at Debug level, because a host child that `dotnet watch` just stopped can hold the lease briefly. After that it fails with `instance_running`.

Run, reset, and slot reassignment coordinate through that lease. Reset holds it across validation and mutation. Release acquires every affected application's lease in catalog order and holds them through final validation and registry update. This prevents a new start between a running-process check and a destructive operation.

For ordinary `dotnet watch`, each host child acquires its own lifetime lease and reuses the persisted assignment after a rebuild. The assignment survives the gap between child processes. Reset may acquire the lease while no host child runs. A new child must acquire that lease before initialization, so reset and startup cannot mutate resources concurrently. Do not require a custom watch supervisor or SDK modification to achieve automatic startup detection.

Runtime records contain process ID, process start time, application ID, worktree ID, URLs, and lease ownership. PID existence alone is insufficient for stop. Normal shutdown requests graceful termination and reports timeout before any explicit force-stop option.

Runtime records are the existing host runtime descriptors. In managed mode, the descriptor writer adds an optional `devAppInstance` object containing `appId`, `worktreeId`, `slot`, `worktreeRoot`, `httpUrl`, `httpsUrl`, `processStartedAt`, and `leasePath`. Every descriptor also lists the bound `urls`. The existing `processId`, `startedAt`, and `workspacePath` fields are reused. Inactive hosts omit the object. Stop verifies the process ID, the process start time, and the held lease before it signals the process. Descriptor writing is currently gated by `DevKitLocalToolingPolicy`. Managed mode requires descriptor writing even when the CLI's IPC and MCP features are disabled, so the eligibility of the descriptor writer becomes "local tooling enabled or isolation active".

Port checks are advisory. Another process may bind after a preflight check. An eventual bind failure must still fail startup and release the application lease. Never kill the conflicting process or retry with different ports.

### Management commands

These optional `bdk dev` commands manage instances. Normal startup automatically assigns a slot and does not require `configure`, `run`, or `watch` wrappers. Commands resolve the worktree from the current directory using the CLI's existing workspace resolution, and accept the global `--output json` option for machine-readable output:

```text
bdk dev configure --slot 2
bdk dev configure --forget-app forecasthub
bdk dev status [--output json]
bdk dev run [--app forecasthub,forecastadmin] [--no-build]
bdk dev stop --app forecasthub [--force] [--timeout 45]
bdk dev reset --app forecasthub --confirm-instance forecasthub:w02
bdk dev release --confirm-slot 2
bdk dev prune [--confirm] [--output json]
```

`stop` writes a stop request file next to the application lease and waits up to `--timeout` seconds (default 45) for the host to shut down gracefully. With `--force`, it kills the process after the timeout, but only when the process ID and start time still match the lease holder.

Starting a host stays possible with `dotnet run`, `dotnet watch`, workspace tasks, or IDE launch. `bdk dev run` is a convenience that starts one or more hosts of the current checkout. It works in linked worktrees and in the ordinary checkout. Without `--app` it starts every host it finds. It takes the project paths that registered applications recorded in the registry inventory, which keeps `WithAppId` overrides, and adds every `Microsoft.NET.Sdk.Web` project whose top-level C# files call `DevKitWebApplication.CreateBuilder`, with the ID derived from the assembly name. It skips `bin`, `obj`, `node_modules`, dot folders and nested checkouts. An unknown ID fails with `app_not_found` and lists the available IDs. A host whose lease is held is skipped. Hosts build sequentially, because parallel builds collide on `obj` files, and a failed build starts nothing (`build_failed`). They then run concurrently with `dotnet run --no-build` and their launch profile, and each output line is prefixed with `[appId]`. The first Ctrl+C reaches the hosts through the shared console, and the command writes a stop request for each managed instance. After 30 seconds, or on a second Ctrl+C, it terminates the remaining process trees. A host that exits with an error before Ctrl+C makes the command fail with `host_failed`. `bdk dev run` adds no `watch` wrapper.

`configure` optionally reserves a specific slot before first startup. Repeating the same assignment is idempotent. It is unavailable for ordinary checkouts. Changing the slot requires all application leases and no remaining instance databases or payload roots. The command reports what must be stopped or reset first.

`configure --forget-app` removes an application's recorded catalog entry so that its next start records the changed declaration. It refuses while any assignment still has instance databases or payload roots for that application, because a changed prefix or offset would orphan them.

`release` covers the whole worktree and all catalog applications. It never deletes resources. Refuse release while instance databases or payload roots remain. Retained logs and key rings do not block release because they remain under the old worktree path. Assignments whose worktree was deleted are handled by `prune`, never by automatic recycling.

`prune` handles worktrees that coding agents and `git worktree remove` delete. It runs from any development checkout of the repository, including the ordinary one. An assignment is orphaned when `git worktree list` no longer lists it as a live worktree. Git lists a live worktree at the recorded path, or at a new path after `git worktree move` whose `.dev/instance.json` carries the same worktree ID. Locked entries count as live even when their directory is missing; entries Git marks `prunable` do not. When `git worktree list` is unavailable, an assignment is orphaned when its canonical path no longer exists. It is not orphaned when a verified runtime descriptor shows a running host for its worktree ID. Without `--confirm`, `prune` lists each orphaned slot, its applications, and the catalog names from the registry inventory copy, then exits without changes. With `--confirm`, for each orphaned application it starts that application's project from the current checkout with the reserved orphan-reset argument, which names the orphaned worktree ID. Acceptance rules for that argument are listed under [Inactive footprint](#inactive-footprint). The host reads the target catalogs from the registry copy, not from its own context. It replaces only `InitialCatalog` on its own configured connection, and requires that the sanitized server identity matches the inventory. It verifies that each catalog name matches the generated naming formula for the orphaned slot before dropping it. Orphan reset never targets the host's own catalog or the ordinary catalog. Payload roots, logs, and key rings were under the deleted directory and need no action. When every application of an orphaned assignment succeeds, `prune` removes the assignment and frees the slot. Otherwise the assignment stays and the output lists the remaining targets, as with `reset_incomplete`. It also stays when the application project no longer exists in the current checkout, and the output names the catalogs to drop manually. The startup slot allocator never prunes.

`bdk dev run` and other launch paths use supported project launch paths such as `dotnet run --project`, preserving development static web assets. They must not launch a built web server DLL as a substitute. Existing workspace tasks and IDE launches continue to invoke their normal project commands. The application bootstrap, not the CLI, owns detection, assignment, and configuration derivation.

Before reset or release, display the application, canonical worktree path, slot, URLs, database catalog names, and storage roots. At startup, the host logs the same summary once at Information level, as described in [Startup log](#startup-log). Never print complete connection strings or authentication material.

`status` reports registry assignments for the worktree and the catalog applications. It joins running descriptors for that workspace by `appId` and `worktreeId`. A descriptor whose process ID or start time no longer matches is reported as stale and is not counted as running.

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

The host integration applies the resolved URLs to Kestrel's effective listening configuration. Tracked launch settings remain baseline inputs and must not restore port 5001 after resolution. URL inputs follow [D7](#d7-baseline-url-settings-are-replaced-explicit-endpoint-configuration-conflicts). Generic URL settings are replaced with an Information log entry. Explicit endpoint configuration that conflicts fails validation rather than silently selecting another address.

The SQL Server adapter uses a connection-string builder to replace only the declared connection's database catalog before EF Core registration. The database server, credentials, and unrelated connection options remain unchanged. Worktree startup applies the caller's creator or migrator options to that instance catalog.

For an in-process fake identity provider, startup applies the resolved public origin through `WithIssuer()` and uses that same origin for bearer and dashboard authorities and registered callback URLs. The provider runs inside the application on the resolved HTTPS listener. No second identity-provider process is launched. The hosted browser client resolves its relative authority against the same origin.

An external identity provider retains its externally configured issuer. Such an integration requires valid per-instance registered callbacks and explicit application integration. The resolver must not replace an external authority with localhost or claim support for automatic remote identity-provider administration.

The resulting configuration flow is:

```text
Normal project or IDE launch
    -> read baseline application configuration
    -> check eligibility and detect the linked worktree
    -> allocate or reuse its persistent slot
    -> derive URLs, declared resource names, and authentication values
    -> apply one resolved context before dependent registrations
    -> initialize instance resources and start the listener
```

An ordinary checkout follows its existing configuration and initialization path. The worktree context does not replace its port, catalog, or identity settings.

1. Identify the catalog application and detect whether the host is in a linked local-development worktree. Otherwise continue normal application startup unchanged.
2. Resolve Git identity, create or recover the persistent assignment under the registry lock, and read normal configuration and secrets.
3. Derive and validate the immutable context and apply its managed settings.
4. Acquire the application lease and verify port availability and the localhost certificate.
5. Validate filesystem ownership. Provision absent resources only after validation.
6. Register consistent authentication and browser-client configuration.
7. Apply the caller's creator or migrator options to the instance catalogs, then run idempotent seeds.
8. Start the listener, publish verified runtime status, and report readiness.

Configuration derivation occurs early enough for host construction. Filesystem validation and provisioning may occur in ordered startup services before the application becomes ready. A failed validation must not leave the host advertising readiness.

### Startup log

In a linked worktree, developers often start the host from an IDE that opens the wrong URL. The startup log is therefore the main way to find the correct address and understand what the routine changed. All entries use the repository template `[LogKey] message (prop=value, ...)` with structured properties and the shared DevKit log key `BDK`. Every entry of this feature starts with `worktree`, so it stands apart from other `BDK` entries.

Resolution runs before the host logger exists. The resolver records its steps in the immutable context. A lifecycle service writes them through the host `ILogger` when the host starts, so the configured sinks receive them. A failure before the host is built writes the same error line to standard error and exits with a nonzero code.

Each step below writes one Information entry:

| Step | Content |
|---|---|
| Mode | Linked worktree detected, application ID, worktree ID, canonical worktree path. |
| Slot | Slot number and how it was obtained: `new`, `existing`, or `recovered`. |
| URLs | Resolved HTTP and HTTPS URLs. When baseline URLs were replaced (D7), the replaced values and their source, for example `ASPNETCORE_URLS`. |
| Databases | Per declared connection: connection name and resolved catalog. Never the connection string. |
| Database lifecycle | Per catalog: created, kept, deleted and recreated, or truncated, as the caller's options decided. |
| Authentication | Issuer, cookie names, and the number of normalized relative redirect URIs. |
| Data protection | Application name and key ring directory. |
| Listener | Result of the post-start address check. |
| Duration | Total resolution time in milliseconds. |

Skipped slots, Git invocations, and replaced configuration keys are logged at Debug level. An inactive context writes at most one Debug entry with its reason and nothing else, consistent with the [inactive footprint](#inactive-footprint).

After the listener check passes, the host writes one summary entry at Information level. It is the last feature entry before readiness, and its first lines are the URLs to open:

```text
[BDK] worktree instance ready (app=forecasthub, slot=2, worktree=w-7f3a)
      Open:       https://localhost:5211
      HTTP:       http://localhost:5210 (redirects to HTTPS)
      Dashboard:  https://localhost:5211/_bdk/dashboard
      Issuer:     https://localhost:5211
      Databases:  ForecastHub_w02_Core
      Worktree:   C:\src\forecasthub-feature-x
      Note:       launch profile URL https://localhost:5001 was replaced; an IDE browser may still open it
```

The note line appears only when baseline URLs were replaced. Every summary value is also a structured property so that file and database sinks can query it. Failures write an Error entry with the stable error code, application, slot when known, and the recovery action from the [error contract](#error-contract). No entry contains connection strings, credentials, tokens, or key material.

## Error contract

Errors identify the application and slot when available. Include a recovery action and return a nonzero process exit code. Machine-readable status must use a stable error code.

| Code | Condition | Required outcome |
|---|---|---|
| `worktree_detection_failed` | Linked-worktree metadata cannot be resolved reliably | Log the failure and fall back to the configured defaults. |
| `slots_exhausted` | All managed slots are claimed | Name `bdk dev prune` for deleted worktrees and report how to inspect and release unused assignments. |
| `assignment_invalid` | Corruption, copied identity, or unsupported version | Preserve files and identify the disagreement. |
| `slot_in_use` | Slot belongs to another worktree | Report its canonical path. |
| `registry_locked` | Lock timeout | Report the registry path without modifying it. |
| `instance_running` | Application lease still unavailable after the 5-second retry | Report the holder's process ID and URLs from its verified runtime descriptor. |
| `port_in_use` | Assigned port unavailable | Report port and verified owner if accessible. |
| `configuration_conflict` | Managed settings disagree | Report key names and sanitized expected values. |
| `unsupported_host` | Linked worktree hosted behind IIS or IIS Express | Name the project launch profile as the supported alternative, without connecting to any declared resource. |
| `resource_not_owned` | Missing or mismatched filesystem ownership marker, or a reset or prune target not recorded in the registry inventory | Leave the resource unchanged. |
| `reset_incomplete` | Some targets could not be removed | List completed and remaining targets. |
| `unexpected_failure` | An unexpected exception during activation or connection string resolution in a linked worktree | Name the exception type and message, fall back to the configured defaults, and name `DevAppInstance__Mode=Disabled` as the way to start without isolation. |

**Fallback policy.** Isolation never blocks a host. When activation, validation, or connection string resolution fails in a linked worktree, the host reports reason `fallback`, removes the managed configuration source, hosted lifecycle service and lease, leaves cookie names and data protection unchanged, and runs with its configured ports and databases. The failure is written to the console as a single `[BDK] worktree isolation failed, the host uses its configured ports and databases:` line with the error code and recovery action, so it stays visible before logging is configured. The dashboard Worktree card shows the code and message. Connection strings that were already resolved before the failure keep their instance database names. The explicit `bdk dev reset`, `prune` and orphan-reset runs stay strict: they refuse or fail with their own exit codes. `DevAppInstance:Mode=Disabled` remains the way to skip the attempt. Unknown or inaccessible process owners remain unknown. Never infer permission to stop them.

## Implementation boundaries and compatibility

Implement the resolver and management commands without a new general-purpose infrastructure framework. Reuse existing configuration, logging, result, and host integration conventions where they fit. Public C# APIs require XML documentation and usage examples. Follow `.editorconfig`, use braces for conditionals, and preserve the repository's member spacing.

Do not automatically enable isolation for production hosts, libraries, or arbitrary consumers of `DevKitWebApplication`. Applications adopt it through explicit public registration. The repository's two development applications and the published solution template are the first consumers of that registration. External applications that consume the packages are the primary audience. Existing integration tests and container-based fixtures retain their configuration and lifecycle without new opt-out settings, changed connection strings, or interactive slot claims. Generic database creator and Testcontainers defaults must not change as part of this feature.

Tracked launch settings remain useful editor entry points, but must not override the resolved URLs. Existing REST request files need selectable instance origins or generated local environment settings. Do not rewrite their tracked base URL on every launch.

Repository updates delivered with the first adoption:

- Add `.dev/` to `.gitignore`.
- Keep launch profiles and the workspace watch tasks unchanged. They work under D7, and their port-agnostic `Now listening on` problem matchers still match. Editor debug configurations whose `serverReadyAction` pattern names port 5001 are generalized to any localhost port, so the browser opens the resolved URL. Verify this from a linked worktree.
- Profiles with `launchBrowser: true` make editors open the profile's `applicationUrl` (port 5001), not the resolved URL. In a linked worktree that opens the ordinary checkout's instance, or nothing. The [startup log](#startup-log) summary starts with the resolved URLs and notes the replaced profile URL, and the guide tells developers to use them or `bdk dev status`. Adopters may set `launchBrowser: false` for worktree work. Do not generate per-worktree launch settings.
- Update the repository agent guidance that prescribes `--urls https://localhost:5001;http://localhost:5000`. It must say that linked worktrees replace those URLs and that agents read the effective URLs from startup output or `bdk dev status --json`.
- Move REST request base URLs to an ignored per-user environment file or `bdk dev status` output instead of a tracked `@baseUrl`.

Package placement, final names subject to review:

| Concern | Placement |
|---|---|
| Context, declarations, adapter contracts, assignment and registry models | `Common.Abstractions` or a new small abstractions namespace with no ASP.NET Core dependency, so the CLI can reference it. |
| Resolver, Git detection, registry, leases | A common implementation assembly referenced by both the web host integration and `Presentation.Cli`. |
| Web host registration, URL and authentication integration | `Presentation.Web` (`Hosting`, `Dashboard`, `IdentityProvider`). |
| SQL Server adapter | `Infrastructure.EntityFramework` (SQL Server-specific code), without making core registration depend on it. |
| Design-time factory isolation | `Infrastructure.EntityFramework.SqlServer`, reading the persisted inventory through the common implementation assembly. |
| Feature state roots | The common implementation assembly. Application file paths are not adapted. |
| `bdk dev` commands | `Presentation.Cli`. |

The initial rollout requires no action for ordinary branch-based development. Preserve its existing fixed port 5001, database catalogs, authentication configuration, and startup lifecycle. Linked worktrees allocate slots automatically on first startup and use separate catalogs. Delete-on-start options keep their effect but act only on the worktree's own catalogs. A failure in worktree detection or allocation must never reconnect to the ordinary database as a fallback.

## Verification plan

Use temporary Git repositories, disposable SQL Server catalogs, and OS temporary directories. Tests own every resource they create. Tests must not delete a developer's current database, change their slot registry, or terminate their hosts.

| Area | Required proof |
|---|---|
| Detection | Ordinary primary checkout, branch switch, detached HEAD, linked worktree, submodule `.git` file, `--separate-git-dir` checkout, linked worktree of a submodule, source archive, and unreadable metadata select the required mode. Startup, the design-time factory, and `bdk dev` return the same result for each case. `GIT_DIR` set in the launching shell does not change the result. |
| Git invocation | Conclusive layouts start no Git process. A moved worktree with a stale back-pointer triggers the Git fallback at startup and fails with `worktree_detection_failed` in the design-time factory. Paths containing spaces remain literal arguments. Missing Git, nonzero exit, timeout, and invalid output follow the detection error policy. Git is invoked only during eligible startup or `bdk dev` commands, with no remote access. |
| Ordinary checkout | Startup preserves configured port 5001, catalog names, authentication, and initialization behavior without creating assignment files. |
| Formulas | All forty-nine managed slots and catalog entries produce unique ports in valid ranges and stable names. |
| Catalog declarations | A second host declaring a recorded ID, prefix, or offset fails with `configuration_conflict` naming both applications. A single-application host with only an ID uses offsets 0 and 1. A changed declaration fails until reconfigured. |
| Assignment | Duplicate claims, copied files, branch switches, moved paths, and corrupt registries behave as specified. |
| Registry concurrency | Concurrent first starts for one worktree share one assignment. Different worktrees receive different slots. Explicit duplicate slot requests produce one winner. |
| Allocation recovery | A crash between the registry commit and local-file write recovers the same assignment. Exhaustion fails without fallback. |
| Cross-platform paths | Windows case handling, Linux case handling, spaces, and separate linked-worktree Git directories work. |
| Launch parity | Run, watch, editor, and direct host startup resolve the same context. Launch profiles that set `ASPNETCORE_URLS`, and `dotnet run --urls ...`, start on the resolved URLs with an Information log entry. Conflicting `Kestrel:Endpoints` fall back to the configured defaults with `configuration_conflict` in the log. A `ConfigureKestrel` `Listen*` call on another port logs a `[BDK]` warning with `configuration_conflict` after start and keeps the host running. |
| Configuration precedence | A configuration source added after registration that shadows a managed key fails at `Build()`. Eager `builder.Configuration[...]` reads after registration observe managed values. `https_port` matches the resolved HTTPS port. |
| Eligibility | Build-time OpenAPI generation, `Testing` environment factories, containerized and cloud hosts, and `Disabled` mode never invoke Git or touch the registry, including inside a linked worktree. |
| Hidden connections | An undeclared logging-sink connection string that targets the ordinary catalog fails startup without connecting. A declared sink logs into the instance catalog. |
| Delete on start | A caller passing `DeleteOnStartup(true)` or enabling `EnsureTruncated` recreates or empties only the instance catalog in managed mode, leaves the ordinary catalog unchanged, and keeps its behavior in an ordinary checkout. Creator and migrator services are unchanged. |
| Port allocation | Allocation skips a slot whose block contains a denylisted port, an OS-reserved port, or a port held by a disposable listener. A reservation added after assignment fails with `port_in_use` and names the recovery. |
| Runtime descriptor | Managed hosts write `devAppInstance` with process start time. Existing descriptor readers and `bdk hosts list` keep working. `bdk dev stop` refuses a reused PID. |
| Port conflict | A disposable listener causes explicit failure without fallback or process termination. |
| Lifetime leases | Duplicate starts fail. Crashes release leases. Ordinary watch children reuse the assignment and acquire the lease before initialization. |
| Database isolation | Two worktrees seed and change independent catalogs on one SQL Server. |
| Preservation | With delete and truncate off, restart preserves an inserted sentinel row. |
| Ownership | Missing or mismatched file markers refuse startup mutation and reset. Reset and prune refuse catalog names that the registry inventory does not record. |
| Reset race | Start cannot acquire the lease while reset validates or mutates resources. Reset mode never starts the listener and exits with a nonzero code on any failure. |
| Reset retry | Partial failure reports targets accurately and a retry converges without touching foreign resources. |
| Orphan prune | After `git worktree remove`, `bdk dev prune` from the ordinary checkout lists the slot and catalogs without changes. With `--confirm` it drops only the catalogs the registry inventory records for the removed worktree, then frees the slot. A catalog that fails a drop stays and keeps the assignment. A worktree with a running verified host is not pruned. The ordinary catalog is unchanged. |
| Path safety | Symlink and reparse-point escapes are refused before recursive deletion. |
| Authentication | Two instances log in independently in one browser profile. Signing out of one preserves the other. |
| Identity metadata | Issuer, discovery endpoints, callbacks, and public browser configuration agree with each origin. Origin-relative client redirect and post-logout URIs resolve per instance at the authorize endpoint, the logout endpoint, and the sign-in page. Absolute non-loopback URIs keep exact-match behavior in every mode. An absolute loopback callback on another port fails validation. The public config endpoint is not mapped when inactive. |
| Inactive footprint | A `Production` host and a containerized host with the feature registered create no `.dev` or state-root files, add no configuration source, register no hosted service or endpoint, leave all declared options unchanged, and refuse the reset and orphan-reset arguments. |
| Cookies and data protection | Every cookie authentication scheme, including app schemes, the dashboard and the fake identity provider, and the session, antiforgery and temp-data cookies carry the instance suffix. Signing in to one instance's provider leaves another instance's provider signed out. Antiforgery cookie names differ between instances. Key rings persist under each state root and survive restart and reset. |
| External identity | An externally configured issuer remains unchanged. Unsupported callback integration fails explicitly without remote administration. |
| Startup log | A managed start writes each step entry and one ready summary whose first line is the HTTPS URL. A start through a launch profile adds the replaced-URL note. An inactive host writes no Information entries from the feature. A failure before host build writes the error line to standard error. |
| Secret handling | Status, public config, errors, and logs omit credentials and private key material. |
| Build and tests | Existing OpenAPI generation, container-based tests, and integration fixtures run unchanged from both ordinary checkouts and linked worktrees. Their ports, connection strings, database lifecycle, and cleanup remain fixture-controlled. They claim no slots and create no interactive resources. |
| Consumer adoption | An application generated from the published solution template, outside this repository, references NuGet packages built from the implementation branch and the `bdk` tool installed from its built package. It adopts the registration without repository scripts or sample references. It runs unchanged in an ordinary checkout, and isolated from a linked worktree, with its module-scoped connection key, logging sink, and authority-derived callbacks. |
| Design-time tooling | From a linked worktree, `dotnet ef database update` through the DevKit SQL Server factory targets the instance catalog after one host start. It fails without connecting when no inventory exists. An explicit `--connection-string=` wins. An ordinary checkout behaves as before. |
| IIS hosting | An IIS Express start from a linked worktree fails with `unsupported_host` before any database connection. An IIS Express start from an ordinary checkout is unchanged. |
| Compatibility | Ordinary checkouts, non-Development environments, containers, build-time tools, hosts that opt out, and hosts not built with `DevKitWebApplication.CreateBuilder` remain unchanged. A DevKit web host started in Development from a linked worktree isolates without code changes. |
| Optional dependencies | Host isolation works without SQL Server references. Declared unsupported resources fail explicitly. |
| Context and status | Startup and DI observe the same immutable context. Sanitized, versioned status reports mode, resources, and process ownership without secrets. |
| Compatibility | Non-development hosts retain their original configuration and do not read the Git registry. |
| Coexistence | An ordinary host on port 5001 and linked-worktree hosts run together. Worktree reset never changes the ordinary database. |

Run real browser acceptance checks for the dashboard and hosted browser client on both Windows and Linux. Use at least two worktrees and two applications. Keep process ownership evidence outside `docs/`. Stop only hosts started by the verification and confirm their ports are released.

Performance acceptance concerns correctness of isolation, not a throughput threshold. Any later throughput report must identify competing workloads because the SQL Server and hardware remain shared.

## Suggested implementation sequence

1. Define public registration, instance-context, and resource-adapter contracts with package boundaries. Add the eligibility rule, automatic startup mode detection, the application catalog, pure derivation rules, and ordinary-checkout regression tests.
2. Add first-start allocation with the port-collision and OS-reservation checks, assignment persistence, crash recovery, atomic registry updates, and multiprocess locking tests.
3. Integrate early host resolution, the managed configuration source with `Build()` validation, URL replacement per D7, the post-start listener address check, the startup log, lifetime leases, and the `devAppInstance` descriptor extension for native run, watch, and IDE startup.
4. Add filesystem ownership, the ordinary-catalog guard, delete-on-start and preservation, reset, and recovery tests. Record source keys in the persisted inventory and apply it in the DevKit design-time database factories.
5. Apply resolved identity URLs, the shared redirect URI normalizer at all three match sites, cookie names including the new fake identity-provider hook, key-ring isolation, browser public configuration, and the client helper.
6. Add the `bdk dev` commands and the `unsupported_host` check. Migrate the first adopters: DevKit builder, registration order, declared catalogs and logging sinks, and relative callbacks. Make the repository updates listed under implementation boundaries.
7. Publish preview packages and the `bdk` tool. Update the solution template as described under [External adopters](#external-adopters), verify an application generated from it, complete Windows and Linux acceptance runs, and deliver the consumer guide specified below under `docs/`.

The feature is complete only when every participating host detects linked worktrees automatically, ordinary-checkout development remains unchanged, worktree startup applies database options only to instance catalogs, and concurrent browser sessions pass. Requiring a separate launcher or manual slot assignment for normal first startup does not satisfy this specification.

## Documentation deliverable

Write the adoption guide as part of implementation, after the public API and package boundaries are established. Do not publish proposed signatures as working APIs in the planning phase. The intended location is `docs/development-worktrees-support.md`, linked from the documentation index and the host guide when it exists.

The guide must serve developers building their own DevKit applications, not only repository contributors. Use fictional, self-contained applications and real shipped APIs. Do not reference project READMEs or rely on sample-project source paths for required steps.

Required content:

1. Explain default activation for DevKit web hosts, the `Enabled(false)` and `Mode=Disabled` opt-outs, automatic linked-worktree selection, and the absence of AI-agent detection or extra permissions.
2. Identify required and optional packages, supported host types, Windows and Linux support, and the initial adapter support matrix.
3. Provide one complete web-host registration using normal configuration, a stable application ID, deterministic ports, and declared resources. Show the required order relative to modules and authentication.
4. Contrast unchanged ordinary-checkout startup with first and subsequent linked-worktree starts. Explain persistent slot allocation, reserved port blocks, conflict errors, and optional explicit slot selection.
5. Show SQL Server catalog mapping, relative file paths, cookie names, authorities, callbacks, data-protection configuration, and the hosted browser client's relative authority.
6. Explain consuming the immutable instance context and sanitized status from application integrations or developer tools. Include versioned machine-readable output without secrets.
7. Describe the supported resource-adapter extension contract, intentionally shared resources, and isolation limits. Never imply that undeclared external resources are isolated automatically.
8. Cover status, verified stop, explicit reset, release, prune of deleted worktrees, incomplete provisioning, crash recovery, and safe handling of stale assignments. Separate automatic startup from optional management tooling.
9. State that existing container-based tests, integration fixtures, build-time generation, and production configuration retain their lifecycle without new opt-out flags.
10. Include troubleshooting for port conflicts, identity redirects, unsupported resources, registry disagreement, filesystem ownership failures, and exhausted slots.
11. State the minimum DevKit package version, and that the installed `bdk` tool version must match the packages. Show the `.gitignore` entry and `dotnet tool` installation for an application outside this repository.
12. Cover existing-application concerns: module-scoped connection keys, configuration-driven logging sinks, `DeleteOnStartup` and `EnsureTruncated` callers, issuers and callbacks derived from an authority setting, CORS origin lists, `launchBrowser`, and IIS Express profiles.
13. Explain design-time EF Core tooling in linked worktrees: the DevKit factory behavior, the start-once requirement, `--connection-string=`, and the lack of coverage for custom factories.

Validate guide snippets against shipped public APIs and test the documented adoption path in an independent fictional consumer host. The guide is a completion requirement, not a substitute for automated compatibility tests. The guide is published at [development-worktrees-support.md](../development-worktrees-support.md).

## Open items for implementation review

The first implementation step resolved these items:

- **Symbols and packages**: The resolver, context and options live in `BridgingIT.DevKit.Common.Utilities` (namespace `BridgingIT.DevKit.Common`). Every `DevKitWebApplicationBuilder` activates the feature by default. The optional entry point `AddDevAppInstance` lives in `BridgingIT.DevKit.Presentation.Web`, and `Enabled`, `WithAppId` and `WithHttpOffset` live on the builder in `BridgingIT.DevKit.Common.Utilities`. Lazy activation uses `CompleteConfiguration` and `DevKitBuilderProperties.ConfigurationCompletion` in `BridgingIT.DevKit.Common.Abstractions`. `ShareConnectionString` and `WithDatabases` live on the builder in `BridgingIT.DevKit.Common.Utilities`. `WithSqlServerDatabases` and the shared `DevAppInstanceDatabaseAdapterBase` live in `BridgingIT.DevKit.Infrastructure.EntityFramework`. `DevAppInstanceDatabaseAdapterAttribute` and `DevAppInstanceDatabaseAdapterLocator` live in `BridgingIT.DevKit.Common.Utilities`; the SQL Server, PostgreSQL and SQLite provider assemblies carry the attribute. `WithPostgresDatabases` and `WithSqliteDatabases` live in `BridgingIT.DevKit.Infrastructure.EntityFramework.Postgres` and `.Sqlite`. The three `<Provider>ModuleDbContextFactory` types resolve instance databases at design time. The configuration section is `DevAppInstance`, with the keys `Mode` (`Auto` or `Disabled`) and `Slot`.
- **Reserved arguments**: `--bdk-dev-reset=<appId>:wNN` and `--bdk-dev-orphan-reset=<worktreeId>`. Both run inside the bootstrap before the host is built and do not use the console-command infrastructure.
- **REST environment file**: Not implemented. Tracked `.http` files keep their ordinary base URL. Developers read the instance URLs from the startup log or `bdk dev status`.
- **Port denylist**: 5341, 5432 and 5672. Adopters extend it with `WithReservedPorts`.
- **Log key**: `BDK`, with every message starting with `worktree`.
- **First version**: The release after 10.0.119. `bdk dev` does not yet detect a host package older than the tool. The guide tells adopters to keep both versions equal.
- **Inventory schema**: `.dev/state/<appId>/inventory.json` and the registry copy carry `schemaVersion` `1`. Sanitized status uses the same version.

## Implementation outcomes

These decisions changed during implementation:

- Registration and resolution errors throw `DevAppInstanceException` with a stable code instead of returning `Result<T>`, because they must stop startup before the host is built.
- `stop` uses a stop request file for graceful shutdown. `--force` kills only after verifying the process ID and start time. The descriptor and lease info record `processStartedAt` for that check.
- Reset does not check catalog ownership markers. Reset trusts the inventory and the generated naming formula.
- An application whose lease is free is reported as `stopped`.
- `reset` and `prune --confirm` pass the host's exit codes through: `2` refused and `3` incomplete.
- Identity authorities are rewritten only when the ordinary authority is a loopback URL. Token issuer validation uses the normalized issuer.
- OpenTelemetry resource attributes are not changed for worktrees.
- `bdk` tooling is one version per repository and shared by every worktree.
- IDE launch profiles keep their explicit `--urls` values. Managed mode replaces them in linked worktrees.
- Host runtime descriptors gained a general `urls` list (bound addresses, HTTPS first). The writer runs again on `ApplicationStarted` to capture them, and falls back to the instance URLs before the server binds. `devAppInstance` also records `worktreeRoot`. `bdk hosts list/current` and the `bdk mcp` runtime tools return `url`, `urls` and a `devAppInstance` summary, so an agent in a worktree gets the right host URL.
- Database isolation no longer needs a key mapping. `MapSqlServerDatabase(moduleId, keys...)`, `ShareSqlServerConnection` and the application database prefix were removed. The database adapters discover connection strings and name instance databases `<database>_wNN`. SQL Server, PostgreSQL and SQLite share one base adapter. `ShareConnectionString(key)` keeps a key shared.
- CLI workspace matching accepts `devAppInstance.worktreeRoot`, and workspace resolution treats a `.git` file as a root, so linked worktrees without a solution file at their root resolve correctly. Discovery reports a host as `Stale` when its recorded `processStartedAt` does not match the running process.

## Design alternatives

- Random ports reduce assignment work but make addresses and identity callbacks unpredictable. They are excluded by requirement.
- Branch-name or path hashing avoids a registry but needs collision handling and changes identity when paths or branches change. Persisted sequential slots provide stable addresses.
- A SQL Server per worktree isolates engine resources but adds service startup time and memory use. Separate catalogs meet the current data-isolation requirement.
- A repository PowerShell launcher would duplicate host discovery that the `bdk` CLI and host runtime descriptors already provide, and consumers could not reuse it. The CLI command group replaces it.
- A CLI that loads provider adapters directly would need application secrets and provider packages. Reset mode inside the application host reuses both.
- Serving a rewritten `wwwroot/appsettings.json` would avoid a client helper. It is rejected because the file is tracked, shared by every checkout, and subject to browser caching.
- Hostnames and a local reverse proxy could isolate browser origins further. They add DNS, certificate, and proxy setup that is unnecessary for the first implementation.
- The DevKit builder could warn when it detects a linked worktree in an application that never registered the feature. That case is unlikely, and the warning would put detection into every unregistered host. Unregistered hosts stay completely unaffected.

## Related documentation

- [Worktree development isolation guide](../development-worktrees-support.md)
- [DevKit web host specification](./spec-presentation-devkit-web-host.md)
- [bdk CLI specification](./spec-presentation-cli.md)
- [bdk CLI guide](../features-cli.md)
- [Presentation configuration](../features-presentation-configuration.md)
- [Fake identity provider](../features-identityprovider.md)
- [Dashboard feature guide](../features-presentation-dashboard.md)
- [Startup tasks](../features-startuptasks.md)
