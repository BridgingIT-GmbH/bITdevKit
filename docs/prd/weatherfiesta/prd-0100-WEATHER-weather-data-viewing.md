---
id: PRD-0100
title: Weather Data Viewing
slice: WEATHER
status: Implemented
---

# Product Requirements: Weather Data Viewing

## Overview
Authenticated users can view current weather, 7-day forecasts, and hourly breakdowns for cities they are subscribed to. Weather data is ingested from Open-Meteo and stored in the database. Users can also manually trigger ingestion for a subscribed city. Weather responses include a data freshness indicator. The system also generates weather alerts for extreme conditions based on WMO weather codes.

## Scope
- In scope: View current weather, view daily forecast, view hourly forecast, manual ingestion trigger, data freshness indicators (stale data warnings), system-defined weather alerts (WMO-based)
- Out of scope: City subscription management (PRD-0000), admin operations (PRD-0300), ingestion scheduling/queueing internals, push-based alert delivery (alerts are pull-only via GET /cities/alerts in v1)

## User Roles
- **User**: Authenticated user who views weather data for subscribed cities
- **Admin**: Elevated user who can trigger ingestion for any city

## Stories

### Story 1: View current weather
- Status: Implemented
- Ready: Yes
- Ready Reason: Data model and API contract fully defined. Subscription check specified.
- User Story: As an authenticated user, I want to see current weather conditions for a city I'm subscribed to, so that I know what it's like right now.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to the city, when they call GET /cities/{cityId}/weather, then the system returns current weather data including temperature, apparent temperature, humidity, weather code, wind speed/direction/gusts, precipitation, cloud cover, and pressure.
2. Given the user is not subscribed to the city, when they call GET /cities/{cityId}/weather, then the system returns 404.
3. Given the user is subscribed but no weather data exists yet, when they call GET /cities/{cityId}/weather, then the current field is null and the daily field is an empty array.
4. When the weather data includes a weather code, then the response includes both the numeric code and the human-readable description (e.g., weatherCode: 3, weatherDescription: "Overcast").
5. When the weather data was retrieved more than 60 minutes ago, then the response includes a staleDataWarning field set to true with a message like "Data may be outdated — last updated X minutes ago".
6. The response includes a retrievedAt timestamp indicating when the weather data was last fetched from Open-Meteo.
7. The response includes the user's unit preferences as metadata (temperatureUnit, windSpeedUnit) so the frontend can convert display values.

Data Requirements:
- cityId: GUID, required (from URL path, must be subscribed)
- days: int, optional, default 7, range 1-16

Notes:
- Technical context: GET /cities/{cityId}/weather?days={n}. Returns combined current + daily response.

### Story 2: View 7-day forecast
- Status: Implemented
- Ready: Yes
- Ready Reason: Data model and API contract defined. Hourly data as JSON on daily forecast.
- User Story: As an authenticated user, I want to see a 7-day weather forecast for a city I'm subscribed to, so that I can plan ahead.

Acceptance Criteria:
1. Given the user is subscribed to the city, when they call GET /cities/{cityId}/weather?days=7, then the system returns up to 7 daily forecast records.
2. Each daily forecast includes: forecastDate, dayWeatherCode, weatherDescription, temperatureMax, temperatureMin, apparentTemperatureMax, apparentTemperatureMin, precipitationSum, precipitationProbabilityMax, windSpeedMax, windGustsMax, dominantWindDirection, uvIndexMax, sunshineDurationSeconds, daylightDurationSeconds, sunrise, sunset.
3. When the days parameter is omitted, then the default is 7 days.
4. When the days parameter exceeds 16, then the system returns 400 with message "Maximum forecast days is 16."
5. When the forecast data was retrieved more than 60 minutes ago, then the response includes a staleDataWarning field set to true with a message indicating data age.
6. The response includes a retrievedAt timestamp for each forecast record.
7. The response includes the user's unit preferences as metadata (temperatureUnit, windSpeedUnit) so the frontend can convert display values.

Data Requirements:
- days: int, optional, default 7, range 1-16

Notes:
- Technical context: WeatherForecast records are upserted every 30 minutes. HourlyForecasts stored as JSON column.

### Story 3: View hourly forecast
- Status: Implemented
- Ready: Yes
- Ready Reason: Hourly data is embedded in daily forecast response as JSON array.
- User Story: As an authenticated user, I want to see hourly weather details for a specific day, so that I can plan activities at a granular level.

Acceptance Criteria:
1. Given the user is subscribed to the city, when they view a daily forecast, then each day includes an hourlyForecasts array with up to 24 hourly entries.
2. Each hourly entry includes: hour, temperature, relativeHumidity, apparentTemperature, precipitationProbability, precipitation, weatherCode, windSpeed, windDirection, windGusts, cloudCover, visibility, isDay.
3. When no hourly data is available for a day, then the hourlyForecasts array is empty.

Data Requirements:
- Hourly data is part of the daily forecast response, not a separate endpoint.

Notes:
- Technical context: HourlyForecasts stored as JSON column on WeatherForecast. No separate table.

### Story 4: Manual weather ingestion
- Status: Implemented
- Ready: Yes
- Ready Reason: Simple queue trigger. API contract defined.
- User Story: As an authenticated user, I want to manually trigger weather data refresh for a city I'm subscribed to, so that I get the latest data without waiting for the scheduled job.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to the city, when they call POST /cities/{cityId}/ingest, then the system queues an ingestion job and returns 202.
2. Given the user is not subscribed to the city, when they call POST /cities/{cityId}/ingest, then the system returns 404.
3. When the ingestion is already queued or running for this city, then the system still returns 202 (idempotent trigger).
4. When the city is not found, then the system returns 404.

Data Requirements:
- cityId: GUID, required (from URL path, must be subscribed)

Notes:
- Technical context: POST /cities/{cityId}/ingest. Queues a message for async processing. Actual data update happens asynchronously.

### Story 5: System weather alerts
- Status: Implemented
- Ready: Yes
- Ready Reason: Alert rules are based on WMO weather codes already in the data model. No user-defined thresholds needed.
- User Story: As an authenticated user, I want to see weather alerts for extreme conditions in my subscribed cities, so that I can be aware of dangerous weather.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to cities, when they call GET /cities/alerts, then the system returns active weather alerts for all their subscribed cities.
2. An alert is generated when a city's current weather or daily forecast matches an extreme condition rule (WMO weather codes for thunderstorm/hail/tornado, or computed thresholds for extreme heat, blizzard, and hurricane).
3. Each alert includes: cityId, cityName, alertType, severity, weatherCode, weatherDescription, validFrom, validTo.
4. When no alerts are active for the user's cities, then the system returns an empty array.
5. Given the user is not authenticated, when they call GET /cities/alerts, then the system returns 401.
6. Given the weather data for a city has a staleDataWarning, when alerts are computed, then alerts from stale data include a staleDataWarning flag indicating the data may be unreliable.

Data Requirements:
- Alert types mapped from WMO codes and computed thresholds:
  - Thunderstorm: WMO codes 95, 96, 99
  - Hail: WMO codes 96, 99
  - Severe wind: wind speed > 80 km/h
  - Extreme heat: apparent temperature > 40°C
  - Blizzard: WMO code 71-77 AND wind speed > 50 km/h
  - Hurricane: wind speed > 118 km/h
- Severity levels: warning, severe, extreme

Notes:
- Dependencies: WMO weather codes from Open-Meteo (already in data model)
- Technical context: GET /cities/alerts. Alerts are computed from existing weather data, not stored separately. No user-defined thresholds — system-defined rules only.
- Design decision: System-defined alerts only (Q4 decision). User-defined thresholds are out of scope for v1.

## Non-Functional Notes
- Performance: Weather data response should be < 500ms for subscribed cities
- Security: All endpoints require authentication and subscription verification
- Data freshness: Weather responses include retrievedAt timestamps. Data older than 60 minutes is flagged with a staleDataWarning.
- Alerts: System-defined weather alerts based on WMO codes. No user-defined thresholds in v1.

## Open Questions
- None
