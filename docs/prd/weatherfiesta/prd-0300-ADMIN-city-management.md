---
id: PRD-0300
title: Admin City Management
slice: ADMIN
status: Pending
---

# Product Requirements: Admin City Management

## Overview
Admin users can manage the global city catalog directly (without geocoding), view subscriptions, trigger ingestion for any city, and reset weather data. These operations are restricted to users with the admin role.

## Scope
- In scope: Admin CRUD for cities, view subscriptions, trigger ingestion for any city, weather data reset
- Out of scope: City subscription management (PRD-0000), weather data viewing (PRD-0100), ingestion internals (PRD-0200)

## User Roles
- **Admin**: Elevated user with full CRUD access to global cities, subscription visibility, and ingestion control

## Stories

### Story 1: Admin - Manage cities
- Status: Pending
- Ready: Yes
- Ready Reason: Full API contract defined. Admin role requirement clear. No external dependencies for direct city creation.
- User Story: As an admin user, I want to create, update, and delete cities directly, so that I can manage the city catalog without relying on geocoding.

Acceptance Criteria:
1. Given the admin calls POST /admin/cities with valid data, when the city does not already exist (by ExternalId or coordinates), then the system creates the city and returns 201.
2. Given the admin calls POST /admin/cities with an ExternalId or coordinates that already exist, then the system returns 409 with message "City already exists."
3. Given the admin calls PUT /admin/cities/{cityId} with valid data, when the city exists, then the system updates the city and returns 200.
4. Given the admin calls PUT /admin/cities/{cityId} for a non-existent city, then the system returns 404.
5. Given the admin calls DELETE /admin/cities/{cityId}, when the city exists, then the system hard-deletes the city, all its weather data, and all subscriptions, and returns 204.
6. Given the admin calls DELETE /admin/cities/{cityId} for a non-existent city, then the system returns 404.
7. Given the admin creates a city via POST /admin/cities, then no UserCity subscription is created (the city is just added to the catalog).
8. Given the admin hard-deletes a city, when the city and its subscriptions are deleted, then affected users are not notified in v1. Notification of affected users is a v2 consideration.

Data Requirements:
- POST /admin/cities: name (string, required), country (string, required), countryCode (string, required), timeZone (string, required), latitude (decimal, required, -90 to 90), longitude (decimal, required, -180 to 180), elevation (decimal?, optional), externalId (long?, optional)
- PUT /admin/cities/{cityId}: same fields as POST, all optional

Notes:
- Technical context: Admin endpoints under /api/core/admin. Requires admin role. No geocoding — admin provides exact coordinates.

### Story 2: Admin - View subscriptions
- Status: Pending
- Ready: Yes
- Ready Reason: Simple query. API contract defined.
- User Story: As an admin user, I want to see which users are subscribed to a city, so that I can understand usage patterns.

Acceptance Criteria:
1. Given the admin calls GET /admin/cities/{cityId}/subscriptions, when the city exists, then the system returns all subscriptions (both active and soft-deleted) for that city.
2. Given the city does not exist, when the admin calls GET /admin/cities/{cityId}/subscriptions, then the system returns 404.
3. Each subscription entry includes: id, userId, cityId, isDeleted, createdAt.

Data Requirements:
- cityId: GUID, required (from URL path)

Notes:
- Technical context: GET /admin/cities/{cityId}/subscriptions. Returns both active and soft-deleted subscriptions for full visibility.

### Story 3: Admin - Trigger ingestion for any city
- Status: Pending
- Ready: Yes
- Ready Reason: Same mechanism as user-facing ingestion, just without subscription check.
- User Story: As an admin user, I want to trigger weather data ingestion for any city, so that I can ensure data freshness.

Acceptance Criteria:
1. Given the admin calls POST /admin/cities/{cityId}/ingest, when the city exists, then the system queues an ingestion job and returns 202.
2. Given the city does not exist, when the admin calls POST /admin/cities/{cityId}/ingest, then the system returns 404.
3. Given the admin triggers ingestion for a city they are not subscribed to, then the system still queues the ingestion (no subscription check for admins).

Data Requirements:
- cityId: GUID, required (from URL path)

Notes:
- Technical context: POST /admin/cities/{cityId}/ingest. Same queue mechanism as user-facing ingestion, but no subscription verification.

### Story 4: Admin - List all cities
- Status: Pending
- Ready: Yes
- Ready Reason: Simple query. API contract defined.
- User Story: As an admin user, I want to list all global cities in the system, so that I can see the full city catalog.

Acceptance Criteria:
1. Given the admin calls GET /admin/cities, then the system returns all cities in the database with their subscription counts.
2. Each city entry includes: id, name, country, countryCode, timeZone, location, externalId, subscriptionCount, createdAt.

Data Requirements:
- No input parameters. Returns all cities.

Notes:
- Technical context: GET /admin/cities. Includes subscriptionCount for each city.

## Non-Functional Notes
- Security: All admin endpoints require admin role. Regular users receive 403 Forbidden.
- Audit: Admin operations (create, update, delete cities) should be logged with admin user ID and timestamp.

## Open Questions
- None
