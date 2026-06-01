---
id: PRD-0200
title: Data Ingestion
slice: INGESTION
status: Pending
---

# Product Requirements: Data Ingestion

## Overview
Weather data is ingested from the Open-Meteo API on a 30-minute schedule and on-demand when a new city is added. Ingestion fetches current conditions and daily/hourly forecasts, then upserts them into the database. Failed ingestions are retried with backoff. When Open-Meteo is unavailable, the system serves cached data with a staleness indicator rather than failing the request.

## Scope
- In scope: Scheduled ingestion, on-demand ingestion (new city + manual trigger), ingestion idempotency, error handling, admin weather data reset
- Out of scope: City subscription management (PRD-0000), weather data viewing (PRD-0100), admin city CRUD (PRD-0300)

## Diagram

```text
[New City] --> [Queue Ingestion] --> [Fetch from Open-Meteo] --> [Upsert DB]
[30-min Schedule] --> [Queue All Cities] --> [Fetch from Open-Meteo] --> [Upsert DB]
[Manual Trigger] --> [Queue Single City] --> [Fetch from Open-Meteo] --> [Upsert DB]
```
Covers AC: Story 1 AC1-3, Story 2 AC1-3

## User Roles
- **System**: Scheduled job that ingests weather data for all cities
- **User**: Authenticated user who triggers manual ingestion
- **Admin**: Elevated user who can reset weather data and trigger ingestion for any city

## Stories

### Story 1: Scheduled ingestion
- Status: Pending
- Ready: Yes
- Ready Reason: Ingestion flow, idempotency, and error handling are defined. Open-Meteo API parameters documented.
- User Story: As the system, I want to ingest weather data for all cities every 30 minutes, so that data stays fresh.

Acceptance Criteria:
1. Given the scheduled job runs, when it iterates all cities, then it queues an ingestion message for each city.
2. Given the ingestion worker processes a city, when it fetches data from Open-Meteo, then it upserts CurrentWeather (one per city) and WeatherForecast (one per day per city, up to 16 days).
3. Given the ingestion worker processes a city, when it fetches hourly data, then it replaces the HourlyForecasts JSON column for each daily forecast.
4. Given a city already has weather data, when ingestion runs again, then existing records are overwritten (upsert semantics on CityId + ForecastDate for forecasts, CityId for current weather).
5. Given the Open-Meteo API returns a transient error, when ingestion fails, then it retries up to 3 times with 1-second backoff.
6. Given the Open-Meteo API returns a permanent error (e.g., invalid coordinates), when ingestion fails permanently, then it logs the error and skips the city.
7. Given the Open-Meteo API is unavailable for all retry attempts, when ingestion fails for a city, then the system logs a warning with city ID and error details, and the existing cached data for that city remains available with a staleDataWarning flag.
8. Given the Open-Meteo API is unavailable, when the next scheduled ingestion succeeds, then the staleDataWarning is cleared for that city.

Data Requirements:
- Schedule: Every 30 minutes (cron: 0 */30 * * * ?)
- Open-Meteo API parameters: latitude, longitude, current, daily, hourly, timezone, forecast_days=16, temperature_unit=celsius, wind_speed_unit=kmh, precipitation_unit=mm

Notes:
- Dependencies: Open-Meteo Forecast API (external, free, no API key)
- Risks: Rate limit ~10,000 requests/day. With 10 cities and 48 ingestions/day = 480 requests, well within limits.
- Technical context: All cities ingested regardless of subscriber count.

### Story 2: On-demand ingestion (new city)
- Status: Pending
- Ready: Yes
- Ready Reason: Trigger mechanism defined in PRD-0000 Story 2. Queue-based processing.
- User Story: As the system, I want to trigger weather ingestion immediately when a new city is created, so that users see weather data without waiting for the next scheduled run.

Acceptance Criteria:
1. Given a new city is created via POST /cities, when the city does not already exist in the database, then the system queues an ingestion message for that city.
2. Given a city already exists in the database, when a user subscribes to it via POST /cities, then the system does NOT queue a new ingestion (data already exists from scheduled runs).
3. Given the ingestion message is queued, when the worker processes it, then it follows the same flow as scheduled ingestion (fetch, upsert).

Data Requirements:
- Trigger: CityCreatedDomainEvent (when city is new)
- Queue message: CityId, Latitude, Longitude, TimeZone

Notes:
- Technical context: Domain event triggers queue message. Queue handler dispatches ingestion pipeline.

### Story 3: Admin weather data reset
- Status: Pending
- Ready: Yes
- Ready Reason: Simple delete operation. API contract defined.
- User Story: As an admin user, I want to reset all weather data for a city, so that I can clear stale or corrupted data and trigger a fresh ingestion.

Acceptance Criteria:
1. Given the admin calls DELETE /admin/cities/{cityId}/weather, when the city exists, then the system deletes all CurrentWeather and WeatherForecast records for that city and returns 204.
2. Given the city does not exist, when the admin calls DELETE /admin/cities/{cityId}/weather, then the system returns 404.
3. Given the weather data is deleted, when the next scheduled ingestion runs, then fresh data is repopulated for the city.
4. Given the weather data is deleted, when the admin triggers manual ingestion via POST /admin/cities/{cityId}/ingest, then fresh data is repopulated immediately.
5. The city record and its subscriptions are not affected by the weather data reset.
6. Given the admin resets weather data, when a user requests weather for that city before the next ingestion, then the system returns an empty response (no cached data to serve).

Data Requirements:
- cityId: GUID, required (from URL path)

Notes:
- Technical context: DELETE /admin/cities/{cityId}/weather. Cascading delete of CurrentWeather (by CityId) and WeatherForecast (by CityId). Does not delete the City or UserCity records.

## Non-Functional Notes
- Performance: Ingestion for a single city should complete within 10 seconds
- Reliability: Failed ingestions retried up to 3 times with exponential backoff
- Rate limiting: 100ms delay between sequential API calls for different cities
- Resilience: When Open-Meteo is unavailable, the system serves the last cached data with a staleDataWarning flag. The system logs warnings for failed ingestions. Cached data is never deleted by ingestion failures — only by admin reset or successful upsert.

## Open Questions
- None
