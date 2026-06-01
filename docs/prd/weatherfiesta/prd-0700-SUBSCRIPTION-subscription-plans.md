---
id: PRD-0700
title: Subscription Plans
slice: SUBSCRIPTION
status: Implemented
---

# Product Requirements: Subscription Plans

## Overview
All users must have an active subscription to use WeatherFiesta. New users are automatically assigned the Free plan on first login. Admins can upgrade or downgrade user subscriptions. Each plan defines usage limits (max cities, forecast days, export access, comparison access).

## Scope
- In scope: Subscription model, plan tiers, auto-assignment on first login, admin subscription management, plan-based feature gating
- Out of scope: Payment processing, billing cycles, invoicing (v2 consideration)

## User Roles
- **User**: Authenticated user with an active subscription (default: Free)
- **Admin**: Can view and modify any user's subscription

## Subscription Plans

| Plan | Max Cities | Forecast Days | Comparison | Export | Price |
|------|-----------|---------------|------------|--------|-------|
| Free | 3 | 7 | No | No | $0/mo |
| Basic | 10 | 16 | Yes | Yes | $4.99/mo |
| Pro | 25 | 16 | Yes | Yes | $9.99/mo |
| Enterprise | Unlimited | 16 | Yes | Yes | $29.99/mo |

## Stories

### Story 1: Auto-assign Free plan on first login
- Status: Implemented
- Ready: Yes
- Ready Reason: UserProfile already created on first login. Subscription creation is additive.
- User Story: As a new user, when I first log in, I want to be automatically assigned the Free plan, so I can start using the app immediately.

Acceptance Criteria:
1. Given a user authenticates for the first time, when no Subscription exists for that UserId, then the system creates a Subscription with Plan=Free, Status=Active, BillingCycle=Never, StartDate=UtcNow.
2. Given a user authenticates and already has a Subscription, when the subscription is Active, then no new subscription is created.
3. Given a user authenticates and has a Cancelled/Expired subscription, when they log in, then the system does NOT auto-create a new subscription (admin must reactivate).

Data Requirements:
- UserId: resolved from authentication context
- Plan: Free (default)
- Status: Active (default)
- BillingCycle: Never (default for Free)
- StartDate: DateTime.UtcNow

Notes:
- Technical context: This happens in the UserProfileQuery handler or a domain event handler. When a UserProfile is first created, a Subscription is also created.
- The Subscription.UserId matches the UserProfile.Id (same GUID).

### Story 2: View current subscription
- Status: Implemented
- Ready: Yes
- Ready Reason: Simple query. Data model defined.
- User Story: As an authenticated user, I want to view my current subscription plan, so I know what features I have access to.

Acceptance Criteria:
1. Given the user is authenticated, when they call GET /api/core/users/subscription, then the system returns their current subscription including plan, status, billingCycle, startDate, endDate, and plan details (maxCities, maxForecastDays, allowsComparison, allowsExport).
2. Given the user is not authenticated, when they call GET /api/core/users/subscription, then the system returns 401.
3. The response includes an `isActive` flag computed from Status and EndDate.

Data Requirements:
- No input parameters. UserId resolved from authentication context.

Notes:
- Technical context: GET /api/core/users/subscription. Returns the full subscription with plan details.

### Story 3: Admin - Manage user subscriptions
- Status: Implemented
- Ready: Yes
- Ready Reason: Admin CRUD pattern. Same as admin city management.
- User Story: As an admin user, I want to view and modify any user's subscription, so I can upgrade, downgrade, or cancel plans.

Acceptance Criteria:
1. Given the admin calls GET /api/core/admin/subscriptions, then the system returns all subscriptions with user info and plan details.
2. Given the admin calls GET /api/core/admin/subscriptions/{userId}, when the user exists, then the system returns that user's subscription.
3. Given the admin calls PUT /api/core/admin/subscriptions/{userId} with { plan, status, billingCycle, endDate }, when the user exists, then the system updates the subscription and returns 200.
4. Given the admin calls PUT /api/core/admin/subscriptions/{userId} with an invalid plan, then the system returns 400.
5. Given the admin changes a user's plan from Free to Basic, when the user has more than 10 cities subscribed, then the system does NOT remove cities — the user keeps their subscriptions but cannot add more until they reduce below the limit.

Data Requirements:
- PUT /admin/subscriptions/{userId}: plan (enum), status (enum), billingCycle (enum), endDate (datetime?, optional)

Notes:
- Technical context: Admin endpoints under /api/core/admin. Requires admin role.
- Design decision: Downgrading does NOT remove data. The user keeps existing subscriptions but cannot add new ones beyond the plan limit.

### Story 4: Plan-based feature gating
- Status: Implemented
- Ready: Yes
- Ready Reason: Plan limits are checked at the handler level. Simple comparison logic.
- User Story: As a user on the Free plan, I want to be prevented from exceeding my plan limits, so I understand what features are available to me.

Acceptance Criteria:
1. Given a Free user with 3 cities subscribed, when they try to subscribe to a 4th city, then the system returns 403 with message "Free plan allows a maximum of 3 cities. Upgrade to Basic for up to 10 cities."
2. Given a Free user, when they call GET /api/core/cities/compare, then the system returns 403 with message "Comparison is not available on the Free plan. Upgrade to Basic."
3. Given a Free user, when they call GET /api/core/cities/export, then the system returns 403 with message "Export is not available on the Free plan. Upgrade to Basic."
4. Given a Free user, when they request forecast with days > 7, then the system caps the forecast days at 7 and includes a warning "Free plan allows a maximum of 7 forecast days. Upgrade to Basic for 16-day forecasts."
5. Given a Basic/Pro/Enterprise user, when they use any feature within their plan limits, then the system processes the request normally.

Data Requirements:
- Plan limits are defined in the SubscriptionPlan enumeration (MaxCities, MaxForecastDays, AllowsComparison, AllowsExport).
- Feature checks happen in command/query handlers before processing.

Notes:
- Technical context: Feature gating is implemented as checks in the handler layer. The user's subscription is loaded and the plan's limits are checked before processing the request.
- Design decision: Soft limits — existing data is never removed on downgrade. The user just can't add more.

## Non-Functional Notes
- Performance: Subscription lookup should be cached per request (loaded once, reused across handlers).
- Security: Only admins can modify subscriptions. Users can only view their own.
- Data integrity: Each user has exactly one Subscription. UserId is unique on the Subscriptions table.

## Open Questions
- None
