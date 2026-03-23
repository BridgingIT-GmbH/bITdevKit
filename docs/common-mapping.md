# Common Mapping

`Common.Mapping` provides the devkit's object-mapping building block. It sits at the boundary between domain/application code and presentation models, and it is designed to keep mapping logic explicit, testable, and easy to register per module.

The package uses Mapster as the default engine, but it keeps a small abstraction layer in front of it so application code depends on `IMapper` instead of a concrete mapper implementation.

## What It Provides

- `IMapper` and `IMapper<TSource, TTarget>` as the abstraction used by application code.
- `AddMapping()` and `WithMapster()` for DI registration and configuration.
- `MapsterMapper` and `MapsterMapper<TSource, TDestination>` as the Mapster-backed implementation.
- `ObjectMapper` and `ObjectMapperConfiguration<TSource, TTarget>` as a lightweight manual mapping path.
- `MapperExtensions`, `ResultExtensions`, and `ResultTaskExtensions` for mapping collections and `Result<T>` values.

## Recommended Registration

Mapping is normally registered in the presentation layer, per module, using Mapster registration classes.

```csharp
builder.Services
    .AddMapping()
    .WithMapster<MyModuleMapperRegister>();
```

You can also scan assemblies or pass a `TypeAdapterConfig` directly when you need broader control:

```csharp
builder.Services
    .AddMapping(builder.Configuration)
    .WithMapster(typeof(MyModuleMapperRegister).Assembly);
```

`WithMapster()` scans for Mapster `IRegister` implementations, creates a `TypeAdapterConfig`, and registers both Mapster's `IMapper` and the devkit `IMapper` abstraction.

## How To Use It

Inject `IMapper` into application handlers or endpoint code when translating between domain objects and DTOs:

```csharp
public class CustomerFindOneQueryHandler(IMapper mapper)
{
    public CustomerModel Handle(Customer customer)
    {
        return mapper.Map<Customer, CustomerModel>(customer);
    }
}
```

The package also includes helpers for mapping result-based workflows without losing the original messages or errors:

```csharp
var customerResult = await repository.FindOneAsync(customerId, cancellationToken);
var modelResult = customerResult.MapResult<Customer, CustomerModel>(mapper);
```

For async result pipelines, use `MapResultAsync()` on `Task<Result<T>>` when you want cancellation and exceptions to be translated into result errors in a consistent way.

## When To Use ObjectMapper

`ObjectMapper` is the manual, explicit alternative. It is useful when you want a tiny mapping surface for tests, special cases, or very controlled property-to-property mapping.

```csharp
var mapper = new ObjectMapper()
    .For<Customer, CustomerModel>()
    .Map(x => x.Id, x => x.Id)
    .MapCustom(x => x.FullName, x => x.DisplayName)
    .Apply();
```

Prefer Mapster for normal application mapping. `ObjectMapper` is intentionally small and reflection-based, so it is better suited to simple scenarios than to complex object graphs.

## Caveats

- Keep mapping at layer boundaries. The ADR guidance for the devkit places mapping in presentation/application boundary code, not in the domain model. See [ADR-0010: Mapster for Object Mapping](./adr/0010-mapster-object-mapping.md).
- `ObjectMapper` expects the target type to be constructible and its mapped members to be writable by name.
- `ObjectMapperConfiguration` is intentionally simple. If you need complex conversions, custom constructors, or rich nested object handling, use Mapster registrations instead.
- Mapping failures are usually configuration problems, so each module should have tests that exercise its mapper registrations.

## Related Docs

- [Application Commands and Queries](./features-application-commands-queries.md)
- [Presentation Endpoints](./features-presentation-endpoints.md)
- [Results](./features-results.md)
- [ADR-0010: Mapster for Object Mapping](./adr/0010-mapster-object-mapping.md)
- [ADR-0011: Application Logic in Commands/Queries](./adr/0011-application-logic-in-commands-queries.md)
- [ADR-0014: Minimal API Endpoints and DTO Exposure](./adr/0014-minimal-api-endpoints-dto-exposure.md)
