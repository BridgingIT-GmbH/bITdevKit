# ADR-0008: Typed Entity IDs using Source Generators

## Status

Accepted

## Context

Domain entities and aggregates require unique identifiers to distinguish instances and establish relationships. The application must decide how to represent entity IDs in code.

### Technical Requirements

- **Type safety**: Prevent mixing IDs from different entity types (e.g., `CustomerId` vs `AddressId`)
- **Compile-time enforcement**: Catch ID type mismatches at compile time, not runtime
- **Value object semantics**: IDs should behave as immutable value objects with equality comparison
- **Serialization support**: IDs must serialize to/from JSON and database primitives
- **Developer ergonomics**: Minimal boilerplate for creating new typed ID types

### Business Requirements

- **Domain correctness**: Prevent business logic bugs caused by using wrong ID type (e.g., passing `AddressId` where `CustomerId` expected)
- **Refactoring safety**: Changing entity ID type should be detectable by compiler
- **Auditability**: Clear ID types in logs and error messages (not just "Guid: 12345...")

### Design Challenges

- **Boilerplate vs Safety**: Strongly-typed IDs require creating new types for each entity
- **Primitive obsession**: Using raw `Guid` or `int` everywhere leads to untyped ID soup
- **Performance**: Wrapping primitives adds memory overhead (struct vs class)
- **EF Core mapping**: ORM must convert typed IDs to database primitives seamlessly

### Related Decisions

- **ADR-0007**: Entity Framework Core - Value conversions enable typed ID persistence
- **ADR-0012**: Domain Logic in Domain Layer - IDs are domain value objects
- **ADR-0002**: Result Pattern - ID creation returns `Result<TId>` for validation

## Decision

Use **strongly-typed entity IDs generated via source generators** with the `[TypedEntityId<T>]` attribute from bITdevKit.

### How It Works

#### 1. Attribute-Driven ID Generation

Mark entity classes with `[TypedEntityId<T>]` attribute:

```csharp
// src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/Customer.cs (line 14-15)
[TypedEntityId<Guid>]
public class Customer : AuditableAggregateRoot<CustomerId>, IConcurrency
{
    // Entity properties
}

// src/Modules/CoreModule/CoreModule.Domain/Model/CustomerAggregate/Address.cs (line 14-15)
[TypedEntityId<Guid>]
public class Address : Entity<AddressId>
{
    // Entity properties
}
```

#### 2. Source Generator Creates Typed ID Classes

The `BridgingIT.DevKit.Domain.CodeGen` source generator automatically creates:

```csharp
// Generated at compile time: CustomerId.g.cs
public readonly struct CustomerId : IEquatable<CustomerId>
{
    public Guid Value { get; }

    private CustomerId(Guid value) => Value = value;

    public static CustomerId Create(Guid value) => new(value);
    public static CustomerId CreateNew() => new(Guid.NewGuid());

    // Equality members
    public bool Equals(CustomerId other) => Value.Equals(other.Value);
    public override bool Equals(object obj) => obj is CustomerId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(CustomerId left, CustomerId right) => left.Equals(right);
    public static bool operator !=(CustomerId left, CustomerId right) => !left.Equals(right);

    // String conversion
    public override string ToString() => Value.ToString();
}

// Generated at compile time: AddressId.g.cs
public readonly struct AddressId : IEquatable<AddressId>
{
    // Same pattern, but distinct type
}
```

#### 3. Type Safety Enforcement

Compiler prevents mixing ID types:

```csharp
// V Correct usage
CustomerId customerId = CustomerId.CreateNew();
Customer customer = await repository.FindOneAsync(customerId);

// X Compile error: Cannot convert AddressId to CustomerId
AddressId addressId = AddressId.CreateNew();
Customer customer = await repository.FindOneAsync(addressId); // ERROR!

// X Compile error: Cannot compare different ID types
if (customerId == addressId) { } // ERROR!
```

#### 4. EF Core Value Conversions

Map typed IDs to database primitives using `HasConversion`:

```csharp
// src/Modules/CoreModule/CoreModule.Infrastructure/EntityFramework/Configurations/CustomerTypeConfiguration.cs (lines 29-34)
builder.Property(e => e.Id)
    .ValueGeneratedOnAdd()
    .HasConversion(
        id => id.Value,                 // CustomerId → Guid (to database)
        value => CustomerId.Create(value)); // Guid → CustomerId (from database)

// Owned entity configuration (lines 80-84)
ab.Property(a => a.Id)
    .ValueGeneratedOnAdd()
    .HasConversion(
        id => id.Value,                 // AddressId → Guid (to database)
        value => AddressId.Create(value)); // Guid → AddressId (from database)
```

#### 5. JSON Serialization (Automatic)

Typed IDs serialize to their underlying primitive value:

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "firstName": "John",
  "lastName": "Doe"
}
```

### Key Patterns

- **Creation**: Use `CustomerId.CreateNew()` for new entities or `CustomerId.Create(guid)` when reconstructing
- **Comparison**: Use `==` and `!=` operators (value equality semantics)
- **Null safety**: Struct types cannot be null; use `default(CustomerId)` for "empty" ID
- **Repository queries**: Pass typed IDs to `FindOneAsync(CustomerId id)`
- **Database mapping**: Always use `HasConversion` in EF Core configurations

## Rationale

### Why Typed IDs Over Primitives?

1. **Type Safety**: Compiler prevents passing `AddressId` where `CustomerId` expected (catch bugs at compile time)
2. **Domain Clarity**: Method signatures clearly express intent (`UpdateCustomer(CustomerId id)` vs `UpdateCustomer(Guid id)`)
3. **Refactoring Safety**: Changing ID type propagates through codebase automatically
4. **Self-Documenting**: Code is more readable (`CustomerId` vs `Guid`)
5. **Accidental Complexity Prevention**: Cannot accidentally concatenate IDs or use wrong ID in collection lookups
6. **IDE Support**: IntelliSense shows typed ID suggestions (not just `Guid`)

### Why Source Generators Over Manual Types?

1. **Zero Boilerplate**: No need to manually write equality, serialization, conversion logic
2. **Consistency**: All typed IDs follow same pattern (no copy-paste errors)
3. **Compile-Time Generation**: No runtime reflection or overhead
4. **Maintainability**: Update generator logic once; all IDs benefit
5. **Developer Productivity**: Just add `[TypedEntityId<Guid>]`; compiler does the rest

### Why Struct Over Class?

1. **Value Semantics**: IDs compared by value (like `int`, `Guid`), not reference
2. **Performance**: No heap allocation; better memory locality
3. **Equality by Default**: Struct equality uses value comparison automatically
4. **Immutability**: Readonly struct prevents accidental mutation

### Why Guid Over Sequential Int?

1. **Distributed Systems**: Guids can be generated anywhere without coordination
2. **Security**: Non-sequential IDs prevent enumeration attacks (e.g., guessing customer IDs)
3. **Merge-Friendly**: No ID conflicts when merging data from multiple sources
4. **Future-Proof**: Easy to add caching, read replicas, sharding without ID collisions

## Consequences

### Positive

- **Compile-Time Safety**: Catch ID type mismatches before code runs (no more "wrong ID" bugs)
- **Zero Runtime Cost**: Structs compile down to underlying primitive (no wrapper overhead)
- **Cleaner APIs**: Method signatures self-document expected ID types
- **Refactoring Confidence**: Change ID type once; compiler finds all affected code
- **Better Errors**: Exception messages show `CustomerId` not `Guid`, making debugging easier
- **No Boilerplate**: Source generator eliminates 50+ lines of code per ID type
- **Consistency**: All typed IDs have same interface (Create, Equals, ToString)

### Negative

- **Learning Curve**: Developers must understand typed IDs vs raw primitives
- **Conversion Friction**: Integrations expecting `Guid` require `.Value` property access
- **Debugger Display**: Default debugger shows struct wrapper, not underlying value (mitigated with `[DebuggerDisplay]`)
- **Source Generator Dependency**: Requires `BridgingIT.DevKit.Domain.CodeGen` package
- **Build-Time Generation**: First build after adding attribute slower (source generator runs)
- **Migration Effort**: Existing code using `Guid` requires migration to typed IDs

### Neutral

- **Struct vs Class Tradeoffs**: Structs cannot be null (use `default(CustomerId)` for "empty")
- **Generic Constraints**: Some generic methods may need `where T : struct` constraint
- **Serialization Defaults**: JSON serializers treat structs as primitives (good for simple types)

## Alternatives Considered

### 1. Raw Primitive IDs (Guid, int)

**Description**: Use `Guid` or `int` directly for entity IDs.

**Pros**:

- Simple: No additional types to learn
- Performance: No wrapper overhead
- Serialization: Works everywhere without configuration

**Cons**:

- **No Type Safety**: Can pass `Guid addressId` where `Guid customerId` expected
- **Hard to Refactor**: Changing ID type requires finding all usages manually
- **Primitive Obsession**: IDs are just "some Guid" with no domain meaning
- **Accidental Bugs**: Easy to mix up IDs in collections (`Dictionary<Guid, Customer>`)

**Rejected Because**: Sacrifices type safety for minimal convenience gain; leads to runtime bugs that compiler could catch.

### 2. Manual Typed ID Classes

**Description**: Write typed ID classes manually for each entity.

**Pros**:

- Full control over implementation
- No source generator dependency
- Can add custom logic per ID type

**Cons**:

- **Massive Boilerplate**: 50-100 lines per ID type (Create, Equals, GetHashCode, operators, serialization)
- **Copy-Paste Errors**: Easy to forget implementing `IEquatable<T>` correctly
- **Maintenance Burden**: Bug fixes require updating every ID type manually
- **Inconsistency**: Different developers implement IDs differently

**Rejected Because**: Source generator provides same benefits with zero boilerplate.

### 3. Generic ID<TEntity> Wrapper

**Description**: Use `Id<Customer>` and `Id<Address>` as generic types.

**Example**:

```csharp
public class Id<TEntity> : IEquatable<Id<TEntity>>
{
    public Guid Value { get; }
    // ... equality logic
}

Customer : Entity<Id<Customer>>
```

**Pros**:

- Single implementation for all entities
- Type safety via generic parameter
- No source generator needed

**Cons**:

- **Verbose**: `Id<Customer>` is longer than `CustomerId`
- **Generic Constraints**: Complex signatures (`Func<Id<TEntity>, bool>` vs `Func<CustomerId, bool>`)
- **JSON Serialization**: Requires custom converters for `Id<T>`
- **Ugly in Logs**: Generic type names display as `Id\`1[Customer]`
- **EF Core Friction**: Generic types harder to configure in Fluent API

**Rejected Because**: More complex than source-generated IDs with worse readability.

### 4. String-Based IDs (Strongly-Typed String)

**Description**: Use string IDs with format validation (e.g., `CUST-12345`).

**Pros**:

- Human-readable in logs and URLs
- Can encode type prefix in value (`CUST-`, `ADDR-`)

**Cons**:

- **Performance**: String comparison slower than Guid comparison
- **Memory**: Strings allocate heap memory (Guids are 16 bytes)
- **Collision Risk**: Requires central ID generation service for uniqueness
- **Format Validation**: Must validate format on deserialization
- **Database Indexing**: String indexes slower than Guid indexes

**Rejected Because**: Performance overhead and collision risk outweigh readability benefits.

### 5. Smart Enums (Enumeration Pattern)

**Description**: Use Enumeration base class for IDs.

**Cons**:

- **Static Instances**: Enumerations have fixed set of values; IDs are dynamic
- **Not Applicable**: IDs are runtime values, not compile-time constants

**Rejected Because**: Enumeration pattern is for fixed value sets, not unique identifiers.

## Related Decisions

- **ADR-0007**: Entity Framework Core - Value conversions enable typed ID persistence
- **ADR-0012**: Domain Logic in Domain Layer - Typed IDs are domain value objects
- **ADR-0002**: Result Pattern - ID validation can return Result<TId>
- **ADR-0004**: Repository Pattern - Repositories accept typed IDs in methods

## References

- [bITdevKit Domain CodeGen](https://github.com/BridgingIT-GmbH/bITdevKit/tree/main/src/Domain.CodeGen)
- [C# Source Generators Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [EF Core Value Conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- Project Documentation: `README.md` (Domain Modeling section)
- Module Documentation: `src/Modules/CoreModule/CoreModule-README.md` (Domain Layer)

## Notes

### Key Implementation Files

```
src/Modules/CoreModule/CoreModule.Domain/
├── Model/
│   └── CustomerAggregate/
│       ├── Customer.cs                    # [TypedEntityId<Guid>] attribute (line 14)
│       └── Address.cs                     # [TypedEntityId<Guid>] attribute (line 14)
└── CoreModule.Domain.csproj               # References BridgingIT.DevKit.Domain.CodeGen

src/Modules/CoreModule/CoreModule.Infrastructure/
└── EntityFramework/
    └── Configurations/
        └── CustomerTypeConfiguration.cs   # HasConversion mappings (lines 29-34, 80-84)
```

### Source Generator Configuration

```xml
<!-- CoreModule.Domain.csproj (lines 15-18) -->
<PackageReference Include="BridgingIT.DevKit.Domain.CodeGen">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>analyzers</IncludeAssets>
</PackageReference>
```

### Usage Patterns

**Creating New IDs**:

```csharp
// Generate new ID
var customerId = CustomerId.CreateNew();

// Create from existing Guid
var customerId = CustomerId.Create(guid);

// Extract underlying value
Guid guid = customerId.Value;
```

**Repository Queries**:

```csharp
// Strongly-typed repository methods
var customer = await repository.FindOneAsync(customerId); // V Type-safe
var customer = await repository.FindOneAsync(addressId);  // X Compile error!
```

**EF Core Mapping**:

```csharp
// Entity configuration
builder.Property(e => e.Id)
    .HasConversion(
        id => id.Value,                      // To database (CustomerId → Guid)
        value => CustomerId.Create(value));  // From database (Guid → CustomerId)
```

**JSON Serialization**:

```csharp
// Serialize
var json = JsonSerializer.Serialize(customer);
// { "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890", ... }

// Deserialize
var customer = JsonSerializer.Deserialize<Customer>(json);
// customer.Id is CustomerId type
```

**Equality Comparison**:

```csharp
// Value equality
CustomerId id1 = CustomerId.Create(guid);
CustomerId id2 = CustomerId.Create(guid);
bool areEqual = (id1 == id2); // V true (value comparison)

// Type safety
CustomerId customerId = CustomerId.CreateNew();
AddressId addressId = AddressId.CreateNew();
bool canCompare = (customerId == addressId); // X Compile error!
```

### Generated Code Structure

For each `[TypedEntityId<T>]` attribute, the generator creates:

1. **Readonly Struct**: Immutable value type
2. **Value Property**: Exposes underlying primitive (`Guid`, `int`, etc.)
3. **Factory Methods**: `Create(T value)` and `CreateNew()` (for Guid)
4. **Equality Members**: `Equals`, `GetHashCode`, `==`, `!=` operators
5. **String Conversion**: `ToString()` delegates to underlying value
6. **IEquatable<T>**: Proper value equality semantics

### Debugging Tips

**View Underlying Value in Debugger**:

```csharp
// Add DebuggerDisplay to entity (already present in Customer.cs, line 13)
[DebuggerDisplay("Id={Id}, Name={FirstName} {LastName}")]
public class Customer : AuditableAggregateRoot<CustomerId>
{
    // Shows: Id=a1b2c3d4-e5f6-7890-abcd-ef1234567890, Name=John Doe
}
```

**Convert to Raw Guid for Logging**:

```csharp
logger.LogInformation("Customer {CustomerId} created", customerId.Value);
```

### Migration Strategy from Raw Guids

If migrating existing codebase from `Guid` to typed IDs:

1. **Add Attribute**: Add `[TypedEntityId<Guid>]` to entity class
2. **Change Entity Base**: `Entity<Guid>` → `Entity<CustomerId>`
3. **Update Repository**: Change method signatures to accept `CustomerId`
4. **Update Configurations**: Add `HasConversion` in EF Core configurations
5. **Update Application Layer**: Change DTOs and handlers to use typed IDs
6. **Compile**: Let compiler find all affected code (search for errors)
7. **Test**: Run integration tests to verify database mapping works

### Common Patterns

**Specification with Typed IDs**:

```csharp
public class CustomerByIdSpecification : Specification<Customer>
{
    private readonly CustomerId customerId;

    public CustomerByIdSpecification(CustomerId customerId)
    {
        this.customerId = customerId;
    }

    public override Expression<Func<Customer, bool>> ToExpression()
        => e => e.Id == this.customerId; // V Type-safe comparison
}
```

**Domain Events with Typed IDs**:

```csharp
public class CustomerCreatedDomainEvent(CustomerId customerId) : DomainEventBase
{
    public CustomerId CustomerId { get; } = customerId; // V Strongly-typed
}
```

### Performance Characteristics

- **Memory**: Struct (16 bytes for Guid) vs Class (16 bytes + object header + GC overhead)
- **Comparison**: Same as underlying primitive (fast)
- **Boxing**: Avoid boxing structs in collections (use `List<CustomerId>` not `List<object>`)
- **Database**: No difference (typed IDs map to primitives)

### Future Considerations

- **Int-Based IDs**: Change `[TypedEntityId<Guid>]` to `[TypedEntityId<int>]` for sequential IDs
- **Custom ID Formats**: Extend source generator to support prefixed strings (`CUST-12345`)
- **Composite Keys**: Multi-property IDs (e.g., `TenantId + CustomerId`)
- **External System Integration**: Map typed IDs to external system IDs via value converters
