---
id: PRD-0000
title: City Subscriptions
slice: CITIES
status: Pending
---

# Product Requirements: City Subscriptions

## Overview
Authenticated users can search for cities, subscribe to them, view their subscribed cities, and unsubscribe. Cities are global entities shared across users. Subscriptions are per-user (UserCity). Geocoding is handled server-side via Open-Meteo API. Users can set a primary city for quick dashboard access.

## Scope
- In scope: City suggestions (geocoding), subscribe, unsubscribe, reactivate, view my cities, primary city selection
- Out of scope: Weather data viewing (PRD-0100), ingestion (PRD-0200), admin operations (PRD-0300), user notification on admin city deletion

## User Roles
- **User**: Authenticated user who subscribes to cities and views weather data
- **Admin**: Elevated user who can manage global cities, view all subscriptions, trigger ingestion

## Stories

### Story 1: City name suggestions
- Status: Pending
- Ready: Yes
- Ready Reason: All data requirements and API contracts are defined. External dependency on Open-Meteo Geocoding API is documented.
- User Story: As an authenticated user, I want to see city name suggestions as I type, so that I can quickly find and subscribe to the right city.

Acceptance Criteria:
1. Given the user is authenticated, when they type 3 or more characters into the search field, then the system calls GET /cities/suggestions and returns up to 10 matching results.
2. When the search query has fewer than 3 characters, then the system returns a 400 error with message "Name parameter must be at least 3 characters."
3. When the Open-Meteo Geocoding API is unavailable, then the system returns a 502 error with message "Geocoding API unavailable."
4. When no results match the query, then the system returns an empty array.
5. Each result includes: externalId, name, latitude, longitude, elevation, country, countryCode, admin1, timeZone.

Data Requirements:
- name: string, required, minimum 3 characters
- countryCode: string, optional, ISO 3166-1 alpha-2 code

Notes:
- Dependencies: Open-Meteo Geocoding API (external, free, no API key)
- Risks: Geocoding API rate limits (~10,000 requests/day for non-commercial)
- Technical context: GET /cities/suggestions?name={query}&countryCode={code}

### Story 2: Subscribe to a city
- Status: Pending
- Ready: Yes
- Ready Reason: Full API contract defined. Geocoding lookup and auto-selection logic specified. Reactivation logic defined.
- User Story: As an authenticated user, I want to subscribe to a city by name, so that it appears on my dashboard.

Acceptance Criteria:
1. Given the user is authenticated, when they submit POST /cities with {name, countryCode, externalId?}, then the system geocodes the name (or looks up by externalId), creates the city if needed, creates the UserCity subscription, and returns 201.
2. Given the user already has an active subscription to the city, when they submit POST /cities, then the system returns 409 with message "Already subscribed to this city."
3. Given the user had a previously soft-deleted subscription to the city, when they submit POST /cities, then the system reactivates the subscription in-place (sets IsDeleted = false), resets DisplayOrder to the end of the list per PRD-0001 Story 3 AC3, and returns 200.
4. Given the externalId is provided, when the system looks up the geocoding result, then it uses the exact coordinates from the lookup response.
5. Given the externalId is not provided, when the system geocodes the name, then it auto-selects the first result.
6. When geocoding returns no results, then the system returns 400 with message "No geocoding results found."
7. When the Open-Meteo Geocoding API is unavailable, then the system returns 502.

Data Requirements:
- name: string, required
- countryCode: string, optional, ISO 3166-1 alpha-2 code
- externalId: long?, optional, Open-Meteo geocoding ID

Notes:
- Dependencies: Open-Meteo Geocoding API and Lookup API
- Technical context: POST /cities. City deduplication by ExternalId or (Latitude, Longitude). UserCity deduplication by (UserId, CityId).

### Story 3: View my cities
- Status: Pending
- Ready: Yes
- Ready Reason: Simple query endpoint. Data model and API contract defined.
- User Story: As an authenticated user, I want to see a list of cities I'm subscribed to, so that I can manage my subscriptions.

Acceptance Criteria:
1. Given the user is authenticated, when they call GET /cities, then the system returns all cities where the user has an active subscription (IsDeleted = false).
2. When the user has no subscriptions, then the system returns an empty array.
3. Each city entry includes: id, name, country, countryCode, timeZone, location, isPrimary, displayOrder, and currentWeather summary (temperature, weatherCode, weatherDescription).
4. When current weather data is not yet available for a city, then the currentWeather field is null.
5. When current weather data is stale (retrieved more than 60 minutes ago), then the currentWeather object includes a staleDataWarning field set to true.
6. When the user has a primary city, then that city entry includes isPrimary: true. Only one city per user can be primary.

Data Requirements:
- No input parameters. UserId resolved from authentication context.

Notes:
- Technical context: GET /cities. Joins UserCity with City and CurrentWeather.

### Story 4: Unsubscribe from a city
- Status: Pending
- Ready: Yes
- Ready Reason: Simple soft-delete operation. Business rules defined.
- User Story: As an authenticated user, I want to remove a city from my subscriptions, so that it no longer appears on my dashboard.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to the city, when they call DELETE /cities/{cityId}, then the system soft-deletes the UserCity subscription (sets IsDeleted = true) and returns 204.
2. Given the user is not subscribed to the city, when they call DELETE /cities/{cityId}, then the system returns 404.
3. When an optional deleteReason is provided, then it is stored on the UserCity record.
4. Given the user soft-deletes a subscription, when they later call POST /cities for the same city, then the system reactivates the subscription (sets IsDeleted = false).
5. When a subscription is soft-deleted, then the city and its weather data are not affected.
6. When a subscription is soft-deleted, then DisplayOrder gaps are closed per PRD-0001 Story 3 AC4.
7. Given the user unsubscribes from their primary city, when the soft-delete completes, then IsPrimary is set to false for that subscription. The user has no primary city until they explicitly set a new one.

Data Requirements:
- cityId: GUID, required (from URL path)
- deleteReason: string?, optional (from request body)

Notes:
- Technical context: DELETE /cities/{cityId}. Soft-delete only — City is never deleted by regular users.

### Story 5: Set primary city
- Status: Pending
- Ready: Yes
- Ready Reason: Simple flag update on UserCity. Business rules defined.
- User Story: As an authenticated user, I want to mark one of my subscribed cities as primary, so that my dashboard opens to that city by default.

Acceptance Criteria:
1. Given the user is authenticated and subscribed to the city, when they call PUT /cities/{cityId}/primary, then the system sets IsPrimary = true for that UserCity and sets IsPrimary = false for all other UserCity records for that user, and returns 200.
2. Given the user is not subscribed to the city, when they call PUT /cities/{cityId}/primary, then the system returns 404.
3. Given the user sets a new primary city, when the previous primary city's IsPrimary is set to false, then only one primary city exists per user at any time.
4. When the user has no primary city set, then GET /cities returns all cities with isPrimary: false for each.

Data Requirements:
- cityId: GUID, required (from URL path, must be subscribed)

Notes:
- Technical context: PUT /cities/{cityId}/primary. Toggles IsPrimary flag on UserCity. Only one primary per user enforced by setting all others to false.

## Non-Functional Notes
- Performance: Geocoding suggestions should respond within 2 seconds
- Security: All endpoints require authentication. Users can only access their own subscriptions.

## Open Questions
- None
