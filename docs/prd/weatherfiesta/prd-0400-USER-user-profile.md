---
id: PRD-0400
title: User Profile
slice: USER
status: Pending
---

# Product Requirements: User Profile

## Overview
Authenticated users can view and update their profile, configure display preferences (temperature and wind speed units), and delete their account. The profile stores identity information and unit preferences that the frontend uses to convert weather data from metric to the user's preferred display units.

## Scope
- In scope: View profile, update profile, unit preferences (temperature, wind speed), delete account
- Out of scope: City subscription management (PRD-0000), weather data viewing (PRD-0100), ingestion (PRD-0200), admin operations (PRD-0300), authentication/registration (handled by identity provider)

## User Roles
- **User**: Authenticated user who manages their own profile and preferences

## Stories

### Story 1: View user profile
- Status: Pending
- Ready: Yes
- Ready Reason: Simple query endpoint. Data model and API contract defined.
- User Story: As an authenticated user, I want to view my profile, so that I can see my account information.

Acceptance Criteria:
1. Given the user is authenticated, when they call GET /users/me, then the system returns their profile including id, email, name, createdAt.
2. Given the user is not authenticated, when they call GET /users/me, then the system returns 401.
3. The response includes the user's unit preferences (temperatureUnit, windSpeedUnit) with defaults if not set.

Data Requirements:
- No input parameters. UserId resolved from authentication context.

Notes:
- Technical context: GET /users/me. Returns user identity plus preferences in a single response.

### Story 2: Update user profile
- Status: Pending
- Ready: Yes
- Ready Reason: Simple update endpoint. Fields and validation rules defined.
- User Story: As an authenticated user, I want to update my profile information, so that my account details stay current.

Acceptance Criteria:
1. Given the user is authenticated, when they call PUT /users/me with valid data, then the system updates the profile and returns 200.
2. Given the user is not authenticated, when they call PUT /users/me, then the system returns 401.
3. When the user provides an empty name, then the system returns 400 with message "Name is required."
4. When the user provides a name longer than 200 characters, then the system returns 400 with message "Name must be 200 characters or fewer."
5. When the user provides an invalid email format, then the system returns 400 with message "Invalid email format."

Data Requirements:
- name: string, required, max 200 characters
- email: string, required, valid email format

Notes:
- Technical context: PUT /users/me. Partial updates supported — only provided fields are updated.

### Story 3: User unit preferences
- Status: Pending
- Ready: Yes
- Ready Reason: Simple preference storage. No external dependencies. API contract defined.
- User Story: As an authenticated user, I want to set my preferred temperature and wind speed units, so that the frontend can display weather data in my preferred units.

Acceptance Criteria:
1. Given the user is authenticated, when they call GET /users/preferences, then the system returns their current unit preferences (temperatureUnit, windSpeedUnit). Default values are "celsius" and "kmh" if not set.
2. Given the user is authenticated, when they call PUT /users/preferences with { temperatureUnit, windSpeedUnit }, then the system saves the preferences and returns 200.
3. When the user provides an invalid temperatureUnit value, then the system returns 400 with message "Invalid temperature unit. Valid values: celsius, fahrenheit."
4. When the user provides an invalid windSpeedUnit value, then the system returns 400 with message "Invalid wind speed unit. Valid values: kmh, mph, ms, knots."
5. The API always returns weather data in metric units (celsius, kmh). The user preference is returned as metadata so the frontend can convert display values.

Data Requirements:
- temperatureUnit: string, enum ["celsius", "fahrenheit"], default "celsius"
- windSpeedUnit: string, enum ["kmh", "mph", "ms", "knots"], default "kmh"

Notes:
- Technical context: GET/PUT /users/preferences. Preferences stored on user profile. Weather API responses always include metric values plus the user's preference as metadata (e.g., { "temperatureUnit": "fahrenheit" }) so the frontend can convert for display.
- Design decision: API returns metric + preference metadata. Frontend handles conversion. This keeps the API simple and stateless for weather data.

### Story 4: Delete account
- Status: Pending
- Ready: Yes
- Ready Reason: Simple soft-delete with cascade rules defined. No external dependencies.
- User Story: As an authenticated user, I want to delete my account, so that my personal data is removed from the system.

Acceptance Criteria:
1. Given the user is authenticated, when they call DELETE /users/me, then the system soft-deletes the user account and returns 204.
2. Given the user is not authenticated, when they call DELETE /users/me, then the system returns 401.
3. When the user account is soft-deleted, then all UserCity subscriptions for that user are also soft-deleted.
4. When the user account is soft-deleted, then the user can no longer authenticate and all endpoints return 401.
5. Given the user had a primary city set, when the account is deleted, then the primary city flag is cleared (no orphan data).

Data Requirements:
- No input parameters. UserId resolved from authentication context.
- Confirmation: Requires explicit confirmation in the request body (e.g., { "confirm": true }).

Notes:
- Technical context: DELETE /users/me. Soft-delete only — user record is marked as deleted, not physically removed. Cascade soft-delete of all UserCity records for the user.
- Design decision: Soft-delete preserves data integrity. Hard-delete can be added later as an admin operation or scheduled cleanup.
- Dependencies: Identity provider must invalidate the user's session/token on deletion.

## Non-Functional Notes
- Performance: Profile endpoints should respond within 200ms
- Security: All endpoints require authentication. Users can only access their own profile.
- Data privacy: Account deletion cascades to subscriptions. No weather data is deleted (it belongs to global cities).

## Open Questions
- None
