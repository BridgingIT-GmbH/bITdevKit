# ADR Quick Reference

One-paragraph summaries of all Architectural Decision Records (ADRs) for quick lookup during architecture reviews.

## Core Architecture

### ADR-0001: Clean/Onion Architecture with Strict Layer Boundaries

Adopts Clean/Onion Architecture with strictly enforced inward-pointing dependencies: Domain (innermost, pure business logic with ZERO external dependencies) → Application (use case orchestration, references Domain only) → Infrastructure (technical implementation, references Domain + Application) → Presentation (endpoints/UI, references Application via IRequester). This protects core business logic from infrastructure changes, enables independent testing, makes dependency directions explicit and enforceable, and supports long-term maintainability. Enforced through project structure, code reviews, and architecture tests.

**When to reference**: Layer boundary violations, circular dependencies, DbContext in Application/Domain.

### ADR-0003: Modular Monolith Architecture

Organizes the application as a modular monolith where each module under `src/Modules/<ModuleName>` is self-contained with clear boundaries (Domain, Application, Infrastructure, Presentation layers). Modules communicate via integration events (INotifier) rather than direct references. This provides microservices-like modularity benefits (loose coupling, independent deployment potential) while maintaining monolith simplicity (single deployment, shared transaction, easier debugging). Modules can later be extracted into microservices if needed.

**When to reference**: Cross-module dependencies, module isolation, integration events.

---

## Domain & Data

### ADR-0012: Domain Logic Encapsulation in Domain Layer

Enforces that business logic belongs exclusively in the domain layer within aggregates, entities, value objects, and domain services. Aggregates use private setters, factory methods (`Create()` returning `Result<T>`), and change methods (e.g., `ChangeName()`) to enforce invariants. This prevents anemic domain models where business logic leaks into application/presentation layers. Domain methods validate business rules before state changes and return `Result<T>` for composable error handling.

**When to reference**: Public setters on aggregates, business logic in handlers/endpoints, validation outside domain.

### ADR-0008: Typed Entity IDs using Source Generators

Uses `[TypedEntityId<Guid>]` attribute with source generators to create strongly-typed entity IDs (e.g., `CustomerId`, `OrderId`) as `readonly partial struct` types. This provides compile-time type safety (cannot pass `OrderId` where `CustomerId` is expected), enables IDE IntelliSense, makes code self-documenting, and supports safe refactoring. Strongly-typed IDs are used consistently across repositories, commands, queries, DTOs, and API models.

**When to reference**: Using `Guid`/`int` directly for entity IDs, ID type confusion, method signatures with ambiguous parameters.

### ADR-0007: Entity Framework Core with Code-First Migrations

Adopts Entity Framework Core as the ORM with code-first migrations for database schema management. Entity configurations implement `IEntityTypeConfiguration<T>` and are located in Infrastructure layer. Migrations are generated via `dotnet ef migrations add` and applied via `dotnet ef database update`. This provides type-safe database access, automatic schema generation, migration history tracking, and support for multiple databases. EF Core configuration stays in Infrastructure layer only; Domain layer remains pure.

**When to reference**: EF Core attributes in Domain ([Table], [Column]), missing entity configurations, N+1 query problems.

### ADR-0006: Outbox Pattern for Domain Events

Implements the outbox pattern for reliable domain event publishing. Domain events are registered on aggregates (e.g., `CustomerCreatedDomainEvent`), stored in an outbox table, and published asynchronously by a background worker. This ensures at-least-once delivery, maintains transaction consistency (events published only if aggregate persisted successfully), and enables loose coupling between aggregates and modules. Domain events use past-tense naming and derive from `DomainEvent`.

**When to reference**: Domain event naming (not past tense), missing domain events, synchronous event publishing, event reliability concerns.

---

## Application Layer

### ADR-0011: Application Logic in Commands & Queries

Defines that application layer orchestrates use cases via commands and queries but contains NO business logic. Commands/queries follow naming pattern `[Entity][Action]Command/Query`, use nested `Validator` classes for FluentValidation, and handlers delegate to domain methods. Handlers use repository abstractions (not DbContext), coordinate domain objects, and return `Result<T>`. Business rules belong in the domain layer; handlers simply coordinate.

**When to reference**: Business logic in handlers, command/query naming violations, handlers with validation logic.

### ADR-0009: FluentValidation Strategy

Adopts FluentValidation for input validation in the application layer. Validators are nested classes within commands/queries (e.g., `CustomerCreateCommand.Validator : AbstractValidator<CustomerCreateCommand>`), automatically discovered and executed by `ValidationPipelineBehavior`. This provides fluent, strongly-typed validation rules, clear validation messages, and separation of input validation (Application layer, FluentValidation) from business rule validation (Domain layer, Result<T>).

**When to reference**: Missing validators, validators in separate files, manual validation in handlers.

### ADR-0010: Mapster for Object Mapping

Uses Mapster for object mapping between domain entities and DTOs. Mapping configurations are defined in module-specific `MapperRegister` classes that implement `IRegister`. Mapster is registered via `services.AddMapping().WithMapster<CoreModuleMapperRegister>()`. This provides fast, convention-based mapping with explicit configuration for complex scenarios. Avoids ad-hoc inline mapping in handlers; all mapping logic centralized in `MapperRegister`.

**When to reference**: Ad-hoc mapping in handlers, missing mapping configurations, manual object construction.

---

## Error Handling

### ADR-0002: Result Pattern for Error Handling

Adopts the Result<T> pattern (railway-oriented programming) for all expected failures (validation errors, business rule violations, not-found scenarios). Domain methods and application handlers return `Result<T>` with functional operators (`.Bind()`, `.Ensure()`, `.Tap()`) for composition. Exceptions are reserved for truly exceptional circumstances (system failures, bugs). Endpoints map `Result<T>` to HTTP responses via `.MapHttpOk()`, `.MapHttpCreated()`, `.MapHttpNoContent()`. This makes success/failure explicit in method signatures, enables functional composition, provides rich error context, and improves testability.

**When to reference**: Exceptions for business rules, manual if/else checks on Result, missing Result<T> returns.

---

## Presentation & API

### ADR-0014: Minimal API Endpoints with DTO Exposure

Uses ASP.NET Core minimal APIs for endpoint definition. Endpoints derive from `EndpointsBase`, use `.MapGroup()` for common route prefixes, and are thin adapters that delegate to application layer via `IRequester.SendAsync()`. Endpoints expose DTOs (never domain entities directly), use parameter binding attributes (`[FromRoute]`, `[FromBody]`, `[FromServices]`), and include OpenAPI metadata (`.WithName()`, `.WithSummary()`, `.Produces<T>()`). No business logic in endpoints.

**When to reference**: Business logic in endpoints, direct handler injection, missing OpenAPI metadata.

### ADR-0005: Requester/Notifier (Mediator) Pattern

Adopts the mediator pattern via bITdevKit's `IRequester` (for commands/queries) and `INotifier` (for events). Endpoints inject `IRequester` and call `await requester.SendAsync(command, ct)` to delegate to handlers. This decouples presentation from application handlers, enables pipeline behaviors (validation, retry, timeout) to be applied transparently, and supports loose coupling between modules via integration events. Handlers are registered automatically; endpoints never instantiate handlers directly.

**When to reference**: Direct handler injection in endpoints, missing IRequester usage, bypassing pipeline behaviors.

---

## Infrastructure

### ADR-0004: Repository Pattern with Decorator Behaviors

Implements the repository pattern with `IGenericRepository<T>` abstraction for data access. Repository implementations are registered via `.AddEntityFrameworkRepository<TEntity, TDbContext>()` with decorator behaviors chained (logging, audit, domain events, tracing). Application layer uses repository abstractions (never DbContext directly). This provides abstraction over persistence mechanism, enables cross-cutting concerns via decorators, maintains layer boundaries (Application → Domain, not Application → Infrastructure), and supports testability via repository mocks.

**When to reference**: DbContext in Application layer, missing repository abstractions, no decorator behaviors.

### ADR-0019: Specification Pattern for Repository Queries

Uses the specification pattern to encapsulate complex query logic into reusable, testable, composable objects. Specifications implement `ISpecification<T>` and define filtering (`.AddExpression()`), includes (`.AddInclude()`), ordering (`.AddOrdering()`), and paging. Used with repositories: `repository.FindAllAsync(specification, ct)`. This keeps EF Core specifics out of Application layer, enables specification reuse, improves testability, and prevents N+1 query problems via explicit includes.

**When to reference**: Inline LINQ queries, EF Core `.Include()` in Application layer, N+1 query problems, missing query encapsulation.

### ADR-0015: Background Jobs & Scheduling with Quartz.NET

Uses Quartz.NET for background job scheduling and execution. Jobs implement `IJob` and are registered via bITdevKit extensions in module registration. Typical jobs: cleanup tasks, email sending, report generation, integration event processing. Quartz provides cron-based scheduling, persistent job store, retry/failure handling, and clustering support. Jobs follow same architectural patterns (delegate to domain, use repository abstractions).

**When to reference**: Background job implementation, scheduled tasks, long-running operations.

### ADR-0016: Logging & Observability Strategy (Serilog)

Adopts Serilog for structured logging with enrichers for correlation ID, module name, and context. Logs are written to console, file, and optionally Application Insights. Uses structured logging templates (e.g., `logger.LogInformation("Customer {CustomerId} created", customer.Id)`). Correlation ID propagated via `CorrelationId` middleware. This enables log aggregation, searching, and tracing across distributed operations.

**When to reference**: Logging best practices, correlation ID propagation, sensitive PII in logs.

### ADR-0018: Dependency Injection & Service Lifetime Management

Uses ASP.NET Core dependency injection with explicit service lifetimes: Scoped (per HTTP request, e.g., repositories, handlers), Singleton (application lifetime, e.g., IRequester, IMapper), Transient (per resolution, rare). Modules register services via `IModule` interface. This ensures proper resource management, prevents scope leaks (Singleton depending on Scoped), and supports testability via constructor injection.

**When to reference**: Service lifetime issues, scope leaks, dependency injection registration.

---

## Testing

### ADR-0013: Unit Testing Strategy with High Coverage Goals

Adopts comprehensive unit testing with xUnit, NSubstitute (mocking), and Shouldly (assertions). Tests focus on handlers, domain logic (aggregates, value objects, business rules), and mapping. Tests follow AAA pattern (Arrange, Act, Assert) and naming convention `Should_ExpectedBehavior_When_Condition`. Targets high test coverage (>80%) with focus on critical business logic. Tests use repository test doubles (NSubstitute) to avoid database dependencies.

**When to reference**: Missing tests, test naming violations, test independence issues, test coverage concerns.

### ADR-0017: Integration Testing Strategy

Implements integration tests using WebApplicationFactory to test full request/response flow including database operations. Tests use in-memory or test database (SQL Server LocalDB), reset between tests for isolation, and follow AAA pattern. Integration tests verify endpoint routing, command/query handling, persistence, validation, and HTTP response mapping. This complements unit tests by testing integration points and infrastructure.

**When to reference**: Integration test setup, database test isolation, full flow testing.

---

## Security & Authentication

### ADR-0020: JWT Bearer Authentication & Authorization Strategy

Implements JWT Bearer token authentication for API endpoints. Tokens contain user identity and claims, are validated by ASP.NET Core middleware, and enable stateless authentication. Authorization uses role/policy-based checks via `[Authorize]` attributes or `.RequireAuthorization()` on endpoints. This supports distributed authentication, stateless API access, and fine-grained authorization.

**When to reference**: Authentication/authorization implementation, endpoint security, JWT token handling.

---

## Using This Reference

**During Reviews**:
1. Identify the pattern or concern (e.g., layer boundaries, CQRS, Result<T>)
2. Find the relevant ADR above
3. Reference the ADR number and title in feedback (e.g., "Violates ADR-0001: Clean/Onion Architecture")
4. Use the one-paragraph summary to explain why it matters
5. Read the full ADR at `docs/ADR/00XX-title.md` for detailed context

**Quick Lookup by Topic**:
- **Layer boundaries**: ADR-0001
- **CQRS**: ADR-0011
- **Result<T>**: ADR-0002
- **Domain logic**: ADR-0012
- **Repository pattern**: ADR-0004
- **Specifications**: ADR-0019
- **Value objects / Aggregates**: ADR-0012
- **Domain events**: ADR-0006
- **Typed IDs**: ADR-0008
- **Validation**: ADR-0009
- **Mapping**: ADR-0010
- **Endpoints**: ADR-0014
- **IRequester/INotifier**: ADR-0005
- **Modular monolith**: ADR-0003
- **Testing**: ADR-0013, ADR-0017

**Complete ADR List**: See [docs/ADR/README.md](../../../docs/ADR/README.md) for all 20 ADRs with links.
