# ADR-0003: WeatherFiesta Unit Preferences Strategy

## Status

Accepted

## Context

WeatherFiesta serves weather data from Open-Meteo in metric units (Celsius, km/h). Users may prefer Fahrenheit, mph, m/s, or knots for display. Multiple PRDs reference unit preferences:

- PRD-0400: User can set temperatureUnit (celsius/fahrenheit) and windSpeedUnit (kmh/mph/ms/knots)
- PRD-0100: Weather responses include unit preferences as metadata
- PRD-0102: Comparison highlights include unit preferences as metadata
- PRD-0500: Dashboard primary city includes unit preferences as metadata
- PRD-0600: Recommendations include unit preferences as metadata
- PRD-0101: Sun endpoint does NOT include unit preferences (no unit-dependent fields)
- PRD-0103: CSV exports always use metric units

The question is: where and how should unit conversion happen?

## Decision

**API always returns metric data. Frontend handles conversion.** Unit preferences are stored on the user profile and returned as metadata in weather-related responses.

### Storage

- `UserProfile.TemperatureUnit`: enum (Celsius, Fahrenheit), default Celsius
- `UserProfile.WindSpeedUnit`: enum (Kmh, Mph, Ms, Knots), default Kmh

### Response Pattern

Weather-related responses include a `unitPreferences` object:

```json
{
  "temperature": 22.5,
  "apparentTemperature": 20.1,
  "windSpeed": 15.3,
  "unitPreferences": {
    "temperatureUnit": "fahrenheit",
    "windSpeedUnit": "mph"
  }
}
```

The `unitPreferences` object tells the frontend which units the user prefers. The frontend converts metric values to the user's preferred units for display.

### Endpoints that include unit preferences

| Endpoint | Includes unit preferences? | Reason |
|----------|---------------------------|--------|
| GET /cities/{cityId}/weather | Yes | Temperature and wind values need conversion |
| GET /cities/{cityId}/sun | No | No unit-dependent fields (times in ISO 8601, durations in seconds) |
| GET /cities/compare | Yes | Temperature and wind values need conversion |
| GET /cities/{cityId}/recommendations | Yes | sourceCondition values need conversion |
| GET /dashboard | Yes | Primary city and highlights need conversion |
| GET /cities/export | No (metric only) | CSV uses metric units per design decision |
| GET /cities/{cityId}/weather/export | No (metric only) | CSV uses metric units per design decision |
| GET /cities | Yes | currentWeather summary needs conversion |
| GET /cities/alerts | No | Alert severity and type are not unit-dependent |

## Rationale

1. **API simplicity**: One data format (metric) for all responses. No per-request conversion logic.
2. **Caching friendly**: Same response for all users regardless of preferences. Can cache at the API level.
3. **Frontend flexibility**: Frontend can convert on the fly, show both units, or let users toggle without API calls.
4. **Consistency**: All numeric values in the API are in the same units. No risk of mixing Celsius and Fahrenheit in the same response.
5. **CSV export clarity**: CSV files always use metric. Users who want other units convert in their spreadsheet.

## Consequences

### Positive

- API returns one format — no conversion bugs from mixed units
- Cacheable — same weather data serves all users
- Frontend has full control over display formatting
- Adding new unit types (e.g., pressure units) requires only frontend changes
- CSV exports are consistent and machine-readable

### Negative

- Frontend must implement conversion logic (Celsius↔Fahrenheit, km/h↔mph↔m/s↔knots)
- API response includes redundant metadata for users with default preferences
- Frontend must know conversion formulas (trivial but must be implemented)

### Neutral

- Conversion formulas are simple arithmetic (no lookup tables)
- Open-Meteo provides data in metric, so no server-side conversion needed

## Alternatives Considered

- **Alternative 1: Server-side conversion based on user preferences**
  - Rejected because it prevents caching (same endpoint returns different data for different users)
  - Adds conversion logic to every handler
  - Risk of mixing units in the same response if a field is missed
  - CSV export would need a separate "user units" format

- **Alternative 2: Dual-unit responses (both metric and preferred)**
  - Rejected because it doubles response size for temperature and wind fields
  - Adds complexity to every response DTO
  - Frontend still needs to choose which value to display

- **Alternative 3: Query parameter for units (e.g., ?units=imperial)**
  - Rejected because it couples display concerns to API design
  - Same caching problem as Alternative 1
  - Inconsistent with the "metadata" approach used for staleness

## Related Decisions

- [ADR-0002](adr-0002-weatherfiesta-staleness-strategy.md): Similar "compute at query time, include as metadata" pattern
- PRD-0400: User profile stores unit preferences

## References

- [PRD-0400](./prd-0400-USER-user-profile.md) — Unit preferences storage and API
- [PRD-0100](./prd-0100-weather-weather-data-viewing.md) — Unit metadata in weather responses

## Notes

### Conversion Formulas (for frontend reference)

```
Fahrenheit = Celsius * 9/5 + 32
Celsius = (Fahrenheit - 32) * 5/9

mph = km/h * 0.621371
m/s = km/h * 0.277778
knots = km/h * 0.539957

km/h = mph * 1.60934
km/h = m/s * 3.6
km/h = knots * 1.852
```

### UserProfile Entity

```csharp
public class UserProfile : Entity, IConcurrency
{
    public UserId Id { get; }
    public string Email { get; }
    public string Name { get; }
    public TemperatureUnit TemperatureUnit { get; } // Enumeration: Celsius, Fahrenheit
    public WindSpeedUnit WindSpeedUnit { get; }       // Enumeration: Kmh, Mph, Ms, Knots
    public bool IsDeleted { get; }
}
```
