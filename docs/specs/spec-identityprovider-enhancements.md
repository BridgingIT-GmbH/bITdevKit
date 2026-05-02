---
status: draft
---

# Design Specification: Fake Identity Provider Enhancements

> This design document outlines planned enhancements to the fake OAuth2/OpenID Connect identity provider for development and testing scenarios.

[TOC]

## 1. Introduction / Overview

The fake identity provider (`FakeIdentityProvider`) is a development-time OAuth2/OpenID Connect server used across the devkit for local authentication testing. It currently supports the authorization code flow, client credentials, token issuance, and basic user management.

This specification covers five planned enhancements that improve the developer experience when working with authentication scenarios:

1. **User Switching** — swap active user without logout/login cycle
2. **Token Introspection / Decode** — inspect issued JWTs for debugging
3. **Custom Claims Configuration** — per-user and per-client claim injection
4. **Consent Simulation** — toggleable scope consent screen
5. **Session Management Dashboard** — API + Blazor UI for session visibility

All features are **opt-in or additive** — existing configurations continue to work unchanged.

---

## 2. Feature 1: User Switching (Impersonation)

### Goals

- Eliminate the logout/login cycle when testing multi-user scenarios
- Allow developers to quickly switch context between configured users
- Keep the switch operation lightweight (no password re-entry)

### Design

User switching reuses the existing authorization code flow internally. When a switch is requested, the IDP:

1. Validates the caller has an existing authenticated session (valid auth cookie)
2. Looks up the target user by `user_id` or `email`
3. Generates a new authorization code for the target user
4. Redirects to the provided `redirect_uri` with the new code

The client application exchanges the code for tokens as normal. The previous session remains valid until its natural expiry or explicit revocation.

> **CSRF Protection**: The `state` parameter from the original OAuth request provides sufficient CSRF protection for the switch endpoint.

### API

```
POST /api/_system/identity/connect/switch-user
Content-Type: application/x-www-form-urlencoded

user_id=<target-user-id>
  — or —
email=<target-user-email>

redirect_uri=<callback-url>
state=<optional-csrf-state>
```

**Responses:**

| Status | Condition |
|--------|-----------|
| 302 Redirect | Success — redirects to `redirect_uri?code=<new-code>&state=<state>` |
| 401 Unauthorized | No valid session cookie present |
| 404 Not Found | Target user not found |
| 400 Bad Request | Missing `redirect_uri` or both `user_id` and `email` |

### Builder Configuration

```csharp
builder.AddFakeIdentityProvider(options =>
{
    options.EnableUserSwitching = true; // default: true
});
```

### UI

- "Switch User" button rendered on the IDP dashboard (user selection screen)
- Dropdown or list of configured users with name/email display
- Clicking a user triggers the switch flow
- Current active user highlighted in the list

### Backward Compatibility

- Feature is enabled by default (`true`) — no breaking change
- Endpoint only active when `EnableUserSwitching` is `true`
- Existing flows unaffected

---

## 3. Feature 2: Token Introspection / Decode

### Goals

- Provide a quick way to inspect JWTs issued by the fake IDP
- Help developers debug claim values, expiry, audience, and roles
- Standard OAuth2 introspection endpoint for tooling compatibility

### Design

Two endpoints serve complementary purposes:

1. **Standard Introspection** (`POST /introspect`) — RFC 7662 compliant, returns active/inactive status plus claims
2. **Convenience Decode** (`GET /decode`) — developer-friendly, returns full header + payload + validation notes

Both endpoints accept any token type (access token, ID token, refresh token). Neither requires authentication — this is a development tool.

> **Refresh Token Introspection**: Refresh token introspection returns a reduced claim set compared to access tokens.

### API

#### Standard Introspection

```
POST /api/_system/identity/connect/introspect
Content-Type: application/x-www-form-urlencoded

token=<jwt-token>
token_type_hint=access_token|refresh_token|id_token  (optional)
```

**Response (active token):**

```json
{
  "active": true,
  "sub": "user-123",
  "client_id": "my-app",
  "scope": "openid profile email",
  "exp": 1714500000,
  "iat": 1714496400,
  "iss": "https://localhost:5001",
  "aud": "my-api"
}
```

**Response (expired/invalid token):**

```json
{
  "active": false
}
```

#### Convenience Decode

```
GET /api/_system/identity/connect/decode?token=<jwt-token>
```

**Response:**

```json
{
  "header": {
    "alg": "RS256",
    "typ": "JWT",
    "kid": "..."
  },
  "payload": {
    "sub": "user-123",
    "name": "Alice",
    "email": "alice@example.com",
    "roles": ["admin"],
    "scope": "openid profile",
    "exp": 1714500000,
    "iss": "https://localhost:5001",
    "aud": "my-api"
  },
  "validation": {
    "is_expired": false,
    "signature_valid": true,
    "issuer_match": true,
    "audience_match": true,
    "time_until_expiry": "59m 30s"
  }
}
```

### Builder Configuration

No additional builder option required — endpoints are always available when the fake IDP is registered. They are dev-only tools with no production risk.

### Backward Compatibility

- Additive — new endpoints only
- No changes to existing token issuance

---

## 4. Feature 3: Custom Claims Configuration

### Goals

- Allow per-user custom claims (e.g., `tenant_id`, `department`, `employee_type`)
- Allow per-client custom claims (e.g., `client_environment`, `feature_flags`)
- Claims flow into both access tokens and ID tokens automatically

### Design

Custom claims are merged during token generation using a layered approach:

```
final_claims = base_claims + user_custom_claims + client_custom_claims
```

Client claims override user claims if the same key exists (client takes precedence). This is intentional — client claims represent application-level overrides.

### Data Model Changes

#### FakeUser Extension

```csharp
public class FakeUser
{
    // ... existing properties ...

    /// <summary>
    /// Custom claims injected into tokens for this user.
    /// </summary>
    public Dictionary<string, object> CustomClaims { get; set; } = new();
}
```

#### FakeIdentityProviderClient Extension

```csharp
public class FakeIdentityProviderClient
{
    // ... existing properties ...

    /// <summary>
    /// Custom claims injected into tokens for this client.
    /// </summary>
    public Dictionary<string, object> ClientClaims { get; set; } = new();
}
```

### Builder API

```csharp
builder.AddFakeIdentityProvider(options =>
{
    options.AddUser(user =>
    {
        user.Email = "alice@example.com";
        user.Name = "Alice";
        user.WithClaim("tenant_id", "acme");
        user.WithClaim("department", "engineering");
        user.WithClaim("employee_type", "full-time");
    });

    options.AddClient(client =>
    {
        client.ClientId = "my-app";
        client.WithClaim("client_environment", "development");
        client.WithClaim("feature_flags", "new-dashboard,beta-api");
    });
});
```

### Token Generation Behavior

- Claims appear in access tokens under the configured claim types
- Claims appear in ID tokens alongside standard OIDC claims
- No claim filtering by scope (all custom claims always included — dev tool simplicity)
- Complex claim values (arrays, objects) serialized as JSON

> **Serialization**: Complex claim values (nested objects, arrays) are serialized using standard JSON within the JWT payload.

### Debug Endpoint

```
GET /api/_system/identity/connect/debug/claims?user_id=<id>&client_id=<id>
```

Returns the fully resolved claim set for a given user+client combination, useful for verifying claim merge behavior.

### Backward Compatibility

- `CustomClaims` defaults to empty dictionary — no change to existing users
- `ClientClaims` defaults to empty dictionary — no change to existing clients
- Token generation additive — existing claims unaffected

---

## 5. Feature 4: Consent Simulation

### Goals

- Simulate the OAuth2 consent screen that real identity providers show
- Allow developers to test scope-restricted token scenarios
- Provide toggleable control over which scopes are granted

### Design

When consent is enabled, the authorization flow gains an intermediate step:

```
User Selection → Consent Screen → Authorization Code → Token Exchange
```

The consent screen displays all requested scopes with checkboxes. The developer toggles which scopes to grant. The consent result is stored alongside the authorization code and respected during token issuance.

> **Consent Persistence**: Consent decisions are persisted per user+client combination. On subsequent authorization requests for the same client, previously consented scopes are pre-selected.

### Flow

1. Client redirects user to IDP authorization endpoint with `scope=openid profile email api.read`
2. IDP shows user selection (existing)
3. IDP shows consent screen listing: `openid` (locked), `profile` (toggleable), `email` (toggleable), `api.read` (toggleable)
4. Developer toggles scopes and confirms
5. Authorization code generated with consented scopes stored
6. Token endpoint issues tokens with only consented scopes
7. If all toggleable scopes denied: return `invalid_scope` error or issue token with minimal scope

### API

No new API endpoints — the consent screen is part of the existing authorization flow UI.

The consent data is stored in the authorization code entry (in-memory) and consumed by the token endpoint.

### Scope Rules

| Scope | Behavior |
|-------|----------|
| `openid` | Always granted, non-deselectable (required for OIDC) |
| `profile` | Toggleable, default: selected |
| `email` | Toggleable, default: selected |
| `offline_access` | Toggleable, default: selected (if requested) |
| Custom scopes | Toggleable, default: selected |

### Builder Configuration

```csharp
builder.AddFakeIdentityProvider(options =>
{
    options.EnableConsentScreen = false; // default: false (backward compatibility)
});
```

### UI

- Simple HTML form with checkboxes for each requested scope
- Scope descriptions pulled from a configurable dictionary (or scope name as fallback)
- "Allow" and "Deny" buttons
- "Deny All" returns error to client
- Clean, minimal design consistent with IDP dashboard style

### Backward Compatibility

- Disabled by default (`false`) — existing flows unchanged
- When disabled, all requested scopes are granted automatically (current behavior)

---

## 6. Feature 5: Session Management Dashboard (API-First)

### Goals

- Provide visibility into active authentication sessions
- Allow developers to revoke individual sessions or all sessions
- API-first design enabling Blazor dashboard integration
- Support multi-client session debugging
- Allows token introspection and debugging of active sessions
- Provides ab API (endpoints) that can be used by the Blazor dashboard for session management

### Design

Sessions are tracked in-memory within the fake IDP. A session is created when a user completes the sign-in flow (and persistent refresh tokens are enabled). Sessions are invalidated on cookie expiry, explicit revocation, or sign-out.

### API Endpoints

All endpoints under `/api/_system/identity/connect/sessions`.

#### List Active Sessions

```
GET /api/_system/identity/connect/sessions
```

**Response:**

```json
[
  {
    "session_id": "a1b2c3d4-...",
    "user_id": "user-123",
    "user_email": "alice@example.com",
    "user_name": "Alice",
    "created_at": "2026-04-30T10:00:00Z",
    "expires_at": "2026-04-30T22:00:00Z",
    "client_id": "my-app",
    "ip_address": "127.0.0.1",
    "is_current": true
  }
]
```

#### Get Session Details

```
GET /api/_system/identity/connect/sessions/{session_id}
```

**Response:**

```json
{
  "session_id": "a1b2c3d4-...",
  "user_id": "user-123",
  "user_email": "alice@example.com",
  "user_name": "Alice",
  "created_at": "2026-04-30T10:00:00Z",
  "expires_at": "2026-04-30T22:00:00Z",
  "client_id": "my-app",
  "ip_address": "127.0.0.1",
  "is_current": true,
  "tokens": {
    "access_token_expires_at": "2026-04-30T11:00:00Z",
    "refresh_token_expires_at": "2026-04-30T22:00:00Z",
    "consented_scopes": ["openid", "profile", "email"]
  }
}
```

#### Revoke Session

```
DELETE /api/_system/identity/connect/sessions/{session_id}
```

**Response:** `204 No Content` on success, `404 Not Found` if session doesn't exist.

#### Revoke All Sessions

```
DELETE /api/_system/identity/connect/sessions
```

**Response:** `204 No Content`. Revokes all sessions for the current authenticated user.

> **Cross-Client Revocation**: `DELETE /sessions` revokes all sessions for the authenticated user across all clients.

> **Refresh Token Invalidation**: When a session is revoked, all associated refresh tokens are immediately invalidated.

### Builder Configuration

```csharp
builder.AddFakeIdentityProvider(options =>
{
    options.EnableSessionManagementEndpoints = false; // default: false
});

// — or via endpoint registration —

app.AddEndpoints(options =>
{
    options.EnableSessionManagementEndpoints();
});
```

### Backend Storage

```csharp
internal class FakeSession
{
    public Guid SessionId { get; init; }
    public string UserId { get; init; }
    public string ClientId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public Dictionary<string, object> PrincipalClaims { get; init; }
    public string IpAddress { get; init; }
}
```

- Stored in a `ConcurrentDictionary<Guid, FakeSession>` within the IDP service
- Created during `SignInUserAsync` when `EnablePersistentRefreshTokens` is `true`
- Invalidated on: cookie expiry, explicit `DELETE`, `SignOutAsync` on logout endpoint
- Expired sessions cleaned up on-demand during read operations (lazy cleanup)

### Blazor Dashboard Page

Reference implementation following the pattern in `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsMessages.razor`.

**Features:**

- Table of active sessions with columns: User, Client, Created, Expires, Actions
- Per-row "Revoke Session" button
- Top-level "Revoke All Sessions" button
- Auto-refresh interval selector (off, 5s, 10s, 30s)
- Uses Kiota-generated client to call session endpoints
- Empty state message when no active sessions

### Backward Compatibility

- Disabled by default (`false`) — no new endpoints unless opted in
- Session tracking only active when `EnablePersistentRefreshTokens` is `true`
- No impact on existing token flows

---

## 7. Cross-Cutting Concerns

### Shared Types

New types introduced across features:

| Type | Used By |
|------|---------|
| `SessionInfo` | Feature 5 (list sessions response) |
| `SessionDetail` | Feature 5 (session detail response) |
| `TokenIntrospectionResponse` | Feature 2 (introspect response) |
| `TokenDecodeResponse` | Feature 2 (decode response) |
| `ConsentEntry` | Feature 4 (scope consent data) |

### Builder Changes

All new options are additive to the existing `FakeIdentityProviderOptions`:

```csharp
public class FakeIdentityProviderOptions
{
    // Existing options unchanged ...

    // Feature 1
    public bool EnableUserSwitching { get; set; } = true;

    // Feature 4
    public bool EnableConsentScreen { get; set; } = false;

    // Feature 5
    public bool EnableSessionManagementEndpoints { get; set; } = false;
}
```

### Discovery Document

When features are enabled, the discovery document (`/.well-known/openid-configuration`) is extended:

- Feature 2: `introspection_endpoint` claim added
- Feature 1: Custom `switch_user_endpoint` claim (non-standard, dev convenience)
- Custom endpoints (`switch-user`, `decode`) are advertised in the discovery document when their respective features are enabled.

### Backward Compatibility Summary

| Feature | Default | Breaking? |
|---------|---------|-----------|
| User Switching | Enabled | No — additive endpoint |
| Token Introspection | Always available | No — additive endpoint |
| Custom Claims | Empty dictionaries | No — additive data |
| Consent Screen | Disabled | No — opt-in only |
| Session Management | Disabled | No — opt-in only |

---

## 8. Testing Strategy

### Unit Tests

- **User Switching**: validate redirect generation, user lookup, error cases (missing user, no session)
- **Token Introspection**: validate active/inactive responses, expired token detection, signature validation
- **Custom Claims**: validate claim merge order, client override behavior, empty claims handling
- **Consent Screen**: validate scope filtering, `openid` lock, deny-all behavior
- **Session Management**: validate CRUD operations, expiry cleanup, concurrent access

### Integration Tests

- Full authorization code flow with user switching mid-session
- Token introspection against freshly issued tokens
- Custom claims visible in decoded tokens
- Consent flow with partial scope grant — verify token contains only consented scopes
- Session lifecycle: create → list → revoke → verify 404

### Test Scenarios

| Scenario | Feature | Type |
|----------|---------|------|
| Switch user, exchange code, verify new identity | 1 | Integration |
| Introspect valid token, verify `active: true` | 2 | Unit |
| Introspect expired token, verify `active: false` | 2 | Unit |
| User with custom claims, verify in access token | 3 | Integration |
| Client claims override user claims | 3 | Unit |
| Consent grants subset of scopes | 4 | Integration |
| Revoke session, verify token invalidation | 5 | Integration |
| List sessions, verify correct count | 5 | Unit |

---

## 9. Open Questions

All open questions have been resolved during specification:

1. **User Switching — CSRF protection**: The `state` parameter is sufficient for CSRF protection.
2. **Token Introspection — Refresh tokens**: Returns a reduced claim set.
3. **Custom Claims — Serialization**: Standard JSON serialization for complex values.
4. **Consent Screen — Persistence**: Consent decisions remembered per user+client.
5. **Session Management — Scope**: `DELETE /sessions` revokes across all clients.
6. **Session Management — Refresh token binding**: Immediate invalidation on session revoke.
7. **Discovery Document**: Custom endpoints are advertised.
