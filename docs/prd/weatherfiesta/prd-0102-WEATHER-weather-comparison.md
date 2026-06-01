---
id: PRD-0102
title: Weather Comparison
slice: WEATHER
status: Partial
---

# Product Requirements: Weather Comparison

## Overview
Authenticated users can compare weather conditions across multiple subscribed cities side by side. This answers the question "Which of my cities is warmest this weekend?" or "Should I pack a jacket for Helsinki vs Berlin?" — the #1 reason people check multiple cities in a weather app.

## Scope
- In scope: Side-by-side current-weather comparison, comparison metrics, multi-day comparison requirement tracking
- Out of scope: Full weather data viewing (PRD-0100), dashboard summary (PRD-0500), recommendations (PRD-0600)

## Diagram

```text
[POST /cities/compare]
  |-- request body: [id1, id2, id3]
  |-- for each city:
  |     |-- current weather snapshot
  |-- comparison summary:
        |-- warmest city (max apparent temp)
        |-- coldest city (min apparent temp)
        |-- wettest city (max precipitation prob)
        |-- windiest city (max wind speed)
```
Covers AC: Story 1 AC1, AC4-6 and Story 3 AC1, AC3

## User Roles
- **User**: Authenticated user who compares weather across subscribed cities

## Stories

### Story 1: Compare current weather across cities
- Status: Implemented
- Ready: Yes
- Ready Reason: Data already exists in CurrentWeather table. Aggregation endpoint with no new storage.
- User Story: As an authenticated user, I want to compare current weather conditions across multiple subscribed cities side by side, so that I can quickly see which city is warmest, coldest, or wettest right now.

Acceptance Criteria:
1. Given the user is authenticated, when they call POST /cities/compare with 2 to 10 city IDs in the request body, then the system returns current weather data for each specified city in a single response.
2. Given any city ID in the request is not in the user's subscriptions, when the system processes the request, then that city is omitted from the comparison response.
3. Given any city ID does not exist, when the system processes the request, then that city is omitted from the comparison response.
4. When fewer than 2 city IDs are provided, then the system returns a validation error with message "Must compare between 2 and 10 cities."
5. When more than 10 city IDs are provided, then the system returns a validation error with message "Must compare between 2 and 10 cities."
6. Given any city's weather data has a staleDataWarning, when the comparison response is assembled, then each city's weather object includes a staleDataWarning flag.

Data Requirements:
- cityIds: array of GUIDs, required, minimum 2, maximum 10. Requested cities are expected to be subscribed; the current implementation omits IDs that are missing or not subscribed.
- Response: array of city comparison objects, each containing cityId, currentWeather, staleDataWarning, and staleDataWarningMessage.

Notes:
- Technical context: POST /cities/compare with a JSON array of city IDs. The implemented handler verifies UserCity subscriptions and loads CurrentWeather for each requested city. No new data storage.
- Dependencies: PRD-0000 (subscription check), PRD-0100 (current weather data)

### Story 2: Compare multi-day forecast across cities
- Status: Pending
- Ready: Yes
- Ready Reason: Requirement remains valid, but the current CityCompareQuery implementation returns current-weather comparison only and does not include forecast data.
- User Story: As an authenticated user, I want to compare daily forecasts across multiple cities for the next several days, so that I can plan travel or activities across cities.

Acceptance Criteria:
1. Given the user is authenticated, when they request forecast comparison for 2 to 10 city IDs, then the system returns daily forecast data for each city included in the comparison response.
2. Each city's forecast includes the same daily fields as GET /cities/{cityId}/weather (forecastDate, dayWeatherCode, weatherDescription, temperatureMax, temperatureMin, apparentTemperatureMax, apparentTemperatureMin, precipitationSum, precipitationProbabilityMax, windSpeedMax, windGustsMax, dominantWindDirection, uvIndexMax, sunshineDurationSeconds, daylightDurationSeconds, sunrise, sunset).
3. When the days parameter is omitted, then the default is 3 days.
4. When the days parameter exceeds 16, then the system returns 400 with message "Maximum forecast days is 16."
5. Given any city's forecast data has a staleDataWarning, when the comparison response is assembled, then each city's forecast object includes a staleDataWarning flag.

Data Requirements:
- cityIds: array of GUIDs, required, minimum 2, maximum 10
- days: future optional parameter or request field, default 3, range 1-16.

Notes:
- Technical context: Not implemented in the current POST /cities/compare handler. A future implementation can extend CityCompareQuery or add a dedicated forecast-comparison endpoint.
- Design decision: City IDs are sent in the request body to avoid long query strings and keep request validation simple.

### Story 3: Comparison highlights
- Status: Partial
- Ready: Yes
- Ready Reason: Current-weather highlights are implemented. Highlight entries currently include cityId, value, and unit; cityName and weatherCode are not included.
- User Story: As an authenticated user, I want to see comparison highlights (warmest, coldest, wettest, windiest) across the cities I'm comparing, so that I can quickly identify the best or worst conditions.

Acceptance Criteria:
1. Given the user compares 2 or more cities, when the comparison response is assembled, then it includes a highlights section with: warmest city (highest apparent temperature), coldest city (lowest apparent temperature), wettest city (highest precipitation probability), and windiest city (highest wind speed).
2. Each highlight entry includes: cityId, cityName, value, unit, and weatherCode.
3. Given the user's unit preferences are set, when the comparison response is assembled, then the highlights include the user's temperatureUnit and windSpeedUnit as metadata for frontend conversion.

Data Requirements:
- No additional data. Highlights computed from CurrentWeather data for the compared cities.
- Unit preferences from user profile (PRD-0400 Story 3).

Notes:
- Technical context: Highlights are computed at query time from the same current-weather data returned in the comparison. The current response does not include cityName or weatherCode on highlight entries.
- Dependencies: PRD-0400 (unit preferences), PRD-0100 (current weather data)

## Non-Functional Notes
- Performance: Comparison response should be < 500ms for up to 10 cities. Single aggregated query preferred over N+1.
- Security: All city IDs must belong to the user's subscriptions. No cross-user data leakage.

## Open Questions
- None
