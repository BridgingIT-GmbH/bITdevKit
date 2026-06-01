# ADR-0004: WeatherFiesta Ingestion Pipeline

## Status

Accepted

## Context

WeatherFiesta needs to ingest weather data from Open-Meteo on a 30-minute schedule and on-demand when a new city is added. The ingestion process involves multiple sequential steps:

1. **City creation** (POST /cities): Geocode city name → Check if city exists → Persist City → Persist UserCity → Enqueue ingestion
2. **Weather ingestion**: Fetch from Open-Meteo → Upsert CurrentWeather → Upsert WeatherForecasts → Update RetrievedAt

The bITdevKit provides a Pipeline pattern (composable steps) and an Orchestration pattern (saga-like coordinator). The PRDs specify that City creation uses a Pipeline (PRD spec: CityCreatePipeline with steps: GeocodeCity → CheckCityExists → PersistCity → PersistUserCity → EnqueueIngestion).

## Decision

Use the **Pipeline pattern** for both city creation and weather ingestion. Do not use Orchestration for v1.

### City Creation Pipeline

```
CityCreateCommand
  → GeocodeCityStep          (call Open-Meteo Geocoding API)
  → CheckCityExistsStep      (dedup by ExternalId or Lat/Lng)
  → PersistCityStep          (save City aggregate if new)
  → PersistUserCityStep      (save UserCity subscription)
  → EnqueueIngestionStep     (queue weather data fetch)
```

Each step receives the pipeline context, performs its action, and passes control to the next step. Steps can short-circuit the pipeline (e.g., CheckCityExistsStep can return early if city already exists).

### Weather Ingestion Pipeline

```
IngestWeatherCommand
  → FetchCurrentWeatherStep  (call Open-Meteo Forecast API)
  → UpsertCurrentWeatherStep (upsert CurrentWeather record)
  → FetchDailyForecastStep   (call Open-Meteo Forecast API)
  → UpsertForecastsStep      (upsert WeatherForecast records with HourlyForecasts JSON)
```

### Scheduling

- **Scheduled**: Quartz job runs every 30 minutes, queries all cities, and enqueues an IngestWeatherCommand for each.
- **On-demand**: POST /cities/{cityId}/ingest enqueues an IngestWeatherCommand immediately.
- **New city**: CityCreatePipeline's EnqueueIngestionStep enqueues an IngestWeatherCommand for the new city.

### Error Handling

- Transient errors: Retry up to 3 times with 1-second backoff (handled by RetryPipelineBehavior).
- Permanent errors: Log and skip the city.
- Open-Meteo unavailable: Existing cached data remains available with staleDataWarning flag (per ADR-0002).

## Rationale

1. **Composable**: Each step is a single-responsibility class. Easy to add, remove, or reorder steps.
2. **Testable**: Each step can be unit-tested in isolation with mocked dependencies.
3. **Consistent with bITdevKit**: Uses the same Pipeline pattern as other bITdevKit modules.
4. **Simple for v1**: No saga state machine, no compensation logic, no distributed transactions.
5. **Retry built-in**: bITdevKit's RetryPipelineBehavior handles transient failures automatically.

## Consequences

### Positive

- Each pipeline step is independently testable and replaceable
- Pipeline steps can be reused (e.g., geocoding step can be used by admin city creation)
- bITdevKit's pipeline behaviors (validation, retry, timeout) apply automatically
- Easy to add new steps (e.g., air quality ingestion step) without modifying existing steps
- No saga state machine complexity for v1

### Negative

- Pipeline is synchronous within a single request — long-running pipelines block the thread
- No built-in compensation — if UpsertForecastsStep fails after UpsertCurrentWeatherStep succeeds, CurrentWeather is updated but forecasts are stale until next ingestion
- Pipeline steps must be idempotent (upsert semantics handle this)

### Neutral

- Orchestration pattern can be added later for complex multi-step workflows (e.g., city creation + ingestion + notification)
- Pipeline context carries data between steps (no shared mutable state)

## Alternatives Considered

- **Alternative 1: Orchestration (Saga) pattern**
  - Considered for city creation because it has compensation requirements (if UserCity creation fails, should City be rolled back?)
  - Rejected for v1 because: (a) city creation is idempotent (dedup by ExternalId), (b) UserCity creation failure is rare and can be retried, (c) adds significant complexity for minimal benefit
  - Can be introduced in v2 if compensation requirements become critical

- **Alternative 2: Simple command handlers without pipeline**
  - Rejected because city creation has 5 sequential steps that benefit from composable pipeline
  - Would require a single large handler with all logic in one method
  - Less testable and less reusable

- **Alternative 3: Event-driven (domain events trigger subsequent steps)**
  - Considered for decoupling city creation from ingestion
  - Partially adopted: CityCreatedDomainEvent triggers ingestion enqueue
  - Not used for the full pipeline because domain events are async and add latency for the initial request

## Related Decisions

- [ADR-0005](../../adr/0005-requester-notifier-mediator-pattern.md): Requester pattern for command/query handling
- [ADR-0001](adr-0001-weatherfiesta-module-boundaries.md): Single module means shared pipeline infrastructure
- PRD-0000 Story 2: City creation with geocoding pipeline
- PRD-0200: Scheduled and on-demand ingestion

## References

- [PRD-0000](./prd-0000-cities-city-subscriptions.md) — City creation flow
- [PRD-0200](./prd-0200-ingestion-data-ingestion.md) — Ingestion flow
- [bITdevKit Pipeline Documentation](https://github.com/BridgingIT-GmbH/bITdevKit/blob/main/docs/features-pipeline.md)

## Notes

### Pipeline Step Interface

```csharp
public interface IPipelineStep<TContext>
{
    Task<Result<TContext>> ExecuteAsync(
        TContext context,
        CancellationToken cancellationToken);
}
```

### City Creation Pipeline Registration

```csharp
services.AddPipeline<CityCreateCommand, CityCreateResult>()
    .WithStep<GeocodeCityStep>()
    .WithStep<CheckCityExistsStep>()
    .WithStep<PersistCityStep>()
    .WithStep<PersistUserCityStep>()
    .WithStep<EnqueueIngestionStep>();
```

### Weather Ingestion Pipeline Registration

```csharp
services.AddPipeline<IngestWeatherCommand, IngestWeatherResult>()
    .WithStep<FetchCurrentWeatherStep>()
    .WithStep<UpsertCurrentWeatherStep>()
    .WithStep<FetchDailyForecastStep>()
    .WithStep<UpsertForecastsStep>();
```

### Quartz Job Configuration

```csharp
// Scheduled every 30 minutes
builder.Services.AddQuartzJob<WeatherIngestionJob>(cron: "0 */30 * * * ?");
```

### Idempotency

- CurrentWeather upsert: `CityId` as unique key → INSERT or UPDATE
- WeatherForecast upsert: `CityId + ForecastDate` as composite key → INSERT or UPDATE
- UserCity creation: `(UserId, CityId)` as unique key → INSERT or reactivate (IsDeleted = false)
- City creation: `ExternalId` or `(Latitude, Longitude)` as dedup key → return existing if found
