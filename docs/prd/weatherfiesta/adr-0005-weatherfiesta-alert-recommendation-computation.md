# ADR-0005: WeatherFiesta Alert and Recommendation Computation

## Status

Accepted

## Context

WeatherFiesta generates two types of derived data from weather records:

1. **Weather Alerts** (PRD-0100 Story 5): System-defined rules that detect dangerous conditions (thunderstorm, hail, severe wind, extreme heat, blizzard, hurricane). Served via GET /cities/alerts and on the dashboard.

2. **Recommendations** (PRD-0600): Actionable advice derived from weather data (bring umbrella, wear sunscreen, dress warmly, etc.). Served via GET /cities/{cityId}/recommendations and on the dashboard.

Both are computed from existing weather data (CurrentWeather and WeatherForecast). Neither is stored in the database. The question is: how should they be computed and served?

## Decision

Compute alerts and recommendations **at query time** from existing weather data. Do not store them in the database.

### Alert Rules (PRD-0100)

| Alert Type | Condition | Severity |
|------------|-----------|----------|
| Thunderstorm | WMO codes 95, 96, 99 | Warning |
| Hail | WMO codes 96, 99 | Warning |
| Severe wind | Wind speed > 80 km/h | Caution |
| Extreme heat | Apparent temperature > 40°C | Warning |
| Blizzard | WMO codes 71-77 AND wind speed > 50 km/h | Warning |
| Hurricane | Wind speed > 118 km/h | Extreme |

### Recommendation Rules (PRD-0600)

| Category | Condition | Message | Severity |
|----------|-----------|---------|----------|
| Precipitation | Precipitation probability > 60% | Bring an umbrella | Caution |
| UV | UV index max > 6 | Wear sunscreen | Caution |
| Temperature (cold) | Apparent temperature < 5°C | Dress warmly | Caution |
| Temperature (hot) | Apparent temperature > 35°C | Stay hydrated and seek shade | Warning |
| Wind | Wind speed max > 40 km/h | Wind advisory — secure loose items | Caution |
| Storm | WMO codes 95, 96, 99 | Thunderstorm expected — seek shelter | Warning |
| General | No adverse conditions | Great day for outdoor activities | Info |

### Computation Strategy

1. **Query-time computation**: When a request hits GET /cities/alerts or GET /cities/{cityId}/recommendations, the handler loads CurrentWeather and WeatherForecast for the relevant cities, applies the rules, and returns the results.
2. **No database storage**: Alerts and recommendations are not persisted. They are derived values, like a calculated column.
3. **Deterministic**: Same weather data always produces the same alerts and recommendations.
4. **Shared rule engine**: Alert rules and recommendation rules are implemented as a shared `WeatherRuleEngine` in the Domain layer. Both the alerts handler and recommendations handler use the same engine.

### Dashboard Aggregation

The dashboard endpoint (GET /dashboard) computes alerts and recommendations for all subscribed cities in a single query, using the same rule engine.

## Rationale

1. **No stale data**: Alerts and recommendations always reflect the latest weather data.
2. **No extra storage**: No Alert or Recommendation tables needed.
3. **Simple**: One rule engine, two endpoints, no background jobs.
4. **Consistent**: Dashboard and individual endpoints use the same rules.
5. **Extensible**: New rules can be added to the rule engine without schema changes.

## Consequences

### Positive

- Zero storage overhead — no Alert or Recommendation tables
- Always up-to-date — reflects latest weather data
- Easy to add new rules — just add to the rule engine
- Same rules used everywhere — no divergence between dashboard and individual endpoints
- No background processing needed — no scheduled job to compute alerts

### Negative

- Computed on every request — for a dashboard with 10 cities, this means loading CurrentWeather for 10 cities and running ~6 alert rules + ~7 recommendation rules per city
- Not suitable for push notifications — if push notifications are added in v2, alerts will need to be computed on ingestion and stored
- No historical alert data — cannot query "what alerts were active yesterday"

### Neutral

- Computation cost is negligible (simple threshold checks on in-memory data)
- Dashboard endpoint may benefit from short-lived caching (60 seconds) in high-traffic scenarios
- The rule engine can be extracted to a standalone service if computation becomes expensive

## Alternatives Considered

- **Alternative 1: Materialized alert/recommendation tables**
  - Computed on ingestion and stored in Alert and Recommendation tables
  - Rejected for v1 because: (a) adds storage and migration complexity, (b) alerts become stale between ingestion cycles, (c) requires background processing on every ingestion, (d) dashboard would query these tables instead of computing
  - May be reconsidered for v2 when push notifications are added

- **Alternative 2: Cached computation with expiration**
  - Compute on first request, cache for 5 minutes, serve from cache
  - Rejected for v1 because: (a) adds caching infrastructure, (b) cache invalidation is complex (when does it expire?), (c) computation is cheap enough to do on every request
  - May be reconsidered if performance profiling shows query-time computation is too slow

- **Alternative 3: Pre-computed on ingestion, stored as JSON**
  - Store alert/recommendation results as a JSON column on CurrentWeather
  - Rejected because: (a) couples alert rules to the ingestion pipeline, (b) makes rule changes require re-ingestion, (c) adds a mutable JSON column that must be kept in sync

## Related Decisions

- [ADR-0002](adr-0002-weatherfiesta-staleness-strategy.md): Staleness is also computed at query time
- [ADR-0003](adr-0003-weatherfiesta-unit-preferences-strategy.md): Unit preferences are metadata, not conversion
- PRD-0100 Story 5: Alert rules and thresholds
- PRD-0600: Recommendation rules and categories

## References

- [PRD-0100](./prd-0100-weather-weather-data-viewing.md) — Alert rules
- [PRD-0600](./prd-0600-RECOMMENDATIONS-daily-summary.md) — Recommendation rules
- [PRD-0500](./prd-0500-DASHBOARD-dashboard-summary.md) — Dashboard aggregation

## Notes

### Rule Engine Interface

```csharp
public interface IWeatherRuleEngine
{
    IReadOnlyList<WeatherAlert> EvaluateAlerts(CurrentWeather current, WeatherForecast todayForecast);
    IReadOnlyList<Recommendation> EvaluateRecommendations(CurrentWeather current, WeatherForecast todayForecast);
}
```

### Alert and Recommendation Types

```csharp
public class WeatherAlert
{
    public Guid CityId { get; }
    public string CityName { get; }
    public AlertType AlertType { get; }      // Enumeration: Thunderstorm, Hail, SevereWind, ExtremeHeat, Blizzard, Hurricane
    public AlertSeverity Severity { get; }    // Enumeration: Warning, Severe, Extreme
    public int WeatherCode { get; }
    public string WeatherDescription { get; }
    public DateTimeOffset ValidFrom { get; }
    public DateTimeOffset ValidTo { get; }
    public bool StaleDataWarning { get; }
}

public class Recommendation
{
    public RecommendationCategory Category { get; }  // Enumeration: Precipitation, UV, Temperature, Wind, Storm, General
    public RecommendationSeverity Severity { get; }  // Enumeration: Info, Caution, Warning
    public string Message { get; }
    public string SourceCondition { get; }           // e.g., "precipitationProbabilityMax=75%"
}
```

### Sorting Rules

Recommendations are sorted by:
1. Severity descending (Warning → Caution → Info)
2. Category order (Storm → Temperature → Precipitation → Wind → UV → General)

Alerts are sorted by severity descending (Extreme → Severe → Warning).
