---
id: PRD-0500
title: Dashboard Summary
slice: DASHBOARD
status: Implemented
---

# Product Requirements: Dashboard Summary

## Overview
Authenticated users get a personalized dashboard that aggregates weather data across all their subscribed cities. The dashboard shows the primary city's current weather prominently, highlights across all subscribed cities (warmest, coldest, wettest, windiest), a summary of active alerts, and recommendation highlights. This is the landing page experience that makes the app useful at a glance.

## Scope
- In scope: Dashboard endpoint, primary city highlight, cross-city highlights, alert summary, recommendation highlights
- Out of scope: Detailed weather data viewing (PRD-0100), city subscription management (PRD-0000), user profile (PRD-0400), push notifications

## Diagram

```text
[GET /dashboard]
  |-- primary city current weather (if set)
  |-- highlights across subscribed cities
  |     |-- warmest city
  |     |-- coldest city
  |     |-- wettest city (most precipitation)
  |     |-- windiest city
  |-- active alerts count
  |-- stale data warnings
  |-- recommendation highlights
```
Covers AC: Story 1 AC1-5, Story 2 AC1-4, Story 3 AC1-4, Story 4 AC1-3, Story 5 AC1-3

## User Roles
- **User**: Authenticated user who views their personalized dashboard

## Stories

### Story 1: View dashboard
- Status: Implemented
- Ready: Yes
- Ready Reason: Aggregation endpoint. All source data defined in PRD-0000, PRD-0100. No external dependencies.
- User Story: As an authenticated user, I want to see a dashboard with weather highlights for all my subscribed cities, so that I get a quick overview without navigating to each city individually.

Acceptance Criteria:
1. Given the user is authenticated and has subscribed cities, when they call GET /dashboard, then the system returns a dashboard response with primary city weather, cross-city highlights, alert summary, and recommendation highlights.
2. Given the user has no subscribed cities, when they call GET /dashboard, then the system returns an empty dashboard with a message "No cities subscribed yet." Recommendations and alerts are empty.
3. Given the user is not authenticated, when they call GET /dashboard, then the system returns 401.
4. The dashboard response includes a retrievedAt timestamp indicating when the dashboard data was assembled.
5. Given the user has subscribed cities but no primary city set, when they call GET /dashboard, then the primary city section is null and the response includes a suggestion to set a primary city.

Data Requirements:
- No input parameters. UserId resolved from authentication context.

Notes:
- Technical context: GET /dashboard. Aggregates data from CurrentWeather, WeatherForecast, and UserCity tables. No new data storage — this is a read-only projection.
- Dependencies: PRD-0000 (subscriptions, primary city), PRD-0100 (current weather, forecasts, alerts), PRD-0600 (recommendation rules)

### Story 2: Primary city highlight
- Status: Implemented
- Ready: Yes
- Ready Reason: Depends on primary city (PRD-0000 Story 5) and current weather (PRD-0100 Story 1). Data already available.
- User Story: As an authenticated user, I want to see my primary city's current weather prominently on the dashboard, so that I immediately see the weather that matters most to me.

Acceptance Criteria:
1. Given the user has a primary city set, when they call GET /dashboard, then the response includes a primaryCity section with the city's current weather data (temperature, apparentTemperature, weatherCode, weatherDescription, humidity, windSpeed, windDirection, windGusts, precipitation, cloudCover, pressure).
2. Given the user has a primary city set, when the primary city's weather data includes a staleDataWarning, then the primaryCity section includes the staleDataWarning flag and message.
3. Given the user has a primary city set but no weather data exists yet, when they call GET /dashboard, then the primaryCity section shows the city name with a "Weather data not yet available" message.
4. The primaryCity section includes the user's unit preferences (temperatureUnit, windSpeedUnit) as metadata for frontend conversion.

Data Requirements:
- No additional data. Primary city resolved from UserCity.IsPrimary.

Notes:
- Technical context: Primary city data sourced from CurrentWeather table. Unit preferences from user profile (PRD-0400 Story 3).

### Story 3: Cross-city highlights
- Status: Implemented
- Ready: Yes
- Ready Reason: Computed from existing weather data. No new data storage.
- User Story: As an authenticated user, I want to see weather highlights across all my subscribed cities, so that I can quickly compare conditions.

Acceptance Criteria:
1. Given the user has 2 or more subscribed cities with weather data, when they call GET /dashboard, then the highlights section includes: warmest city (highest apparent temperature), coldest city (lowest apparent temperature), wettest city (highest precipitation probability), and windiest city (highest wind speed).
2. Given the user has only 1 subscribed city, when they call GET /dashboard, then the highlights section is omitted (not meaningful with a single city).
3. Each highlight entry includes: cityId, cityName, value, unit, and weatherCode.
4. Given the user's unit preferences are set, when the highlights section is assembled, then it includes the user's temperatureUnit and windSpeedUnit as metadata for frontend conversion.

Data Requirements:
- No additional data. Highlights computed from CurrentWeather across all subscribed cities.

Notes:
- Technical context: Highlights are computed at query time from CurrentWeather data. No pre-computation or caching needed for v1.
- Design decision: Highlights use apparent temperature (not raw temperature) because it accounts for wind chill and humidity — what it actually feels like.

### Story 4: Alert summary on dashboard
- Status: Implemented
- Ready: Yes
- Ready Reason: Aggregates existing alert data from PRD-0100 Story 5. No new data.
- User Story: As an authenticated user, I want to see a count of active weather alerts on my dashboard, so that I'm immediately aware of dangerous conditions.

Acceptance Criteria:
1. Given the user has subscribed cities with active alerts, when they call GET /dashboard, then the alerts section includes a count of active alerts and a summary list with cityId, cityName, alertType, and severity for each alert.
2. Given the user has no active alerts, when they call GET /dashboard, then the alerts section shows a count of 0 and an empty list.
3. Given alerts are based on stale data (staleDataWarning), when the dashboard is assembled, then each stale alert includes a staleDataWarning flag.

Data Requirements:
- No additional data. Alert rules defined in PRD-0100 Story 5.

Notes:
- Technical context: Alert computation reuses the same rules as GET /cities/alerts (PRD-0100 Story 5). The dashboard provides a summary; full alert details are available via the alerts endpoint.
- Dependencies: PRD-0100 Story 5 (system weather alerts)

### Story 5: Recommendation highlights on dashboard
- Status: Implemented
- Ready: Yes
- Ready Reason: Aggregates existing recommendation data from PRD-0600. No new data.
- User Story: As an authenticated user, I want to see a summary of weather recommendations on my dashboard, so that I can quickly see what actions I should take today.

Acceptance Criteria:
1. Given the user has subscribed cities with active recommendations, when they call GET /dashboard, then the recommendations section includes a count of recommendations and a summary list with cityId, cityName, category, message, and severity for each recommendation.
2. Given the user has no active recommendations (all cities have pleasant weather), when they call GET /dashboard, then the recommendations section shows a single "Great day for outdoor activities" recommendation with severity "info".
3. Given recommendations are based on stale data (staleDataWarning), when the dashboard is assembled, then each stale recommendation includes a staleDataWarning flag.

Data Requirements:
- No additional data. Recommendation rules defined in PRD-0600 Story 2.

Notes:
- Technical context: Recommendation computation reuses the same rules as GET /cities/{cityId}/recommendations (PRD-0600). The dashboard provides a summary across all subscribed cities; detailed per-city recommendations are available via the recommendations endpoint.
- Dependencies: PRD-0600 (recommendation rules)

## Non-Functional Notes
- Performance: Dashboard response should be < 500ms. Aggregation queries should use indexed lookups on UserCity and CurrentWeather.
- Caching: Consider short-lived cache (60 seconds) for dashboard responses in high-traffic scenarios. Not required for v1.
- Security: Requires authentication. Users can only see data for their subscribed cities.

## Open Questions
- None
