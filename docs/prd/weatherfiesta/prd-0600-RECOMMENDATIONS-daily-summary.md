---
id: PRD-0600
title: Daily Summary and Recommendations
slice: RECOMMENDATIONS
status: Implemented
---

# Product Requirements: Daily Summary and Recommendations

## Overview
Authenticated users receive actionable daily weather recommendations derived from current conditions and forecasts. Instead of raw data alone, the system translates weather codes, temperature, UV index, precipitation probability, and wind speed into human-readable advice like "Bring an umbrella" or "Wear sunscreen." Recommendations make the app genuinely useful for daily planning.

## Scope
- In scope: Daily recommendation generation, recommendation rules, per-city recommendations, recommendation categories
- Out of scope: Push notification delivery (v2), user-defined thresholds (v2), city subscription management (PRD-0000), weather data viewing (PRD-0100), dashboard summary (PRD-0500)

## Diagram

```text
[CurrentWeather + WeatherForecast]
  --> [Recommendation Engine]
       |-- Rule: precipitation > 60% --> "Bring an umbrella"
       |-- Rule: UV index > 6       --> "Wear sunscreen"
       |-- Rule: apparent temp < 5    --> "Dress warmly"
       |-- Rule: apparent temp > 35   --> "Stay hydrated"
       |-- Rule: wind > 40 km/h       --> "Wind advisory"
       |-- Rule: WMO thunderstorm     --> "Thunderstorm warning"
       |-- Rule: pleasant conditions  --> "Great day outdoors"
  --> [GET /cities/{cityId}/recommendations]
  --> [GET /dashboard includes recommendation highlights]
```
Covers AC: Story 1 AC1-6, Story 2 AC1-4, Story 3 AC1-3

## User Roles
- **User**: Authenticated user who views recommendations for their subscribed cities

## Stories

### Story 1: View daily recommendations for a city
- Status: Implemented
- Ready: Yes
- Ready Reason: Recommendation rules are fully defined. All source data (current weather, forecasts) exists in PRD-0100. No external dependencies.
- User Story: As an authenticated user, I want to see daily weather recommendations for a city I'm subscribed to, so that I know how to prepare for the day.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to the city, when they call GET /cities/{cityId}/recommendations, then the system returns a list of recommendations derived from current weather and today's forecast.
2. Given the user is not subscribed to the city, when they call GET /cities/{cityId}/recommendations, then the system returns 404.
3. Each recommendation includes: category, message, severity (info, caution, warning), and sourceCondition (the weather data that triggered it).
4. Given the weather data for the city has a staleDataWarning, when recommendations are generated, then each recommendation includes a staleDataWarning flag.
5. Given the user is subscribed but no weather data exists yet, when they call GET /cities/{cityId}/recommendations, then the system returns an empty array with a message "Weather data not yet available."
6. The response includes the user's unit preferences (temperatureUnit, windSpeedUnit) as metadata so the frontend can convert sourceCondition values for display.

Data Requirements:
- cityId: GUID, required (from URL path, must be subscribed)

Notes:
- Technical context: GET /cities/{cityId}/recommendations. Recommendations are computed at query time from existing weather data. No new data storage.
- Dependencies: PRD-0100 (current weather, daily forecast)
- Design decision: sourceCondition values in recommendations are always in metric units (celsius, kmh). The user's unit preferences are included as metadata for frontend conversion, consistent with PRD-0400 Story 3 AC5.

### Story 2: Recommendation rules
- Status: Implemented
- Ready: Yes
- Ready Reason: All rules are defined with clear thresholds and source data fields. No ambiguity.
- User Story: As the system, I want to apply defined recommendation rules to weather data, so that users receive consistent, actionable advice.

Acceptance Criteria:
1. Given the precipitation probability for today exceeds 60%, when recommendations are generated, then a recommendation with category "precipitation", message "Bring an umbrella", and severity "caution" is included.
2. Given the UV index max for today exceeds 6, when recommendations are generated, then a recommendation with category "uv", message "Wear sunscreen", and severity "caution" is included.
3. Given the apparent temperature (current or forecast min) is below 5°C, when recommendations are generated, then a recommendation with category "temperature", message "Dress warmly", and severity "caution" is included.
4. Given the apparent temperature (current or forecast max) exceeds 35°C, when recommendations are generated, then a recommendation with category "temperature", message "Stay hydrated and seek shade", and severity "warning" is included.
5. Given the wind speed max for today exceeds 40 km/h, when recommendations are generated, then a recommendation with category "wind", message "Wind advisory — secure loose items", and severity "caution" is included.
6. Given the WMO weather code indicates thunderstorm (codes 95, 96, 99), when recommendations are generated, then a recommendation with category "storm", message "Thunderstorm expected — seek shelter", and severity "warning" is included.
7. Given no adverse conditions are met, when recommendations are generated, then a single recommendation with category "general", message "Great day for outdoor activities", and severity "info" is included.

Data Requirements:
- Recommendation categories: precipitation, uv, temperature, wind, storm, general
- Severity levels: info, caution, warning
- Source conditions mapped from: precipitationProbabilityMax, uvIndexMax, apparentTemperatureMin, apparentTemperatureMax, windSpeedMax, WMO weather codes

Notes:
- Technical context: Rules are evaluated in order. Multiple recommendations can be active simultaneously. The "great day outdoors" recommendation only appears when no adverse conditions are met.
- Design decision: Thresholds are system-defined for v1. User-defined thresholds are out of scope (see PRD-0100 Story 5 design decision).

### Story 3: Recommendation categories and severity
- Status: Implemented
- Ready: Yes
- Ready Reason: Simple enumeration. No external dependencies.
- User Story: As a developer, I want well-defined recommendation categories and severity levels, so that the frontend can display recommendations with appropriate icons and styling.

Acceptance Criteria:
1. Each recommendation belongs to exactly one category from the set: precipitation, uv, temperature, wind, storm, general.
2. Each recommendation has a severity level from the set: info, caution, warning.
3. Given multiple recommendations for the same city, when the response is assembled, then recommendations are sorted by severity descending (warning first, then caution, then info).
4. Given multiple recommendations of the same severity, when the response is assembled, then they are sorted by category in the order: storm, temperature, precipitation, wind, uv, general.

Data Requirements:
- Categories: precipitation, uv, temperature, wind, storm, general
- Severity levels: info, caution, warning

Notes:
- Technical context: Categories and severities are enumerations. Frontend can map categories to icons (umbrella, sun, thermometer, wind, lightning, smile) and severities to colors (green, yellow, red).

## Non-Functional Notes
- Performance: Recommendation computation should be < 100ms (simple threshold checks on existing data)
- Consistency: Same weather data always produces the same recommendations (deterministic rules)
- Extensibility: New rules can be added without changing the API contract (new categories can be added to the enumeration)

## Open Questions
- None
