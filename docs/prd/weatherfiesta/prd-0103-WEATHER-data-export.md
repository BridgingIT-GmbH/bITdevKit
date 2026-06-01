---
id: PRD-0103
title: Data Export
slice: WEATHER
status: Pending
---

# Product Requirements: Data Export

## Overview
Authenticated users can export weather data for their subscribed cities as CSV files. This enables personal tracking, analysis in spreadsheets, and long-term record keeping. Power users who track weather patterns over time need this to get value beyond real-time viewing.

## Scope
- In scope: Export current weather as CSV, export daily forecast as CSV, export format options
- Out of scope: Full weather data viewing (PRD-0100), comparison (PRD-0102), dashboard summary (PRD-0500), admin data export

## User Roles
- **User**: Authenticated user who exports weather data for their subscribed cities

## Stories

### Story 1: Export current weather as CSV
- Status: Pending
- Ready: Yes
- Ready Reason: Simple CSV projection of existing data. No new storage. Format well-defined.
- User Story: As an authenticated user, I want to export current weather data for my subscribed cities as a CSV file, so that I can analyze it in a spreadsheet.

Acceptance Criteria:
1. Given the user is authenticated, when they call GET /cities/export?format=csv, then the system returns a CSV file with current weather data for all their subscribed cities.
2. The CSV includes columns: cityName, country, latitude, longitude, temperature, apparentTemperature, humidity, weatherCode, weatherDescription, windSpeed, windDirection, windGusts, precipitation, cloudCover, pressure, staleDataWarning, retrievedAt.
3. When the user has no subscribed cities, then the system returns a CSV with headers only and no data rows.
4. The CSV filename follows the pattern "weatherfiesta-current-{date}.csv" where date is YYYY-MM-DD format in UTC.
5. The response Content-Type header is "text/csv" and Content-Disposition header includes the filename.

Data Requirements:
- format: string, optional, default "csv", valid values: ["csv"]
- No city ID filter for v1 — exports all subscribed cities.

Notes:
- Technical context: GET /cities/export?format=csv. Streams CSV response. No temporary file storage on server.
- Design decision: v1 supports CSV only. JSON and other formats can be added later via the format parameter.
- Design decision: CSV exports always use metric units (celsius, kmh) per PRD-0400 Story 3 AC5. User preferences are not applied to export data.

### Story 2: Export daily forecast as CSV
- Status: Pending
- Ready: Yes
- Ready Reason: Same mechanism as Story 1, extended to forecast data. Format well-defined.
- User Story: As an authenticated user, I want to export daily forecast data for a specific city as a CSV file, so that I can track weather patterns over time.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to the city, when they call GET /cities/{cityId}/weather/export?format=csv&days=7, then the system returns a CSV file with daily forecast data for that city.
2. The CSV includes columns: cityName, forecastDate, dayWeatherCode, weatherDescription, temperatureMax, temperatureMin, apparentTemperatureMax, apparentTemperatureMin, precipitationSum, precipitationProbabilityMax, windSpeedMax, windGustsMax, dominantWindDirection, uvIndexMax, sunshineDurationSeconds, daylightDurationSeconds, sunrise, sunset, staleDataWarning, retrievedAt.
3. When the days parameter is omitted, then the default is 7 days.
4. When the days parameter exceeds 16, then the system returns 400 with message "Maximum forecast days is 16."
5. Given the user is not subscribed to the city, when they call GET /cities/{cityId}/weather/export, then the system returns 404.

Data Requirements:
- cityId: GUID, required (from URL path, must be subscribed)
- format: string, optional, default "csv", valid values: ["csv"]
- days: int, optional, default 7, range 1-16

Notes:
- Technical context: GET /cities/{cityId}/weather/export?format=csv&days=N. Streams CSV response from WeatherForecast data.
- Design decision: Per-city export for forecasts because a multi-city forecast CSV would be wide and confusing. Current weather export (Story 1) covers all cities because it's one row per city.

### Story 3: Export format and encoding
- Status: Pending
- Ready: Yes
- Ready Reason: Standard CSV conventions. No ambiguity.
- User Story: As an authenticated user, I want the exported CSV to be well-formatted and compatible with common spreadsheet applications, so that I can open it directly without manual cleanup.

Acceptance Criteria:
1. The CSV uses UTF-8 encoding with a BOM (Byte Order Mark) for Excel compatibility.
2. The CSV uses comma as the delimiter and double quotes for field quoting when fields contain commas, quotes, or newlines.
3. Numeric fields use period as the decimal separator (e.g., 12.5, not 12,5).
4. Date and time fields use ISO 8601 format (YYYY-MM-DD for dates, YYYY-MM-DDTHH:MM:SSZ for timestamps).
5. The first row contains column headers matching the field names specified in Story 1 and Story 2.

Data Requirements:
- Encoding: UTF-8 with BOM
- Delimiter: comma
- Quoting: RFC 4180 compliant
- Decimal separator: period
- Date format: ISO 8601

Notes:
- Technical context: BOM ensures Excel opens the CSV correctly with UTF-8 characters (city names with accents, special characters). RFC 4180 is the CSV standard.
- Design decision: ISO 8601 dates for machine readability. Users who want locale-specific formatting can convert in their spreadsheet.

## Non-Functional Notes
- Performance: CSV export should stream (not buffer entire file in memory). Response time < 2 seconds for up to 50 cities (current) or 16 days × 1 city (forecast).
- Security: Users can only export data for their subscribed cities.
- Data volume: Current weather export for 50 cities ≈ 50 rows. Forecast export for 16 days ≈ 16 rows. Both are small.

## Open Questions
- None
