---
created: 2026-11-19
status: implemented
---

# Specification: Filtering Review

> Review filtering from REST requests and backend-created filter models through repository and database execution, including correctness, test coverage, performance, and allocations. Preserve existing public APIs and contracts in every proposed improvement.

[TOC]

## Purpose

Establish how reliably filtering behaves, where its tests leave gaps, and what its execution costs. Produce reproducible findings and a prioritized hardening backlog grounded in measurements.

The review covers two complete flows. REST calls start with HTTP binding and deserialization and end with the HTTP response. Backend calls start with a `FilterModel` constructed directly in C# or through the fluent builder and end with repository results and pagination metadata. Both flows include filter translation and database execution. Backend calls require no HTTP context or JSON round trip. Isolated expression benchmarks alone cannot establish end-to-end correctness or performance.

The primary documentation contract is [Filtering](../features-filtering.md). Reconcile that guide with the public API, existing tests, and observed behavior. A discrepancy is a finding, not permission to change existing behavior silently.

This specification defines the review and its deliverables. Writing this specification does not perform the review or implement production changes. Later hardening is a separate stage with the acceptance criteria below.

The first delivery consists of review findings, tests and reproducers, benchmark baselines, and a prioritized hardening backlog. Production fixes follow in separate, focused changes after that evidence is available.

## Scope

### Included layers and components

- Common: `FilterModel`, criteria and operator types, fluent builders, expression helpers, query-string helpers, JSON parsing, and converters.
- Domain: standard and custom specification construction, named and composite specifications, specification registration, and find, order, include, and hierarchy option builders.
- Application: filtering consumers and service contracts, required server-side restrictions, mapping, cancellation, and recoverable failures.
- Infrastructure: EF Core repository implementations and operational query consumers, query translation, counting, ordering, paging, tracking, materialization, and relational providers.
- Presentation: controller and minimal API binding, query and body readers, OpenAPI schemas, error mapping, and response serialization.
- Tests and benchmarks for these components and their integration points.

Cover both repository specifications and direct `IQueryable` operational searches. Record their supported subsets and intentional differences. Shared use of `FilterModel` does not imply identical capabilities or defaults.

Inventory every documented operational search consumer. Use shared contract tests where behavior is common and focused tests where consumers impose different field, archive, or authorization restrictions.

### Filter origins

- REST-originated models supplied through supported query-string and request-body entry points.
- Backend-created models constructed with object initializers, criteria constructors, and collection assignments, then passed directly to repository filtering overloads.
- Backend-created models produced through `FilterModelBuilder` and its expression-based helpers, then passed directly to the same repository APIs.

Backend construction is a first-class execution path with its own tests and measurements. Cover native CLR values, custom parameter values, and specification arguments without relying on JSON conversion to normalize them. Compare equivalent programmatic and deserialized models where their contracts overlap, and record intentional differences.

### Boundaries

The review can propose internal changes and public extensions. It does not prescribe a replacement filtering architecture, new query language, new persistence provider, or database schema migration.

For proven validation or complexity-control gaps, the review proposes minimal opt-in extensions with supporting evidence. Detailed public API design and implementation belong to later hardening. Do not design speculative policies before a finding establishes their need.

Database querying is limited to EF Core with SQLite, SQL Server, and PostgreSQL. Cosmos DB and LiteDB are excluded from capability audits, tests, benchmarks, and hardening. LiteDB removal is separate work on another branch and must not be included in this review.

Application-specific indexes and search semantics are considered when explaining query costs. They are not universal library defaults. Dashboard redesign, unrelated repository refactoring, package upgrades, and exhaustive testing of every application query are outside scope.

## Compatibility requirements

Compatibility is mandatory for all later improvements, including optimizations and fixes.

- **COMP-001:** Preserve existing public and protected C# signatures, constructors, generic constraints, extension methods, optional parameter defaults, and externally implemented contracts.
- **COMP-002:** Preserve source and binary compatibility. New overloads must not make existing calls ambiguous. An extension must not force existing interface implementations or subclasses to add members.
- **COMP-003:** Preserve JSON property names, enum and operator representations, existing numeric enum values, supported aliases, query encodings, and established serialization conventions.
- **COMP-004:** Preserve established behavior of existing valid REST requests and programmatic repository calls, including grouping, conversions, field resolution, ordering, paging defaults, tracking, includes, and result metadata, except for proven defect corrections under COMP-007.
- **COMP-005:** Characterize missing, empty, malformed, and unsupported input separately. Record current exception, `Result`, HTTP status, and response-body behavior before proposing changes.
- **COMP-006:** New restrictions and deliberate semantic changes, including limits, field policies, null semantics, and validation rules, must be opt-in through an additive API or configuration when they change existing acceptance or behavior. Proven defect corrections follow COMP-007.
- **COMP-007:** Proven defects may be corrected through existing APIs without opt-in while preserving source compatibility, binary compatibility, and JSON contracts. Each correction requires a reproducer, an independently justified expected result, regression tests, and release notes explaining the observable change. Classify disagreements between documentation, tests, and execution explicitly. Do not label an intentional policy change as a defect or redefine the baseline to hide a change.
- **COMP-008:** Verify proposed changes against an API baseline and representative consumers. Compile existing calls against the new assemblies and execute a consumer compiled against the baseline with the new assemblies where applicable.

Public extensions must retain the architecture boundaries in [ADR-0001](../adr/0001-clean-onion-architecture.md) and [ADR-0003](../adr/0003-modular-monolith-architecture.md). Reuse existing results, specifications, repository options, validation, and exception handling. Domain and Common must not gain dependencies on Presentation or Infrastructure.

## Review work packages

### R1: Contract and execution inventory

As a toolkit maintainer, I want a complete filtering inventory so that the review follows every supported entry point and execution path.

Requirements:

- Map HTTP entry points, serializer configurations, direct C# construction, fluent builders, translation stages, repository overloads, and response types to their source locations.
- Inventory standard operators, custom filters, named specifications, composite nodes, ordering, includes, hierarchy, tracking, and pagination.
- Record supported operator/type combinations and expected rejection for unsupported combinations. Include CLR paths, casing, nullable properties, enums, toolkit enumerations, typed identifiers, and value objects where applicable.
- Record current defaults per execution path rather than assuming the guide defines one universal default.
- Identify shared mutable configuration, specification registry lifetime, and repeated construction or enumeration.

Acceptance criteria:

1. Every discovered public filtering entry point maps to an implementation path and a documented or characterized contract.
2. Every capability maps to existing tests, a coverage gap, or an explicit unsupported outcome.
3. When documentation and execution disagree, the inventory records both behaviors and links a finding.
4. The inventory distinguishes REST, direct C# construction, and fluent-builder origins, includes repository and operational search paths, and identifies their differences.

### R2: Correctness, validation, and resource handling

As an API consumer, I want filters to return predictable results and failures so that invalid input cannot silently broaden queries or exhaust resources.

Review cases:

- AND/OR grouping, precedence, empty groups, nested groups, negation where supported, and composition with existing server specifications.
- `Any`, `All`, and `None` over empty, null, and populated collections, multiple child criteria, nested collections, and supported collection shapes.
- Null values, nullable comparisons, null navigation properties, string empty checks, and positive and negative string operators.
- Numeric precision and overflow, incompatible values, booleans, GUIDs, enums, toolkit enumerations, typed identifiers, and implicit conversions.
- Native CLR values assigned directly to criteria, custom parameters, and specification arguments, compared with their supported deserialized representations. Include fluent-builder output and direct model construction.
- Date and time parsing, inclusivity, open or reversed ranges, overnight ranges, relative periods, culture, time zones, and daylight-saving boundaries where applicable.
- Query-string escaping, JSON round trips, custom serializer options, missing bodies, JSON null, malformed payloads, duplicate or unknown members, and unsupported operator representations.
- Paging at zero, negative values, default values, large values, integer overflow, empty results, and the final page.
- Invalid paths, inaccessible or unsupported members, invalid include chains, and hierarchy depth.
- Missing or unknown named specifications, incompatible entity types, constructor arguments, duplicate registration, and concurrent registry use.
- Request cancellation, body buffering and stream ownership, response writes after errors, and failure propagation through service and repository boundaries.
- Excessive payload size, total criteria count, nesting, composite-node count, membership lists, includes, hierarchy expansion, and provider parameter limits.
- Cyclic programmatically constructed models and concurrent reuse of mutable models or serializer configuration. Distinguish supported use from misuse.

Inspect whether current policies protect exposed fields, orderings, includes, and specification names. Verify parameterization and preservation of application-owned tenant, archive, soft-delete, and authorization restrictions wherever present. The filtering library must not be treated as the owner of application authorization.

Acceptance criteria:

1. Each supported operation has normal, boundary, and invalid-input expectations recorded for its applicable types.
2. A suspected correctness or security issue has a minimal reproducer and an independently stated expected result before it becomes a confirmed finding.
3. Tests demonstrate that client criteria cannot remove mandatory server restrictions in the paths that apply them.
4. Resource-limit and failure-handling proposals state existing behavior and compatibility implications. New restrictions remain opt-in; proven defect corrections follow COMP-007.

### R3: Test coverage and assertion quality

As a maintainer, I want tests that detect filtering regressions so that passing checks provide evidence about results and failures.

Requirements:

- Collect line and branch coverage for filtering code, reporting each relevant assembly and execution path separately from solution-wide coverage.
- Review assertions for returned records, order, total count, page metadata, failure details, and observable effects. Object existence alone is insufficient.
- Cover parser-to-builder round trips and HTTP-to-database-to-response behavior with fictional models such as `CatalogItem`, `Category`, and `Supplier`.
- Execute directly constructed and fluent-built models against repositories without HTTP or serialization. Assert records, ordering, count, pagination, includes, hierarchy, tracking, and failure behavior where applicable.
- Exercise actual ASP.NET Core request binding and response handling with a test host. Mocks alone cannot prove malformed-request behavior or response-stream ownership.
- Compare compiled in-memory predicates with provider execution where their contracts overlap. Treat intentional provider differences explicitly.
- Use deterministic generated combinations for grouping, conversion, and collection behavior. Store seeds and failing inputs. Expected results must not be computed by the builder under test.
- Evaluate targeted mutation testing for high-risk grouping, null, range, and paging branches. Report surviving mutations or a documented tooling limitation.
- Control time, culture, and static registry state so tests are repeatable and independent. Prefer existing test utilities.

Acceptance criteria:

1. Every inventoried capability has behavioral coverage or an explicit gap with priority and rationale.
2. High-risk branches have tests or a recorded gap; aggregate coverage percentages do not replace this assessment.
3. Confirmed defects have executable reproducers suitable for regression tests. Known failing reproducers remain distinct from the passing baseline.
4. Reports distinguish passed, failed, skipped, and unexecuted tests. Unavailable infrastructure is not reported as a pass.
5. The provider corpus covers both backend construction styles and equivalent REST-originated models. REST coverage alone does not satisfy backend-path coverage.

### R4: Repository and database behavior

As an application developer, I want filtering to execute correctly at the persistence layer so that large datasets do not require unintended client-side processing.

The required relational execution targets are EF Core with SQLite, SQL Server, and PostgreSQL. Include compiled predicates and LINQ-to-Objects execution as separate reference paths where applicable. EF Core InMemory is not evidence of relational translation. Cosmos DB and LiteDB are outside this review.

Requirements:

- Execute backend-created models directly through repository APIs for each required provider, covering both object construction and fluent builders. Use equivalent REST cases to detect unintended differences in shared translation and execution.
- Inspect generated queries and parameters for representative standard, nested, collection, custom, and named specification cases.
- Verify filtering before counting and paging, matching count and result predicates, ordering composition, and tie handling on a stable dataset.
- Verify includes, hierarchy, tracking options, materialization, repository behaviors, and restrictions retained by the application.
- Record query counts and identify N+1 execution, premature enumeration, duplicate translation, and unnecessary data loading.
- Evaluate parameterization, query-shape reuse, large membership sets, provider translation failures, and supported execution plans.
- Record differences in collation, case sensitivity, null ordering, GUID ordering, date support, and conversions. Do not normalize them by changing established defaults.
- Separate offset-pagination limitations under concurrent writes from guarantees on a stable dataset. State whether count and page consistency depends on transaction isolation.

Acceptance criteria:

1. Required providers execute the selected contract corpus and report differences with exact provider and server versions.
2. Representative queries have parameterization and query-count evidence, plus execution-plan analysis for measured slow cases where supported.
3. Tests verify returned records, total count, ordering, and page boundaries through the actual repository path.
4. Unsupported translations and unavailable providers have explicit outcomes. Any missing required provider evidence leaves that part of the review incomplete.

### R5: Performance and allocations

As a toolkit maintainer, I want reproducible cost measurements so that improvements target demonstrated bottlenecks while preserving public contracts and documenting proven defect corrections.

The reference machine is `doomarchy2`, the current workstation. Run the reference benchmarks and load generators there, and capture its actual hardware, operating system, runtime, and power settings with each baseline.

Run SQL Server and PostgreSQL in dedicated local containers on `doomarchy2`, with images pinned to immutable digests. Use dedicated local SQLite files for database benchmarks. Run provider measurements sequentially without unrelated test services or competing workloads. Record image digests, server settings, container resource limits, storage placement, and SQLite settings so later runs can reproduce the setup. Lightweight in-memory SQLite correctness tests remain separate from these reference measurements.

Measure these stages separately, as a complete HTTP request, and as a backend model-to-repository call:

1. HTTP query/body reading, decoding, and deserialization for REST calls, or direct model construction and fluent-builder execution for backend calls.
2. Value conversion and filter validation already present in the path.
3. Specification, expression, and find-option construction.
4. Query composition and provider translation.
5. Count and data-query execution and materialization, followed by response serialization for REST calls.

Backend benchmarks must measure direct construction and fluent construction separately. Include both constructing and executing a model in one operation and executing an already constructed model. Label these cases so moving setup outside the timed operation cannot conceal construction costs. Backend measurements require neither an HTTP host nor a serializer.

Use BenchmarkDotNet and its memory diagnostics for isolated benchmarks. Keep setup and data generation outside timed operations unless construction is the operation being measured. Enumerate or consume results when measuring execution, and label construction-only benchmarks explicitly.

When optimization opportunities compete, prioritize ordinary paged queries with 1 to 10 criteria, common comparison and string operators, and sorting, for both backend and REST calls. Complex nested filters retain full correctness and stress-test coverage. Do not trade a regression in these everyday queries for a faster complex case without an explicit, measured tradeoff decision.

The workload set must include:

- Empty, single, 10-criterion, and 100-criterion filters where supported, plus cases at and beyond existing limits.
- Flat and nested groups, mixed operators, collection criteria, custom ranges, text search, and named/composite specifications.
- Small and large membership lists and include sets, with exact sizes recorded.
- Arrays, lists, lazy enumerables, and single-use enumerables at public APIs accepting `IEnumerable<T>`.
- First use and warmed execution, repeated values, changed values with the same shape, and many distinct query shapes.
- Selective, broad, and no-match queries against deterministic datasets of approximately 1,000, 100,000, and 1,000,000 rows where feasible.
- Ordinary and deep pages, duplicate sort values, different page sizes, and queries with and without includes.
- Single-request execution and sustained concurrent HTTP requests against an isolated test host and dedicated datasets.
- Sequential and concurrent backend repository calls with an independent repository scope and `DbContext` per operation, avoiding unsupported concurrent use of one context.

Record isolated latency distributions, allocated bytes per operation, GC collection counts, and throughput. HTTP runs report throughput, p50/p95/p99 latency, errors, process allocation rate, GC activity, and retained-memory behavior. Thread-local allocation counters are not valid measurements for an asynchronous request crossing threads.

Inspect repeated enumeration, reflection and metadata lookup, boxing, expression compilation, string splitting, JSON buffering, temporary collections, and repeated materialization. Caching is a candidate only when measurements justify it. Any proposed cache must have bounded growth, safe keys, concurrency behavior, and no captured request, tenant, entity instance, or expiring relative-time values.

Benchmark measurements initially produce reports rather than automated pass/fail gates. Establish repeatability on `doomarchy2` before enabling per-scenario latency, allocation, or throughput gates. Derive thresholds from repeated measurements and observed variance, and record the reference environment used by each gate. Correctness and compatibility checks block changes from the start; reporting-only performance checks do not waive the requirement to assess measured regressions during hardening.

Acceptance criteria:

1. Baseline results identify the commit, commands, SDK/runtime, build configuration, hardware, database versions, schema/indexes, dataset, workload, and tool versions.
2. Timed operations represent the stated stage. Database I/O, query translation, and library CPU costs are distinguishable.
3. Before/after comparisons use equivalent environments, datasets, warmup, and workload settings, with variance reported. Unsupported or infeasible workloads have explicit reasons.
4. Performance findings include measurements and allocation evidence. Static observations remain hypotheses until measured.
5. Proposed latency, allocation, and throughput regression budgets derive from the baseline and measurement noise. Record them before optimization; do not invent a universal zero-allocation or latency target.

## Documentation review

Review the existing filtering guide, related sections of the domain specifications, repositories, and serialization guides, relevant package READMEs, public XML documentation, and generated OpenAPI metadata. Compare them with the implementation, public contracts, and executed behavior.

Check both directions. Identify documented behavior that the code does not support or implements differently, and supported behavior that the documentation omits. Neither the code nor existing prose alone proves the intended contract when they disagree; record the evidence and proposed resolution.

Check setup and registration steps, API names and signatures, examples, default values, standard and custom operators, grouping rules, naming and casing, conversions, pagination, response metadata, failure handling, provider limits, and distinctions between repository and operational searches. Include both REST and backend-created filter workflows.

Compile representative C# examples and execute representative JSON and query-string examples against fictional test models. Record the applicable serializer configuration and EF Core provider. Identify incomplete examples, missing prerequisites, and examples that compile but produce unexpected results.

Assess documentation gaps for setup, direct model construction, fluent builders, typed values, composition with existing specifications, unsupported combinations, null and collection behavior, failure cases, resource controls, and provider differences. Readers must be able to distinguish supported behavior, intentional limitations, and known defects.

Preserve the existing feature-guide structure. Use fictional, self-contained domain samples with real DevKit APIs. Do not name or link repository example applications in documentation or captures. Keep application-specific execution evidence outside `docs/`.

Documentation gaps must reference the actual behavior and the proposed correction. Claims about performance or allocations require measurements. OpenAPI descriptions must not advertise unsupported combinations.

Acceptance criteria:

1. Each inventoried capability maps to existing documentation or an explicit documentation gap.
2. Inaccurate claims, obsolete APIs, broken examples, and missing coverage are classified separately, with source locations and evidence.
3. Representative examples have compilation or execution results; examples not verified are marked as such.
4. The review report includes a prioritized documentation backlog covering corrections and missing material. It does not claim that existing documentation matches the code until those comparisons are complete.

## Execution sequence

1. Record the revision, clean or pre-existing working-tree changes, environment, provider availability, and test/benchmark commands.
2. Complete R1 and establish the compatibility baseline and capability-to-test matrix.
3. Run existing focused tests, collect coverage, and reproduce candidate issues from R2 and R3.
4. Execute the provider corpus, backend model-to-repository scenarios, and HTTP-to-database scenarios from R4.
5. Establish isolated and end-to-end baselines from R5 before any production optimization.
6. Reconcile documentation and deliver the findings, coverage assessment, measurements, and hardening backlog.

The first delivery ends after step 6. Later hardening implements selected findings in focused changes, preserving compatibility and comparing against the recorded baseline.

Run top-level builds and tests sequentially in the same worktree. Run performance measurements without competing builds, tests, or sample hosts. Inspect container and external-service requirements before provider runs. Stop test hosts and temporary infrastructure started for the review when finished.

The review may add focused tests, reproducers, benchmark cases, and isolated test-host support when it is executed. Those additions do not authorize production behavior changes during baseline collection.

## Findings and deliverables

Produce these review outputs:

- A contract inventory and capability-to-test matrix.
- A source and binary compatibility baseline with representative consumer checks.
- A filtering-specific coverage and assertion-quality assessment.
- Provider execution results and representative query evidence.
- Reproducible performance and allocation baselines for REST calls, direct backend model construction, fluent builders, and repository execution.
- A documentation assessment covering accuracy against code, example verification, missing coverage, and proposed corrections, plus a prioritized hardening backlog.

Each finding records an ID, severity, affected entry point and provider, source location, expected and observed behavior, reproducer, evidence, proposed remedy, compatibility impact, regression-test needs, and disposition.

Classify findings as confirmed defects, test gaps, documentation gaps, measured performance problems, or optional extensions. Prioritize incorrect results, lost server restrictions, and unbounded resource use ahead of optimizations. Separate measured cost from suspected cost.

Store raw test, coverage, query, and benchmark artifacts outside `docs/`, for example in an ignored evidence directory or CI artifacts. Reports must identify artifact locations and exact reproduction commands. Include full BenchmarkDotNet output rather than only selected summary numbers. Do not include credentials or sensitive request values.

## Completion criteria

### Review complete

- Every in-scope entry point and capability has a disposition in the inventory.
- Compatibility contracts and representative consumers are recorded.
- Correctness, test quality, provider execution, performance, allocations, and documentation each have an evidence-backed assessment.
- Required relational provider runs are complete. Missing runs are reported as incomplete, with their blocking conditions.
- REST-originated, directly constructed, and fluent-built models each have correctness, coverage, and performance evidence through repository and database execution.
- Every confirmed defect has a reproducer and a proposed compatible remedy or an explicit unresolved compatibility conflict.
- Coverage gaps and unmeasured scenarios are visible. No claim of complete coverage relies only on test counts or a percentage.
- The report identifies what can be hardened internally, what needs an additive extension, and what remains an open decision.

Review completion does not mean all defects have been fixed. A completed review can conclude that filtering is not ready for a given workload.

### Later hardening complete

- Selected findings have regression tests and documented dispositions.
- Existing consumer compilation, binary checks, and JSON contracts pass for affected contracts. Behavior checks preserve established semantics except for proven defect corrections documented under COMP-007.
- Each defect correction has regression tests and release notes describing the changed behavior.
- Focused tests and relevant provider tests pass. Known failures are not suppressed to obtain a passing result.
- Affected project builds and a sequential solution build pass. Relevant broader test suites run according to the change's impact.
- Comparable benchmarks meet the recorded budgets or document an explicitly accepted tradeoff. New retained-memory or cache-growth risks are addressed.
- Documentation describes verified final behavior, including any opt-in extensions and provider limitations.

## Agreed review decisions

1. Proven defects may be corrected through existing APIs under COMP-007. New restrictions and deliberate semantic changes remain opt-in.
2. The reference machine is `doomarchy2`, with dedicated pinned local SQL Server and PostgreSQL containers, local SQLite files, and sequential provider measurements.
3. The first delivery contains review evidence, tests and reproducers, benchmark baselines, and a prioritized backlog. Production fixes follow separately.
4. Prioritize ordinary paged backend and REST queries with 1 to 10 criteria when optimizing. Complex filters retain full correctness and stress-test coverage.
5. Benchmarks initially produce reports. Automated performance gates follow demonstrated repeatability; correctness and compatibility checks block changes from the start.
6. Propose minimal opt-in validation or complexity-control extensions only for proven gaps. Detailed API design and implementation follow during hardening.

The six scope questions are resolved. Exact performance thresholds and extension designs depend on review evidence and must not be invented in advance. Existing documentation accuracy and missing coverage are required review outputs, not completed findings.

## Source references

- [Filtering guide](../features-filtering.md)
- [Domain specifications guide](../features-domain-specifications.md)
- [Domain repositories guide](../features-domain-repositories.md)
- [Common serialization guide](../common-serialization.md)
- [Common filtering contracts and helpers](../../src/Common.Abstractions/Filtering/)
- [Filtering JSON converters](../../src/Common.Serialization/Converters/)
- [Domain filtering builders](../../src/Domain/Filtering/)
- [Repository result extensions](../../src/Domain/Repositories/Result/)
- [HTTP filtering integration](../../src/Presentation.Web/Filtering/)
- [Filter OpenAPI schema transformer](../../src/Presentation.Web/OpenApi/FilterModelSchemaTransformer.cs)
- [Common filtering tests](../../tests/Common.UnitTests/Filtering/)
- [Filtering serialization tests](../../tests/Common.UnitTests/Serialization/Filtering/)
- [Domain filtering tests](../../tests/Domain.UnitTests/Domain/Filtering/)
- [Presentation filtering tests](../../tests/Presentation.UnitTests/Web/Filtering/)
- [Infrastructure integration tests](../../tests/Infrastructure.IntegrationTests/)
- [Benchmarks](../../benchmarks/)
