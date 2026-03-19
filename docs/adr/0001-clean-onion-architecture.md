# ADR-0001: Clean/Onion Architecture with Strict Layer Boundaries

## Status

Accepted

## Context

When building enterprise applications, maintaining long-term maintainability and testability requires a clear architectural structure. Traditional layered architectures often suffer from:

- **Tight coupling** between business logic and infrastructure concerns (databases, frameworks, external services)
- **Difficulty testing** core business logic without standing up infrastructure
- **Framework lock-in** where changing a framework requires rewriting business logic
- **Unclear dependencies** leading to circular references and tangled codebases
- **Fragile architecture** that degrades over time as boundaries erode

The application needed an architecture that:

1. Protects core business logic from infrastructure changes
2. Enables independent testing of business rules
3. Makes dependency directions explicit and enforceable
4. Supports long-term maintainability as the codebase grows
5. Allows infrastructure technology changes without rewriting business logic

## Decision

Adopt **Clean/Onion Architecture** with strictly enforced layer boundaries and inward-pointing dependencies.

### Layer Structure (Inside-Out)

1. **Domain Layer** (innermost): Pure business logic
   - Aggregates, Entities (e.g., `Customer`)
   - Value Objects (e.g., `EmailAddress`, `CustomerNumber`)
   - Domain Events (e.g., `CustomerCreatedDomainEvent`)
   - Business Rules (e.g., `EmailShouldBeUniqueRule`)
   - Enumerations (e.g., `CustomerStatus`)
   - **Dependencies**: None (only bITdevKit domain abstractions)

2. **Application Layer**: Use case orchestration
   - Commands & Queries (e.g., `CustomerCreateCommand`)
   - Request/Response Handlers
   - DTOs (e.g., `CustomerModel`)
   - Specifications for queries
   - **Dependencies**: Domain layer only

3. **Infrastructure Layer**: Technical implementation
   - DbContext and EF Core configurations
   - Repository implementations
   - External service integrations
   - Migrations
   - **Dependencies**: Domain and Application (implements their abstractions)

4. **Presentation Layer**: User/API interface
   - Minimal API endpoints
   - Module registration
   - Mapping profiles
   - **Dependencies**: Application layer (through IRequester/INotifier)

### Dependency Rule

**Dependencies point inward only**. Inner layers must never depend on outer layers.

- Domain → None
- Application → Domain
- Infrastructure → Domain + Application
- Presentation → Application

## Rationale

1. **Persistence Ignorance**: Domain logic doesn't know about databases, allowing database technology changes without domain rewrites
2. **Testability**: Domain and Application can be tested independently of infrastructure
3. **Framework Independence**: Business logic isn't coupled to ASP.NET, EF Core, or any framework
4. **Enforceability**: Architecture boundaries are validated by automated architecture tests
5. **Team Scalability**: Clear rules prevent confusion about where to place code
6. **Long-term Maintainability**: Architecture doesn't degrade because violations are caught early
7. **Technology Agnostic Core**: Domain can be reused in different contexts (web, console, microservices)

## Consequences

### Positive

- Domain logic is completely isolated and reusable across different delivery mechanisms
- Infrastructure can be replaced without touching business logic (e.g., switch from SQL Server to PostgreSQL)
- All layers can be tested independently with appropriate test doubles
- Clear separation of concerns makes codebase easier to understand and navigate
- Architecture boundaries are enforced by `ArchitectureTests.cs` preventing violations at build time
- New developers can quickly understand where to place new code

### Negative

- More projects/folders to manage (4 projects per module: Domain, Application, Infrastructure, Presentation)
- Indirection through abstractions adds some complexity (repository interfaces, etc.)
- Learning curve for developers unfamiliar with Clean Architecture principles
- Initial setup overhead when creating new modules

### Neutral

- Requires discipline to maintain boundaries (mitigated by automated tests)
- Application layer acts as orchestration coordinator between domain and infrastructure
- Each module follows the same layering pattern for consistency

## Alternatives Considered

- **Alternative 1: Traditional N-Tier Architecture (UI → Business Logic → Data Access)**
  - Rejected because it often leads to tight coupling between business logic and data access layer
  - Business logic layer typically has direct references to ORM entities and database concerns

- **Alternative 2: Anemic Domain Model with Service Layer**
  - Rejected because it pushes all logic into services, creating procedural rather than object-oriented code
  - Domain entities become simple data containers with no behavior

- **Alternative 3: Vertical Slice Architecture (no layering)**
  - Rejected at the architectural level (though modules are vertical slices)
  - Still need layering within each module to separate concerns appropriately

## Related Decisions

- [ADR-0003](0003-modular-monolith-architecture.md): Modular Monolith - defines how modules are organized
- [ADR-0011](0011-application-logic-in-commands-queries.md): Application logic placement
- [ADR-0012](0012-domain-logic-in-domain-layer.md): Domain logic placement

## References

- [Robert C. Martin - Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Jeffrey Palermo - Onion Architecture](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-1/)
- [README - Clean Architecture Overview](../../README.md#clean-architecture-overview)
- [README - Layer Responsibilities](../../README.md#layer-responsibilities)
- [CoreModule README - Architecture](../../src/Modules/CoreModule/CoreModule-README.md#architecture)

## Notes

### Enforcement Mechanism

Architecture boundaries are enforced via `ArchitectureTests.cs` in unit tests:

```csharp
[Fact]
public void Domain_Should_Not_HaveDependencyOnOtherLayers()
{
    var result = Types.InAssembly(DomainAssembly)
        .Should().NotHaveDependencyOn("Application")
        .And().NotHaveDependencyOn("Infrastructure")
        .And().NotHaveDependencyOn("Presentation")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

### Project Structure Example (CoreModule)

```
CoreModule/
├── CoreModule.Domain/              # No dependencies on other layers
├── CoreModule.Application/         # References: Domain
├── CoreModule.Infrastructure/      # References: Domain, Application
└── CoreModule.Presentation/        # References: Application
```

### Implementation Location

- **Domain logic**: `src/Modules/CoreModule/CoreModule.Domain/`
- **Application logic**: `src/Modules/CoreModule/CoreModule.Application/`
- **Infrastructure**: `src/Modules/CoreModule/CoreModule.Infrastructure/`
- **Presentation**: `src/Modules/CoreModule/CoreModule.Presentation/`
- **Architecture tests**: `tests/Modules/CoreModule/CoreModule.UnitTests/ArchitectureTests.cs`
