# Common Caching

> Provide a small, shared in-process caching abstraction with a default memory-cache implementation.

`Common.Caching` provides the devkit's in-process caching building block. It centers around the shared `ICacheProvider` contract from `Common.Abstractions` and ships the default `IMemoryCache` implementation plus the DI wiring needed to register it.

The package is intentionally small. It gives other packages a consistent cache abstraction without forcing them to depend directly on `IMemoryCache`.

## What It Provides

- `ICacheProvider` as the shared cache contract.
- `AddCaching()` and `WithInMemoryProvider()` for DI registration.
- `CachingBuilderContext` as the fluent registration context.
- `InMemoryCacheProviderConfiguration` for sliding and absolute expiration defaults.
- `InMemoryCacheProvider` as the default implementation.
- `MemoryCacheExtensions` for key enumeration and prefix-based invalidation.

## Recommended Registration

Register caching during application startup, usually with settings from configuration:

```csharp
builder.Services
    .AddCaching(builder.Configuration)
    .WithInMemoryProvider(new InMemoryCacheProviderConfiguration
    {
        SlidingExpiration = TimeSpan.FromMinutes(10)
    });
```

If you omit the explicit configuration object, `WithInMemoryProvider()` will bind `Caching:InProcess` from configuration and fall back to a default `InMemoryCacheProviderConfiguration`.

`AddCaching()` also registers `IMemoryCache` if it is not already present.

## How To Use It

Use the provider for simple read-through caching, lookup tables, and invalidation patterns that fit a single process:

```csharp
public class CustomerCacheService(ICacheProvider cache)
{
    public async Task<CustomerModel> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (await cache.TryGetAsync(key, out CustomerModel value, cancellationToken))
        {
            return value;
        }

        value = await LoadCustomerAsync(cancellationToken);
        await cache.SetAsync(key, value, slidingExpiration: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);

        return value;
    }
}
```

The provider exposes the expected cache operations:

- `Get` and `GetAsync`
- `TryGet` and `TryGetAsync`
- `Set` and `SetAsync`
- `Remove` and `RemoveAsync`
- `RemoveStartsWith` and `RemoveStartsWithAsync`
- `GetKeys` and `GetKeysAsync`

Prefix-based keys work especially well with `RemoveStartsWith()`, so a stable key naming convention is important.

## Integration Points

- `DocumentStorage` uses cache behaviors to speed up repeated reads.
- `Requester and Notifier` use cache invalidation behaviors for request-driven workflows.
- `ADRs` around service lifetimes and dependency injection reference caching as a shared infrastructure concern. See [ADR-0018: Dependency Injection Service Lifetimes](./adr/0018-dependency-injection-service-lifetimes.md).

## Caveats

- This package only ships an in-memory provider. It does not provide a distributed cache implementation.
- `MemoryCacheExtensions.GetKeys()` and the prefix helpers inspect `MemoryCache` internals. They are useful operational helpers, but they are not the same kind of stable contract as `ICacheProvider` itself.
- `LoggingCacheProviderBehavior` currently exists in the codebase, but its members throw `NotImplementedException`. Do not treat it as a supported production decorator yet.
- Cache keys should be explicit and predictable. The invalidation helpers are most effective when keys share a clear prefix per feature or aggregate.

## Related Docs

- [DocumentStorage](./features-storage-documents.md)
- [Requester and Notifier](./features-requester-notifier.md)
- [StartupTasks](./features-startuptasks.md)
- [ADR-0018: Dependency Injection Service Lifetimes](./adr/0018-dependency-injection-service-lifetimes.md)
