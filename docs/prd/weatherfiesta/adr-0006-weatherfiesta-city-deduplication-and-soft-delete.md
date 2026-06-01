# ADR-0006: WeatherFiesta City Deduplication and Soft-Delete Strategy

## Status

Accepted

## Context

WeatherFiesta has two entity lifecycle patterns that need clear decisions:

1. **City deduplication**: When a user subscribes to a city, the system geocodes the name and creates a City record. If another user subscribes to the same city, the system must not create a duplicate. Cities are global shared entities.

2. **Soft-delete vs hard-delete**: UserCity subscriptions use soft-delete (IsDeleted flag). Cities use hard-delete (admin only). User accounts use soft-delete. The behavior for each must be consistent and documented.

Key constraints from PRDs:
- City deduplication by ExternalId (Open-Meteo geocoding ID) or (Latitude, Longitude) pair (PRD-0000 Story 2)
- UserCity soft-delete with reactivation (PRD-0000 Story 2 AC3, Story 4)
- Admin hard-delete of City cascades to all weather data and subscriptions (PRD-0300 Story 1 AC5)
- User account soft-delete cascades to all UserCity records (PRD-0400 Story 4)
- DisplayOrder gaps are closed on soft-delete (PRD-0001 Story 3 AC4)
- IsPrimary is cleared when primary city is unsubscribed (PRD-0000 Story 4 AC6)

## Decision

### City Deduplication

Use **ExternalId as primary deduplication key**, with (Latitude, Longitude) as fallback.

1. If the request includes `externalId`, look up by ExternalId first. If found, reuse the existing City.
2. If no `externalId`, or ExternalId lookup returns nothing, look up by (Latitude, Longitude) with a small tolerance (±0.01 degrees ≈ 1.1 km).
3. If neither lookup finds a match, create a new City.

This ensures:
- Exact matches via ExternalId (Open-Meteo's unique identifier)
- Fuzzy matches via coordinates for cities without ExternalId
- No duplicate cities for the same real-world location

### Soft-Delete Strategy

| Entity | Delete Type | Behavior |
|--------|------------|----------|
| UserCity | Soft-delete | IsDeleted = true. Reactivates on re-subscription. DisplayOrder gaps closed. IsPrimary cleared. |
| City | Hard-delete (admin only) | Cascading delete of CurrentWeather, WeatherForecast, and all UserCity records. No soft-delete for cities. |
| UserProfile | Soft-delete | IsDeleted = true. Cascading soft-delete of all UserCity records. User cannot authenticate. |

### UserCity Lifecycle

```
[Subscribe] → UserCity created (IsDeleted=false, IsPrimary=false, DisplayOrder=max+1)
[Set Primary] → IsPrimary=true for this, IsPrimary=false for all others
[Unsubscribe] → IsDeleted=true, IsPrimary=false, DisplayOrder gap closed
[Re-subscribe] → IsDeleted=false, DisplayOrder=max+1 (appended to end)
[Delete Account] → All UserCity records soft-deleted
```

### City Lifecycle

```
[User Subscribe] → Geocode → Dedup check → Create City (if new) → Create UserCity
[Admin Create] → No geocoding → Dedup check → Create City (no UserCity)
[Admin Delete] → Hard-delete City + CurrentWeather + WeatherForecast + all UserCity
```

## Rationale

1. **ExternalId is reliable**: Open-Meteo provides a unique identifier for each geocoding result. Using it as the primary dedup key eliminates coordinate-matching ambiguity.
2. **Coordinate fallback handles edge cases**: Some geocoding results may not have ExternalId. Coordinate matching with tolerance handles these.
3. **Soft-delete for UserCity**: Users expect to re-subscribe to the same city. Soft-delete preserves the City and its data while removing the user's subscription.
4. **Hard-delete for City (admin)**: Admins need to remove cities entirely (e.g., duplicate cleanup, test data). Cascading hard-delete ensures no orphan data.
5. **DisplayOrder gap closing**: Prevents ordering drift over time. Simple integer renumbering on soft-delete.

## Consequences

### Positive

- No duplicate cities — ExternalId dedup is exact
- Re-subscription is seamless — UserCity reactivates in-place
- Admin cleanup is thorough — hard-delete removes all related data
- DisplayOrder stays contiguous — no gaps after unsubscribe

### Negative

- Coordinate matching with tolerance could match nearby-but-different cities (e.g., two cities within 1.1 km). Mitigated by ExternalId being the primary key.
- Hard-delete of City removes all users' subscriptions silently. PRD-0300 AC8 explicitly notes no user notification in v1.
- DisplayOrder renumbering requires a database update on every unsubscribe (UPDATE SET DisplayOrder = DisplayOrder - 1 WHERE DisplayOrder > deletedOrder AND UserId = @userId).

### Neutral

- ExternalId is nullable — admin-created cities may not have one
- Coordinate tolerance of ±0.01 degrees is approximately 1.1 km at the equator, less at higher latitudes

## Alternatives Considered

- **Alternative 1: Dedup by city name + country only**
  - Rejected because city names are not unique (e.g., "Springfield" exists in many US states)
  - Would require additional disambiguation logic

- **Alternative 2: No dedup — always create new City**
  - Rejected because it defeats the purpose of shared global cities
  - Would result in duplicate weather ingestion for the same real-world location

- **Alternative 3: Soft-delete for City (instead of hard-delete)**
  - Rejected because: (a) soft-deleted cities would still be ingested every 30 minutes (wasting API calls), (b) admin expects immediate and complete removal, (c) no use case for reactivating a deleted city (users can re-subscribe, which creates a new City if needed)

## Related Decisions

- [ADR-0001](adr-0001-weatherfiesta-module-boundaries.md): Single module means City and UserCity share a DbContext
- [ADR-0004](adr-0004-weatherfiesta-ingestion-pipeline.md): CityCreatePipeline handles dedup and creation
- PRD-0000: City subscription and dedup rules
- PRD-0300: Admin hard-delete

## References

- [PRD-0000](./prd-0000-cities-city-subscriptions.md) — Subscribe, unsubscribe, reactivate
- [PRD-0001](./prd-0001-CITIES-city-reorder.md) — DisplayOrder management
- [PRD-0300](./prd-0300-admin-city-management.md) — Admin hard-delete
- [PRD-0400](./prd-0400-USER-user-profile.md) — Account deletion cascade

## Notes

### City Entity

```csharp
public class City : AggregateRoot, IConcurrency
{
    public CityId Id { get; }
    public string Name { get; }
    public string Country { get; }
    public string CountryCode { get; }    // ISO 3166-1 alpha-2
    public string TimeZone { get; }
    public Location Location { get; }      // Value Object: Latitude, Longitude
    public decimal? Elevation { get; }
    public long? ExternalId { get; }       // Open-Meteo geocoding ID
}
```

### UserCity Entity

```csharp
public class UserCity : Entity, IConcurrency
{
    public UserCityId Id { get; }
    public UserId UserId { get; }
    public CityId CityId { get; }
    public bool IsPrimary { get; }
    public int DisplayOrder { get; }
    public bool IsDeleted { get; }
    public string? DeleteReason { get; }
}
```

### Dedup Query

```csharp
// Primary: ExternalId
var existing = await repository.FirstOrDefaultAsync(
    new CityByExternalIdSpecification(externalId), cancellationToken);

// Fallback: Coordinates with tolerance
if (existing is null)
{
    existing = await repository.FirstOrDefaultAsync(
        new CityByCoordinatesSpecification(latitude, longitude, tolerance: 0.01), cancellationToken);
}
```

### DisplayOrder Gap Closing

```csharp
// On soft-delete, close gaps for remaining subscriptions
await repository.UpdateRangeAsync(
    new UserCityDisplayOrderGapSpecification(userId, deletedDisplayOrder),
    uc => new UserCity { DisplayOrder = uc.DisplayOrder - 1 },
    cancellationToken);
```
