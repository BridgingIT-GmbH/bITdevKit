# ADR-0002: WeatherFiesta Data Staleness Strategy

## Status

Accepted

## Context

WeatherFiesta ingests weather data from Open-Meteo every 30 minutes. When Open-Meteo is unavailable, the system serves cached data. Multiple PRDs reference a `staleDataWarning` concept:

- PRD-0100: Weather responses include `staleDataWarning` if data is >60 minutes old
- PRD-0101: Sun times include `staleDataWarning`
- PRD-0102: Comparison responses include per-city `staleDataWarning`
- PRD-0103: CSV exports include `staleDataWarning` column
- PRD-0500: Dashboard includes `staleDataWarning` on primary city and alerts
- PRD-0600: Recommendations include `staleDataWarning`

The question is: how should staleness be computed and surfaced?

## Decision

Compute `staleDataWarning` at **query time** from the `RetrievedAt` timestamp on weather records. Do not store a staleness flag in the database.

### Staleness Rule

A weather record is stale when `DateTime.UtcNow - RetrievedAt > 60 minutes`.

### Implementation

1. **CurrentWeather.RetrievedAt** and **WeatherForecast.RetrievedAt** are already stored as UTC timestamps (set by the ingestion pipeline on each upsert).
2. At query time, each handler compares `RetrievedAt` against `DateTime.UtcNow - TimeSpan.FromMinutes(60)`.
3. If stale, the response includes `staleDataWarning: true` and `staleDataWarningMessage: "Data may be outdated — last updated X minutes ago"`.
4. If fresh, `staleDataWarning` is omitted (not `false`) to keep responses clean.

### Where staleness is computed

- **Single-city endpoints** (GET /cities/{cityId}/weather, GET /cities/{cityId}/sun, GET /cities/{cityId}/recommendations): Compute per-record staleness in the query handler.
- **Multi-city endpoints** (GET /cities, GET /cities/compare, GET /dashboard, GET /cities/alerts, GET /cities/export): Compute per-city staleness by checking each city's CurrentWeather.RetrievedAt.
- **CSV export**: Include a `staleDataWarning` column with `true`/`false` values.

## Rationale

1. **No extra database column**: Staleness is derived from existing `RetrievedAt` timestamps. No migration needed.
2. **Always accurate**: Computed at query time, so staleness reflects the actual age at the moment of the request.
3. **Consistent across endpoints**: Same 60-minute threshold used everywhere.
4. **Simple to test**: Mock `DateTime.UtcNow` in unit tests to verify staleness behavior.
5. **No background job needed**: No need for a scheduled job to update a staleness flag.

## Consequences

### Positive

- Zero storage overhead — no new columns or tables
- Always accurate — reflects real-time data age
- Consistent — same rule applied everywhere
- Simple — one comparison per weather record
- Testable — mock time to verify threshold

### Negative

- Computed on every read — negligible cost (one DateTime comparison per record)
- Threshold is not configurable per-user — all users see the same 60-minute threshold
- No historical staleness tracking — if needed later, would require a separate log

### Neutral

- The 60-minute threshold matches the 30-minute ingestion schedule (2 missed cycles = stale)
- Threshold can be changed in one place (a constant or configuration value)

## Alternatives Considered

- **Alternative 1: Stored IsStale flag on CurrentWeather and WeatherForecast**
  - Rejected because it requires a background job or ingestion-time computation
  - Adds a column that is always derivable from RetrievedAt
  - Risk of flag being out of sync with actual data age

- **Alternative 2: Middleware that adds staleness headers to all responses**
  - Rejected because staleness is per-record, not per-request
  - A dashboard response has multiple cities with different staleness states
  - Middleware cannot compute per-record staleness without loading data

- **Alternative 3: Per-user configurable staleness threshold**
  - Rejected for v1 — adds complexity with minimal user value
  - Can be added later as a user preference without changing the computation strategy

## Related Decisions

- [ADR-0001](adr-0001-weatherfiesta-module-boundaries.md): Single module means shared staleness logic
- PRD-0100: Weather data viewing (staleness ACs)
- PRD-0200: Ingestion sets RetrievedAt on each upsert

## References

- [PRD-0100](./prd-0100-weather-weather-data-viewing.md) — Staleness ACs in Stories 1, 2, 5
- [PRD-0200](./prd-0200-ingestion-data-ingestion.md) — Ingestion AC7-8 (stale data on failure)

## Notes

### Staleness Constant

```csharp
public static class WeatherConstants
{
    public static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(60);
}
```

### Query-Time Computation Example

```csharp
var isStale = currentWeather.RetrievedAt < DateTime.UtcNow - WeatherConstants.StaleThreshold;
var minutesSinceUpdate = (int)(DateTime.UtcNow - currentWeather.RetrievedAt).TotalMinutes;

if (isStale)
{
    response.StaleDataWarning = true;
    response.StaleDataWarningMessage = $"Data may be outdated — last updated {minutesSinceUpdate} minutes ago";
}
```

### CSV Export Staleness

For CSV exports, `staleDataWarning` is a boolean column (`true`/`false`) rather than omitted, because CSV has no concept of optional fields.
