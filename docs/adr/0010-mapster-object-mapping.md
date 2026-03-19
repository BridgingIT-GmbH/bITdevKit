# ADR-0010: Mapster for Object Mapping

## Status

Accepted

## Context

The application requires object-to-object mapping between domain entities and Data Transfer Objects (DTOs). The mapping layer sits between Domain and Presentation/Application layers and handles:

### Technical Requirements

- **Domain to DTO**: Map domain aggregates/entities to DTOs for API responses
- **DTO to Domain**: Reconstruct domain objects from API request DTOs
- **Value Object Conversions**: Map value objects (e.g., `EmailAddress`, `CustomerId`) to primitives (string, Guid)
- **Enumeration Conversions**: Map `Enumeration` types to strings and vice versa
- **Nested Collections**: Handle complex mappings (e.g., Customer with Addresses collection)
- **Performance**: Minimal overhead for high-throughput scenarios

### Business Requirements

- **Separation of Concerns**: Domain entities should not be exposed directly to API consumers
- **API Stability**: DTOs shield API consumers from internal domain model changes
- **Developer Productivity**: Reduce boilerplate mapping code
- **Type Safety**: Compile-time detection of mapping mismatches

### Design Challenges

- **Mapping Location**: Where should mappings be defined? (Application, Presentation, Infrastructure)
- **Configuration**: Convention-based (automatic) vs explicit mapping configuration
- **Value Object Complexity**: Domain uses value objects; DTOs use primitives
- **Aggregate Reconstruction**: Domain factory methods return `Result<T>`; mappers expect simple constructors

### Related Decisions

- **ADR-0001**: Clean/Onion Architecture - Mapping happens at layer boundaries (not inside Domain)
- **ADR-0002**: Result Pattern - Domain factory methods return `Result<T>`; mapping must handle this
- **ADR-0008**: Typed Entity IDs - Mappers must convert typed IDs to/from primitives
- **ADR-0011**: Application Logic in Commands/Queries - Handlers use `IMapper` to transform entities to DTOs

## Decision

Use **Mapster** as the object mapping library with **explicit mapping configurations** defined in module-specific `MapperRegister` classes located in the Presentation layer.

### How It Works

#### 1. Module Mapper Register

Each module defines mapping configurations in a dedicated class:

```csharp
// src/Modules/CoreModule/CoreModule.Presentation/CoreModuleMapperRegister.cs (lines 15-108)
public class CoreModuleMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Customer -> CustomerModel (Domain to DTO)
        config.ForType<Customer, CustomerModel>()
            .Map(dest => dest.ConcurrencyVersion,
                 src => src.ConcurrencyVersion.ToString())
            .Map(dest => dest.Addresses,
                 src => src.Addresses)
            .IgnoreNullValues(true);

        // CustomerModel -> Customer (DTO to Domain)
        config.ForType<CustomerModel, Customer>()
            .ConstructUsing(src => Customer.Create(
                src.FirstName,
                src.LastName,
                src.Email,
                src.Number).Value)  // Extract from Result<Customer>
            .Map(dest => dest.ConcurrencyVersion,
                 src => src.ConcurrencyVersion != null ? Guid.Parse(src.ConcurrencyVersion) : Guid.Empty)
            .Ignore(dest => dest.Addresses)  // Managed via AddAddress/RemoveAddress methods
            .IgnoreNullValues(true);

        // Address -> CustomerAddressModel
        config.ForType<Address, CustomerAddressModel>()
            .Map(dest => dest.Id,
                 src => src.Id.Value.ToString())  // AddressId -> string
            .IgnoreNullValues(true);

        // CustomerAddressModel -> Address
        config.ForType<CustomerAddressModel, Address>()
            .ConstructUsing(src => Address.Create(
                src.Name,
                src.Line1,
                src.Line2,
                src.PostalCode,
                src.City,
                src.Country,
                src.IsPrimary).Value)  // Extract from Result<Address>
            .IgnoreNullValues(true);

        // Value object conversions
        config.NewConfig<EmailAddress, string>()
            .MapWith(src => src.Value);

        config.NewConfig<string, EmailAddress>()
            .MapWith(src => EmailAddress.Create(src).Value);

        config.NewConfig<CustomerNumber, string>()
            .MapWith(src => src.Value);

        config.NewConfig<string, CustomerNumber>()
            .MapWith(src => CustomerNumber.Create(src).Value);

        // Enumeration conversions
        RegisterConverter<CustomerStatus>(config);
    }

    private static void RegisterConverter<T>(TypeAdapterConfig config)
       where T : Enumeration
    {
        // Enumeration -> string
        config.NewConfig<T, string>()
            .MapWith(src => src.Value);

        // string -> Enumeration
        config.NewConfig<string, T>()
            .MapWith(src => Enumeration.GetAll<T>().FirstOrDefault(x => x.Value == src));
    }
}
```

#### 2. Registration in Startup

Register Mapster with module-specific configurations:

```csharp
// src/Presentation.Web.Server/Program.cs (line 38)
builder.Services.AddMapping().WithMapster();
```

This scans assemblies for `IRegister` implementations and registers all mapping configurations.

#### 3. Usage in Handlers

Inject `IMapper` and use for transformations:

```csharp
// src/Modules/CoreModule/CoreModule.Application/Commands/CustomerCreateCommandHandler.cs (lines 21, 132)
public class CustomerCreateCommandHandler(
    ILogger<CustomerCreateCommandHandler> logger,
    IMapper mapper,
    IGenericRepository<Customer> repository,
    ...)
{
    protected override async Task<Result<CustomerModel>> HandleAsync(...)
    {
        // ... business logic ...

        // Map domain entity to DTO
        return mapper.Map<Customer, CustomerModel>(ctx.Entity);
    }
}
```

#### 4. Usage in Queries

Map collections efficiently using LINQ projection:

```csharp
// src/Modules/CoreModule/CoreModule.Application/Queries/CustomerFindAllQueryHandler.cs (line 53)
var customers = await repository
    .FindAllResultAsync(cancellationToken: cancellationToken)
    .Map(mapper.Map<Customer, CustomerModel>);  // Maps entire collection
```

### Key Mapping Patterns

#### Domain Factory Methods

When DTO-to-Domain mapping involves factory methods returning `Result<T>`:

```csharp
// Extract .Value from Result<T>
.ConstructUsing(src => Customer.Create(...).Value)
```

**Note**: This assumes input validation already occurred in FluentValidation pipeline. If `.Value` fails, it's a programming error (not user error).

#### Typed IDs to/from Primitives

```csharp
// CustomerId -> Guid
.Map(dest => dest.Id, src => src.Id.Value)

// Guid -> CustomerId
.Map(dest => dest.Id, src => CustomerId.Create(value))
```

#### Value Objects

```csharp
// EmailAddress -> string
config.NewConfig<EmailAddress, string>()
    .MapWith(src => src.Value);

// string -> EmailAddress
config.NewConfig<string, EmailAddress>()
    .MapWith(src => EmailAddress.Create(src).Value);
```

#### Enumerations

```csharp
// CustomerStatus -> string
config.NewConfig<CustomerStatus, string>()
    .MapWith(src => src.Value);

// string -> CustomerStatus
config.NewConfig<string, CustomerStatus>()
    .MapWith(src => Enumeration.GetAll<CustomerStatus>().FirstOrDefault(x => x.Value == src));
```

#### Ignored Properties

Use `.Ignore()` for properties managed via domain methods (not direct mapping):

```csharp
// Addresses managed via Customer.AddAddress(), not direct assignment
.Ignore(dest => dest.Addresses)
```

## Rationale

### Why Mapster Over AutoMapper?

1. **Performance**: Mapster generates IL code at runtime (up to 10x faster than AutoMapper reflection)
2. **Simplicity**: Simpler API with less configuration boilerplate
3. **Modern Design**: Built for .NET Core+ with async support
4. **Explicit Conversions**: `MapWith()` for value object conversions (clearer than AutoMapper's type converters)
5. **Less Magic**: More predictable behavior with less "convention magic"

### Why Explicit Configuration Over Conventions?

1. **Clarity**: Explicit mappings are self-documenting (no guessing how mapping works)
2. **Control**: Fine-grained control over value object conversions, factory methods
3. **Refactoring Safety**: Renaming properties breaks mappings at compile time (conventions fail silently at runtime)
4. **Complex Mappings**: Domain factory methods returning `Result<T>` require explicit configuration

### Why Presentation Layer for Mapping?

1. **Layering**: Presentation is the boundary where domain meets DTOs
2. **No Domain Pollution**: Domain layer remains pure (no mapping attributes/configurations)
3. **Module Cohesion**: Each module owns its own mapping configuration

### Why IMapper Abstraction Over Direct Mapster?

1. **Testability**: Handlers can mock `IMapper` interface in tests
2. **Flexibility**: Can swap mapping library without changing handler code
3. **Dependency Inversion**: Application layer depends on abstraction (not concrete Mapster types)

## Consequences

### Positive

- **Performance**: Mapster generates fast IL code (minimal overhead vs manual mapping)
- **Productivity**: Eliminates 100+ lines of manual mapping code per aggregate
- **Type Safety**: Compile-time detection of mapping mismatches
- **Testability**: `IMapper` interface easily mocked in unit tests
- **Maintainability**: All mappings in one place per module (easy to find and update)
- **Flexibility**: Can add custom mapping logic for complex scenarios
- **Clear Boundaries**: Explicit separation between domain entities and DTOs

### Negative

- **Learning Curve**: Developers must learn Mapster API (`ForType`, `MapWith`, `ConstructUsing`)
- **Boilerplate**: Explicit configurations require more code than pure conventions
- **Result<T> Handling**: Extracting `.Value` assumes validation already occurred (risk if validation bypassed)
- **Mapping Errors**: Runtime errors if mapping misconfigured (e.g., forgot to register value object conversion)
- **Two-Way Sync**: Must maintain both domain-to-DTO and DTO-to-domain mappings

### Neutral

- **Library Dependency**: Requires Mapster NuGet package (stable, actively maintained)
- **Registration Overhead**: Mapster scans assemblies at startup (minimal delay)
- **Memory**: Mapping configurations held in memory (negligible impact)

## Alternatives Considered

### 1. AutoMapper

**Description**: Popular object-to-object mapper with extensive features.

**Pros**:

- Mature: Widely used, extensive documentation
- Conventions: Auto-map properties with matching names
- Profile pattern: Organize mappings by feature

**Cons**:

- **Performance**: Uses reflection (slower than Mapster's IL generation)
- **Complexity**: Steeper learning curve; many configuration options
- **Magic**: Convention-based mapping can be unpredictable
- **Value Converters**: Verbose syntax for value object conversions

**Rejected Because**: Mapster provides better performance with simpler API.

### 2. Manual Mapping (Extension Methods)

**Description**: Write manual mapping methods for each entity/DTO pair.

**Example**:

```csharp
public static CustomerModel ToModel(this Customer customer)
{
    return new CustomerModel
    {
        Id = customer.Id.Value,
        FirstName = customer.FirstName,
        LastName = customer.LastName,
        Email = customer.Email.Value,
        // ... 20+ more properties
    };
}
```

**Pros**:

- Full control: No library dependency
- Explicit: Easy to understand what's happening
- Debuggable: Step through mapping logic

**Cons**:

- **Massive Boilerplate**: 50-100 lines per entity/DTO pair
- **Maintenance Burden**: Must update mappings manually when properties change
- **Error-Prone**: Easy to forget mapping new properties
- **No Collection Mapping**: Must manually map each item in collections

**Rejected Because**: Massive code duplication; not maintainable at scale.

### 3. Direct DTO Exposure (No Mapping)

**Description**: Expose domain entities directly as API responses (no DTOs).

**Pros**:

- Simple: No mapping layer needed
- No duplication: Single model for domain and API

**Cons**:

- **API Coupling**: API contract tied to internal domain model (breaks when domain changes)
- **Overfetching**: Expose internal properties consumers shouldn't see (e.g., AuditState)
- **Security**: Risk exposing sensitive domain data
- **Serialization Issues**: Domain entities may not serialize cleanly (circular references, lazy loading)

**Rejected Because**: Violates separation of concerns; breaks API stability.

### 4. Reflection-Based Mapper (Custom)

**Description**: Build custom mapper using reflection to copy properties.

**Pros**:

- No library dependency
- Conventions-based (auto-map matching properties)

**Cons**:

- **Performance**: Reflection overhead on every mapping call
- **Reinventing Wheel**: Duplicates work already done by Mapster/AutoMapper
- **Maintenance**: Must maintain custom mapping logic
- **Missing Features**: No value object conversions, enumerations, etc.

**Rejected Because**: Worse performance than Mapster with no benefits.

## Related Decisions

- **ADR-0001**: Clean/Onion Architecture - Mapping happens at Presentation layer boundary
- **ADR-0002**: Result Pattern - Mapping extracts `.Value` from `Result<T>` (assumes validation passed)
- **ADR-0008**: Typed Entity IDs - Mapping converts typed IDs to/from primitives
- **ADR-0009**: FluentValidation Strategy - Input validation occurs before mapping (ensures `.Value` safe)
- **ADR-0011**: Application Logic in Commands/Queries - Handlers use `IMapper` for entity-to-DTO transformation

## References

- [Mapster Documentation](https://github.com/MapsterMapper/Mapster)
- [Mapster vs AutoMapper Performance](https://github.com/MapsterMapper/Mapster/wiki/Benchmark)
- [bITdevKit Mapping Extensions](https://github.com/BridgingIT-GmbH/bITdevKit/tree/main/docs)
- Project Documentation: `README.md` (Presentation Layer section)
- Module Documentation: `src/Modules/CoreModule/CoreModule-README.md` (Mapping)

## Notes

### Key Implementation Files

```text
src/Modules/CoreModule/CoreModule.Presentation/
└── CoreModuleMapperRegister.cs           # All mapping configurations (lines 15-108)

src/Modules/CoreModule/CoreModule.Application/
├── Commands/
│   ├── CustomerCreateCommandHandler.cs   # Uses IMapper (lines 21, 132)
│   └── CustomerUpdateCommandHandler.cs   # Uses IMapper
└── Queries/
    ├── CustomerFindAllQueryHandler.cs    # Uses IMapper (line 53)
    └── CustomerFindOneQueryHandler.cs    # Uses IMapper

src/Presentation.Web.Server/
└── Program.cs                            # Registers Mapster (line 38)

tests/Modules/CoreModule/CoreModule.UnitTests/
└── CoreModuleTestsBase.cs                # Test setup with Mapster (line 16)
```

### Registration Pattern

```csharp
// In Program.cs or module startup
builder.Services.AddMapping().WithMapster();

// Automatically scans assemblies for IRegister implementations
// Registers all mapping configurations globally
```

### Common Mapping Scenarios

**1. Simple Entity to DTO** (auto-mapped properties):

```csharp
config.ForType<Customer, CustomerModel>()
    .IgnoreNullValues(true);
```

**2. Explicit Property Mapping**:

```csharp
config.ForType<Customer, CustomerModel>()
    .Map(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}");
```

**3. Value Object Conversion**:

```csharp
config.NewConfig<EmailAddress, string>()
    .MapWith(src => src.Value);
```

**4. Factory Method Construction**:

```csharp
config.ForType<CustomerModel, Customer>()
    .ConstructUsing(src => Customer.Create(
        src.FirstName,
        src.LastName,
        src.Email,
        src.Number).Value);
```

**5. Ignore Properties**:

```csharp
config.ForType<CustomerModel, Customer>()
    .Ignore(dest => dest.Addresses);  // Managed via domain methods
```

**6. Conditional Mapping**:

```csharp
config.ForType<Customer, CustomerModel>()
    .Map(dest => dest.DateOfBirth,
         src => src.DateOfBirth.HasValue ? src.DateOfBirth.Value : null);
```

### Testing Mappers

**Unit Test Example**:

```csharp
[Fact]
public void Should_Map_Customer_To_CustomerModel()
{
    // Arrange
    var customer = Customer.Create("John", "Doe", "john@example.com", number).Value;
    var mapper = new Mapper();  // Or inject IMapper

    // Act
    var model = mapper.Map<Customer, CustomerModel>(customer);

    // Assert
    model.FirstName.ShouldBe("John");
    model.LastName.ShouldBe("Doe");
    model.Email.ShouldBe("john@example.com");
}
```

### Handler Usage Pattern

**In Command Handlers**:

```csharp
public class CustomerCreateCommandHandler(IMapper mapper, ...)
{
    protected override async Task<Result<CustomerModel>> HandleAsync(...)
    {
        // 1. Create domain entity
        var customer = Customer.Create(...).Value;

        // 2. Persist
        await repository.InsertAsync(customer);

        // 3. Map to DTO for response
        return mapper.Map<Customer, CustomerModel>(customer);
    }
}
```

**In Query Handlers** (collection mapping):

```csharp
public class CustomerFindAllQueryHandler(IMapper mapper, ...)
{
    protected override async Task<Result<IEnumerable<CustomerModel>>> HandleAsync(...)
    {
        return await repository
            .FindAllResultAsync(cancellationToken: cancellationToken)
            .Map(mapper.Map<Customer, CustomerModel>);  // Maps entire collection
    }
}
```

### Result<T> Extraction Pattern

When domain factory methods return `Result<T>`, extract `.Value`:

```csharp
.ConstructUsing(src => Customer.Create(...).Value)
```

**Important**: This assumes input validation already occurred via FluentValidation pipeline. If `.Value` throws, it indicates:

1. Programming error (validation pipeline bypassed), OR
2. Inconsistent validation between FluentValidation and domain factory

**Mitigation**: Ensure FluentValidation rules match domain factory validation rules.

### Performance Considerations

**Mapster Performance Characteristics**:

- **First mapping**: ~1-2ms (IL code generation)
- **Subsequent mappings**: ~0.01ms (compiled IL execution)
- **Collection mapping**: Comparable to manual loops

**Optimization Tips**:

- Use `ProjectToType<T>()` for LINQ queries (maps at database level)
- Avoid mapping in tight loops (map collections in one call)
- Cache mapper instances (registered as singleton)

### Common Pitfalls

**X Don't map inside domain entities**:

```csharp
// X Wrong: Domain should not depend on mapping
public class Customer
{
    public CustomerModel ToModel(IMapper mapper) => mapper.Map<Customer, CustomerModel>(this);
}
```

**V Do map in handlers (Application layer)**:

```csharp
// V Correct: Mapping happens in Application/Presentation boundary
public class CustomerCreateCommandHandler(IMapper mapper, ...)
{
    // Map domain to DTO before returning
    return mapper.Map<Customer, CustomerModel>(customer);
}
```

**X Don't expose IMapper to domain layer**:

```csharp
// X Wrong: Domain layer depends on IMapper
public class Customer
{
    public Customer(IMapper mapper) { }  // X No!
}
```

**V Do inject IMapper in handlers only**:

```csharp
// V Correct: Only Application/Presentation layers use IMapper
public class CustomerCreateCommandHandler(IMapper mapper, ...) { }
```

**X Don't rely on conventions for complex mappings**:

```csharp
// X Risky: Value object conversion may fail silently
config.ForType<Customer, CustomerModel>();  // No explicit EmailAddress -> string mapping
```

**V Do explicitly configure value object conversions**:

```csharp
// V Correct: Explicit value object conversions
config.NewConfig<EmailAddress, string>().MapWith(src => src.Value);
config.ForType<Customer, CustomerModel>()
    .Map(dest => dest.Email, src => src.Email);  // Now works correctly
```

### Mapping vs Domain Logic Separation

| Concern | Mapping (Mapster) | Domain Logic |
|---------|------------------|--------------|
| **Location** | Presentation layer (MapperRegister) | Domain layer (Entities/Value Objects) |
| **Purpose** | Transform data between layers | Enforce business rules |
| **When** | After handler completes (for response) | During entity creation/modification |
| **What** | Property copying, type conversion | Validation, invariants, business logic |
| **Dependencies** | Depends on Domain (references entities) | No dependencies (pure domain) |

### Future Considerations

- **GraphQL**: Mapster supports GraphQL projection (map only requested fields)
- **API Versioning**: Create separate DTOs per API version; map domain to correct version
- **Caching**: Cache frequently-mapped objects (e.g., lookup tables)
- **Bulk Mapping**: Use `Adapt()` extension for bulk mapping scenarios
- **Async Mapping**: Mapster supports async mapping for lazy-loaded properties (use with caution)
