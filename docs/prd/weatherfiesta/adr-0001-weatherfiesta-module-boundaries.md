# ADR-0001: WeatherFiesta Module Boundaries

## Status

Accepted

## Context

WeatherFiesta is a weather dashboard application with 5 feature slices defined in PRDs: CITIES, WEATHER, INGESTION, ADMIN, USER (plus DASHBOARD and RECOMMENDATIONS as read-only projections). The bITdevKit modular monolith architecture (ADR-0003) requires each module to be a self-contained vertical slice with its own Domain, Application, Infrastructure, and Presentation layers.

Key constraints:
- Modules must not reference other modules' internal layers
- Each module has its own DbContext and database schema
- Cross-module communication via contracts or integration events
- The app has 41 stories across 11 PRDs with significant cross-slice dependencies

Cross-slice dependencies identified:
- CITIES depends on nothing (root entity)
- WEATHER depends on CITIES (subscription checks, city data)
- INGESTION depends on CITIES (city coordinates for API calls)
- ADMIN depends on CITIES and WEATHER (city CRUD, weather reset)
- USER depends on nothing (standalone profile)
- DASHBOARD depends on CITIES, WEATHER, USER (aggregation read model)
- RECOMMENDATIONS depends on WEATHER (computed from weather data)

## Decision

Organize WeatherFiesta into **2 modules** with a shared domain concept:

### Module 1: Core (primary module)
Contains all weather-related vertical slices as a single cohesive module:

- **Domain**: City, UserCity, CurrentWeather, WeatherForecast, UserProfile, WeatherCode enumeration, WeatherAlert rules, Recommendation rules
- **Application**: All commands, queries, handlers, specifications, pipeline steps
- **Infrastructure**: DbContext, repositories, Open-Meteo client, ingestion jobs
- **Presentation**: All API endpoints

### Module 2: Core (existing bITdevKit module)
Unchanged. WeatherFiesta does not modify Core.

### Rationale for single module

The 5 feature slices (CITIES, WEATHER, INGESTION, ADMIN, USER) share:
1. **City aggregate** — the root entity that all other features reference
2. **UserCity** — the join table between users and cities
3. **CurrentWeather / WeatherForecast** — read by DASHBOARD, RECOMMENDATIONS, WEATHER, EXPORT
4. **Single DbContext** — all entities are tightly related; separate schemas would create cross-module foreign key issues

Splitting into multiple modules would require:
- City aggregate duplication or shared contracts
- Cross-module queries for dashboard aggregation (defeats module isolation)
- Integration events for weather data updates (adds latency and complexity)
- Separate DbContexts for entities that naturally share a schema

The dependency graph is a star pattern centered on City — not a set of independent domains. A single module is the correct granularity.

## Consequences

### Positive

- Single DbContext eliminates cross-module data consistency issues
- Dashboard and recommendations can query City, UserCity, CurrentWeather directly without integration events
- Simpler development — one module to register, one migration path
- No shared contract projects needed between slices
- Ingestion pipeline can directly access City aggregate for coordinates
- Admin endpoints can directly access all entities

### Negative

- Module boundary is the API layer, not the project layer — discipline required to keep slices independent within the module
- All slices deploy together (no independent deployment)
- Module will grow large as features are added

### Neutral

- Vertical slices within the module are organized by feature folder (CITIES, WEATHER, INGESTION, ADMIN, USER, DASHBOARD, RECOMMENDATIONS)
- Each slice has its own commands, queries, handlers, and endpoints
- Slices share the Domain layer (City, UserCity, WeatherCode) but have separate Application concerns

## Alternatives Considered

- **Alternative 1: One module per feature slice (7 modules)**
  - Rejected because City is a shared root entity — duplicating it across modules violates DDD
  - Dashboard and recommendations require cross-module queries, defeating module isolation
  - Integration events for weather data updates add latency and complexity
  - Separate DbContexts for tightly related entities create foreign key issues

- **Alternative 2: Two modules — CITIES+USER and WEATHER+INGESTION+ADMIN**
  - Rejected because DASHBOARD and RECOMMENDATIONS need data from both modules
  - Still requires integration events for weather data access
  - City aggregate would need shared contracts, adding coupling

- **Alternative 3: Three modules — CITIES, WEATHER+INGESTION+ADMIN, USER**
  - Rejected for same reasons as Alternative 2 — dashboard aggregation requires cross-module queries

## Related Decisions

- [ADR-0001](../../adr/0001-clean-onion-architecture.md): Layering within the Core module
- [ADR-0003](../../adr/0003-modular-monolith-architecture.md): Modular monolith pattern
- [ADR-0005](../../adr/0005-requester-notifier-mediator-pattern.md): Command/query handling within the module

## References

- [PRD Index](./INDEX.md) — 41 stories across 11 PRDs
- [ADR-0003](../../adr/0003-modular-monolith-architecture.md) — Module isolation rules

## Notes

### Module Structure

```
src/Modules/Core/
├── Core.Domain/
│   ├── Aggregates/          # City, UserCity
│   ├── Entities/            # CurrentWeather, WeatherForecast
│   ├── ValueObjects/        # Location, HourlyForecast
│   ├── Enumerations/        # WeatherCode, AlertType, AlertSeverity, RecommendationCategory
│   └── Events/              # CityCreatedDomainEvent
├── Core.Application/
│   ├── CITIES/              # Commands, Queries, Handlers, Specifications
│   ├── WEATHER/             # Commands, Queries, Handlers, Specifications
│   ├── INGESTION/           # Pipeline steps, handlers
│   ├── ADMIN/               # Commands, Queries, Handlers
│   ├── USER/                # Commands, Queries, Handlers
│   ├── DASHBOARD/           # Queries, Handlers (read-only)
│   └── RECOMMENDATIONS/     # Queries, Handlers (read-only)
├── Core.Infrastructure/
│   ├── Persistence/         # DbContext, Configurations, Migrations
│   ├── Repositories/        # Repository implementations
│   ├── Services/            # Open-Meteo client, geocoding service
│   └── Jobs/                # Quartz ingestion job
└── Core.Presentation/
    ├── CITIES/              # Endpoints
    ├── WEATHER/             # Endpoints
    ├── INGESTION/           # Endpoints
    ├── ADMIN/               # Endpoints
    ├── USER/                # Endpoints
    ├── DASHBOARD/           # Endpoints
    └── RECOMMENDATIONS/     # Endpoints
```

### Module Registration

```csharp
public class CoreModule : WebModuleBase("Core")
```

### Host Registration

```csharp
.WithModule<CoreModule>()
```

### API Route Prefix

All WeatherFiesta endpoints use the route prefix `api/core/` to follow bITdevKit convention where the route matches the module name. Admin endpoints use `api/core/admin/`.
