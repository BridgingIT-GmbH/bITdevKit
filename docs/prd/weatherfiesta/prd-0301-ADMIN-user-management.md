---
id: PRD-0301
title: Admin User Management
slice: ADMIN
status: Implemented
---

# Product Requirements: Admin User Management

## Overview
Admin users can remove a WeatherFiesta user and their associated WeatherFiesta data when support or compliance workflows require a hard delete.

## Scope
- In scope: Admin hard-delete of a user profile, city subscriptions, and subscription plan
- Out of scope: User self-service account deletion (PRD-0400), identity-provider account deletion, audit/reporting dashboards

## User Roles
- **Admin**: Elevated user with permission to delete WeatherFiesta user data

## Stories

### Story 1: Admin - Hard-delete user data
- Status: Implemented
- Ready: Yes
- Ready Reason: Implemented API route, command, authorization boundary, and deletion scope are defined.
- User Story: As an admin user, I want to hard-delete a user's WeatherFiesta data, so that support and compliance requests can be completed.

Acceptance Criteria:
1. Given the admin calls DELETE /admin/users/{userId} with a valid existing user ID, when the request is processed, then the system deletes the user's profile, city subscriptions, and subscription plan and returns 204.
2. Given the admin calls DELETE /admin/users/{userId} for a user that does not exist, when the request is processed, then the system returns an error indicating the user profile was not found.
3. Given a non-admin user calls DELETE /admin/users/{userId}, when authorization is evaluated, then the system rejects the request with 403.
4. Given an unauthenticated caller uses the endpoint, when authorization is evaluated, then the system rejects the request with 401.

Data Requirements:
- userId: GUID string, required, supplied in the route path.
- Deleted data: UserProfile, all UserCity records for the user, and all UserSubscription records for the user.

Notes:
- Dependencies / external input: Requires the host authentication setup to assign the CoreAdmin role.
- Risks / constraints: This endpoint deletes WeatherFiesta application data only; deleting the upstream identity-provider account is outside this PRD.
- Technical context: DELETE /api/core/admin/users/{userId}, handled by AdminUserDeleteCommand.

## Non-Functional Notes
- Security: Requires the CoreAdmin role.
- Audit: Admin deletion should be observable through request logs and structured handler logs where available.

## Open Questions
- None