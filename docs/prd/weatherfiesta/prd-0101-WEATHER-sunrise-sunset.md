---
id: PRD-0101
title: Sunrise and Sunset
slice: WEATHER
status: Pending
---

# Product Requirements: Sunrise and Sunset

## Overview
Authenticated users can view sunrise, sunset, and daylight information for their subscribed cities. This data is already ingested from Open-Meteo as part of the daily forecast but is not currently exposed through a dedicated endpoint. A lightweight endpoint makes it easy for the frontend to display sun times without fetching the full forecast.

## Scope
- In scope: Dedicated sunrise/sunset endpoint, current day sun data, multi-day sun data
- Out of scope: Full weather data viewing (PRD-0100), city subscription management (PRD-0000), dashboard summary (PRD-0500)

## User Roles
- **User**: Authenticated user who views sun times for subscribed cities

## Stories

### Story 1: View today's sun times
- Status: Pending
- Ready: Yes
- Ready Reason: Data already exists in WeatherForecast records. Simple projection endpoint.
- User Story: As an authenticated user, I want to see today's sunrise and sunset times for a city I'm subscribed to, so that I can plan my day around daylight hours.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to the city, when they call GET /cities/{cityId}/sun, then the system returns today's sunrise, sunset, daylightDurationSeconds, and isDay (whether it is currently daytime) for that city.
2. Given the user is not subscribed to the city, when they call GET /cities/{cityId}/sun, then the system returns 404.
3. Given the user is subscribed but no forecast data exists for today, when they call GET /cities/{cityId}/sun, then the system returns an empty response with a message "Sun data not yet available."
4. The response includes the city's timeZone so the frontend can display times in the city's local time.
5. Given the weather data has a staleDataWarning, when sun times are returned, then the response includes the staleDataWarning flag.

Data Requirements:
- cityId: GUID, required (from URL path, must be subscribed)
- Response fields: sunrise (time, city local time), sunset (time, city local time), daylightDurationSeconds (int), sunshineDurationSeconds (int), isDay (boolean), timeZone (string), staleDataWarning (boolean, optional)

Notes:
- Technical context: GET /cities/{cityId}/sun. Data sourced from WeatherForecast record for today's date. No new data storage.
- Dependencies: PRD-0100 (daily forecast ingestion, which already includes sunrise/sunset/sunshineDuration/daylightDuration)
- Design decision: Unit preferences metadata is not included in sun endpoint responses because sun data (sunrise, sunset, durations) has no unit-dependent fields. All times are in ISO 8601 format and durations are in seconds.

### Story 2: View multi-day sun times
- Status: Pending
- Ready: Yes
- Ready Reason: Same data source as Story 1, just extended date range. Simple parameter addition.
- User Story: As an authenticated user, I want to see sunrise and sunset times for the next several days, so that I can plan activities across the week.

Acceptance Criteria:
1. Given the user is subscribed to the city, when they call GET /cities/{cityId}/sun?days=7, then the system returns sun data for up to 7 days starting from today.
2. Each day entry includes: date, sunrise, sunset, daylightDurationSeconds, sunshineDurationSeconds.
3. When the days parameter is omitted, then the default is 1 (today only).
4. When the days parameter exceeds 16, then the system returns 400 with message "Maximum days is 16."

Data Requirements:
- cityId: GUID, required (from URL path, must be subscribed)
- days: int, optional, default 1, range 1-16

Notes:
- Technical context: GET /cities/{cityId}/sun?days={n}. Same data source as Story 1, iterated over multiple WeatherForecast records.

### Story 3: Is-day indicator
- Status: Pending
- Ready: Yes
- Ready Reason: Simple boolean computed from current time vs sunrise/sunset. No new data.
- User Story: As an authenticated user, I want to know if it is currently daytime in a subscribed city, so that I can see at a glance whether it's day or night there.

Acceptance Criteria:
1. Given the user is subscribed to the city, when they call GET /cities/{cityId}/sun, then the response includes an isDay boolean indicating whether the current time in the city's timezone is between sunrise and sunset.
2. Given the current time is before sunrise in the city, when the response is returned, then isDay is false.
3. Given the current time is after sunset in the city, when the response is returned, then isDay is false.
4. Given the current time is between sunrise and sunset in the city, when the response is returned, then isDay is true.

Data Requirements:
- isDay: boolean, computed from current UTC time, city timeZone, sunrise, and sunset

Notes:
- Technical context: isDay is computed at query time by comparing the current time (in the city's local time zone) against today's sunrise and sunset. No storage needed.
- Design decision: isDay is computed server-side so the frontend doesn't need timezone logic. The server has the city's timeZone from the City record.

## Non-Functional Notes
- Performance: Sun times response should be < 200ms (simple lookup from existing forecast data)
- Security: Requires authentication and subscription verification (same as PRD-0100)

## Open Questions
- None
