---
id: PRD-0001
title: City Reorder
slice: CITIES
status: Pending
---

# Product Requirements: City Reorder

## Overview
Authenticated users can reorder their subscribed cities to control the display order on their dashboard and city list. The primary city is always first, but users can arrange the remaining cities by personal preference. This makes the app feel personal and reduces friction when switching between frequently checked cities.

## Scope
- In scope: Reorder subscribed cities, display order persistence, order interaction with primary city
- Out of scope: City subscription management (PRD-0000), weather data viewing (PRD-0100), dashboard summary (PRD-0500)

## User Roles
- **User**: Authenticated user who reorders their subscribed cities

## Stories

### Story 1: Reorder subscribed cities
- Status: Pending
- Ready: Yes
- Ready Reason: Simple ordering field on UserCity. Business rules for primary city interaction defined.
- User Story: As an authenticated user, I want to reorder my subscribed cities, so that they appear in my preferred order on the dashboard and city list.

Acceptance Criteria:
1. Given the user is authenticated and has 2 or more subscribed cities, when they call PUT /cities/reorder with an array of city IDs in the desired order, then the system updates the DisplayOrder field on each UserCity record and returns 200.
2. Given the user is authenticated and has fewer than 2 subscribed cities, when they call PUT /cities/reorder, then the system returns 400 with message "At least 2 subscribed cities are required to reorder."
3. When the request includes a city ID the user is not subscribed to, then the system returns 400 with message "City {cityId} is not in your subscriptions."
4. When the request omits a city ID that the user is subscribed to, then the system returns 400 with message "All subscribed cities must be included in the reorder request."
5. When the request includes duplicate city IDs, then the system returns 400 with message "Duplicate city IDs are not allowed."

Data Requirements:
- cityIds: array of GUIDs, required, must match exactly the user's active subscribed cities, order determines DisplayOrder (0-based)

Notes:
- Technical context: PUT /cities/reorder. Updates DisplayOrder field on UserCity records. The array index becomes the DisplayOrder value.
- Design decision: Full-list replacement semantics — the client sends the complete ordered list, not incremental moves. This avoids conflicts and keeps the API simple.

### Story 2: Primary city and display order interaction
- Status: Pending
- Ready: Yes
- Ready Reason: Interaction rules are clear and testable. Primary city behavior already defined in PRD-0000 Story 5.
- User Story: As an authenticated user, I want the primary city to always appear first regardless of display order, so that my most important city is never buried in the list.

Acceptance Criteria:
1. Given the user has a primary city set, when they call GET /cities, then the primary city always appears first in the list, regardless of its DisplayOrder value.
2. Given the user has a primary city set, when they call PUT /cities/reorder, then the primary city is automatically moved to position 0 (first) and the remaining cities follow in the specified order.
3. Given the user sets a new primary city via PUT /cities/{cityId}/primary, when the primary changes, then the new primary city moves to position 0 and the previous primary city takes the next available position.
4. Given the user has no primary city set, when they call GET /cities, then cities are returned in DisplayOrder.

Data Requirements:
- No additional data. Primary city resolved from UserCity.IsPrimary.

Notes:
- Technical context: Primary city is always first. DisplayOrder determines the order of non-primary cities. GET /cities sorts by (IsPrimary DESC, DisplayOrder ASC).
- Dependencies: PRD-0000 Story 5 (set primary city)

### Story 3: Default display order
- Status: Pending
- Ready: Yes
- Ready Reason: Simple default behavior. No external dependencies.
- User Story: As an authenticated user, I want my cities to have a sensible default order before I manually reorder them, so that the list is organized from the start.

Acceptance Criteria:
1. Given a user subscribes to their first city, when the UserCity record is created, then DisplayOrder is set to 0.
2. Given a user subscribes to an additional city, when the UserCity record is created, then DisplayOrder is set to the current maximum DisplayOrder + 1 (appended to the end of the list).
3. Given a user reactivates a previously soft-deleted subscription, when the UserCity record is reactivated, then DisplayOrder is set to the current maximum DisplayOrder + 1 (appended to the end of the list).
4. Given a user unsubscribes from a city, when the UserCity record is soft-deleted, then the DisplayOrder values of the remaining cities are recalculated to close the gap (e.g., 0, 2, 3 becomes 0, 1, 2).

Data Requirements:
- DisplayOrder: int, non-negative, default calculated on creation

Notes:
- Technical context: DisplayOrder is a simple integer on UserCity. Gaps are closed on soft-delete to keep ordering contiguous. This prevents ordering drift over time.
- Design decision: Append-to-end on new subscription is the least surprising default. Users can then reorder.

## Non-Functional Notes
- Performance: Reorder operation should complete within 200ms for up to 50 cities
- Security: Users can only reorder their own subscribed cities

## Open Questions
- None
