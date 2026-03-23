# Fake Authentication for Integration Tests

[TOC]

## Overview

`Common.Extensions.Web` provides a lightweight fake authentication scheme for integration-test scenarios. It is not a full identity provider or token issuer. Instead, it lets your ASP.NET Core app authenticate requests with a simple header or a configured default fake user.

Use this when you want authenticated ASP.NET Core requests in integration tests without standing up a real OAuth2 or OpenID Connect server.

## Setup

Register the scheme through dependency injection:

```csharp
builder.Services.AddFakeAuthentication(FakeUsers.Starwars);
```

or configure it fluently:

```csharp
builder.Services.AddFakeAuthentication(o => o
    .WithUsers(FakeUsers.Starwars)
    .AddUser("alice@example.com", "Alice", new[] { "Admin" }, isDefault: true)
    .AddClaim("tenant", "test")
    .WithClaims(("culture", "en-US")));
```

Both overloads accept an optional `enabled` flag, which is useful when you want to turn the helper on or off by environment.

The builder requires at least one user and allows only one default user.

## Request Contract

Authenticate a specific fake user by sending this header:

```http
Authorization: FakeUser alice@example.com
```

The handler looks up the user by email. If the header is missing, it falls back to the configured default user when one exists.

## Claims Behavior

When a request is authenticated, the handler creates these baseline claims:

- `ClaimTypes.NameIdentifier`
- `ClaimTypes.Name`
- `ClaimTypes.Email`

It then adds:

- the user’s roles as `ClaimTypes.Role`
- any per-user claims configured on the `FakeUser`
- any global claims configured on `FakeAuthenticationOptionsBuilder`

This makes it easy to simulate both identity and authorization rules in tests.

## Default User Rules

The default-user path is useful for integration tests where you do not want to attach a header to every request.

Behavior to remember:

- no users configured means the builder throws
- more than one default user means the builder throws
- a disabled user fails authentication
- if no header is present and no default user exists, authentication fails

## When To Use It

Use fake authentication when you need authenticated ASP.NET Core requests inside integration tests and the app only needs an authenticated principal plus claims.

Use the fake identity provider in [Fake Identity Provider](./features-identityprovider.md) when you need an actual OAuth2/OpenID Connect flow, token issuance, discovery metadata, or client integration coverage.

This helper accepts a request header such as `Authorization: FakeUser alice@example.com`, but it does not mint bearer tokens by itself.

## Testing Notes

In integration tests, the usual pattern is:

```csharp
builder.ConfigureServices(services =>
{
    services.AddFakeAuthentication(FakeUsers.Starwars);
});
```

If your code reads the current user through `ICurrentUserAccessor`, wire that accessor as part of the test host as well. The fake auth scheme only populates the authenticated principal.
