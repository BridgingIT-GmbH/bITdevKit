# Presentation Authentication

> Authenticate ASP.NET Core requests with JWT bearer tokens and expose the current principal through a devkit abstraction.

[TOC]

## Overview

Presentation authentication connects an ASP.NET Core host to an OpenID Connect authority through JWT bearer authentication. It also provides optional cookie-scheme registration, role and policy attributes, and `HttpCurrentUserAccessor` for application code that needs the current user.

The feature validates tokens and creates the request principal. It does not issue production tokens, manage users, define application permissions, or choose authorization policies for the host.

## Challenges

Every API host needs consistent token-validation settings and middleware ordering. Missing issuer, audience, lifetime, or signature checks can let an invalid token reach protected endpoints.

Application handlers should not depend on `HttpContext`. They still need a stable way to read the authenticated user ID, name, email, roles, and claims principal.

Role and policy requirements also need to work across endpoint classes without coupling those classes to a specific identity provider.

## Solution

`AddJwtBearerAuthentication(...)` configures the ASP.NET Core JWT bearer handler as the default authentication and challenge scheme. It reads `AuthenticationOptions` directly or binds the `Authentication` configuration section.

`HttpCurrentUserAccessor` adapts `HttpContext.User` to the common `ICurrentUserAccessor` contract. Application services can depend on that contract without referencing Presentation.Web.

`AuthorizeRolesAttribute` supplies role metadata. `AuthorizePermissionAttribute` sets an ASP.NET Core policy name, which the host must register through its authorization configuration.

## Key Features

- Configuration-based or object-based JWT bearer registration
- OpenID Connect authority metadata and signing-key discovery
- Issuer, audience, lifetime, signing-key, and signed-token validation options
- JWT bearer event logging for receipt, validation, challenge, and failure
- Optional cookie handler with HTTPS-only and HTTP-only defaults
- `ICurrentUserAccessor` implementation for HTTP requests
- Role and named-policy attributes
- Compatibility with the devkit fake identity provider for local development

## Architecture

ASP.NET Core owns authentication and authorization. The devkit adds registration helpers and one adapter:

1. `AddJwtBearerAuthentication(...)` registers the bearer handler and its `TokenValidationParameters`.
2. `UseAuthentication()` validates the request credential and sets `HttpContext.User`.
3. `UseAuthorization()` evaluates endpoint metadata against that principal.
4. `HttpCurrentUserAccessor` reads the principal through `IHttpContextAccessor`.
5. Application handlers consume `ICurrentUserAccessor` from Common.Abstractions.

Authentication proves which principal made the request. Authorization decides whether that principal can perform the requested operation. Keep those decisions separate.

## Use Cases

Use Presentation authentication when an API accepts bearer tokens issued by an OpenID Connect provider. The provider can be an external identity service or the devkit [Fake Identity Provider](./features-identityprovider.md) in development.

Add the cookie handler when a component explicitly signs in through the ASP.NET Core cookie scheme, such as a persistent refresh-token flow. Adding the handler does not change the default scheme from JWT bearer.

Use [Entity Permissions](./features-entitypermissions.md) when authorization depends on an entity and a permission such as read, write, or delete. Use ordinary ASP.NET Core policies for claims-based rules that do not need entity state.

Use [Fake Authentication](./testing-fake-authentication.md) in tests that only need a known principal and do not need a token issuer.

## Basic Usage

Configure the authority and enable the validation checks used by the API:

```json
{
	"Authentication": {
		"Authority": "https://identity.example.com",
		"ValidIssuer": "https://identity.example.com",
		"ValidAudience": "inventory-api",
		"ValidateIssuer": true,
		"ValidateAudience": true,
		"ValidateLifetime": true,
		"ValidateSigningKey": true,
		"RequireSignedTokens": true,
		"RequireHttpsMetadata": true,
		"SaveToken": false
	}
}
```

Register authentication, authorization, and the current-user adapter:

```csharp
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/me", (ICurrentUserAccessor currentUser) => Results.Ok(new
{
	currentUser.UserId,
	currentUser.UserName,
	currentUser.Email,
	currentUser.Roles
})).RequireAuthorization();

app.Run();
```

A request without a bearer token receives `401 Unauthorized`. A request with a valid token receives the user values that `HttpCurrentUserAccessor` found in the claims principal.

## JWT configuration

The `Authentication` configuration section maps to `AuthenticationOptions`:

| Option | Runtime effect |
| --- | --- |
| `Authority` | Sets `JwtBearerOptions.Authority`. This value is required by `AddJwtBearerAuthentication(...)`. |
| `ValidIssuer` | Sets the accepted issuer when `ValidateIssuer` is enabled. |
| `ValidateIssuer` | Enables issuer validation. The default is `false`. |
| `ValidAudience` | Sets the accepted audience when `ValidateAudience` is enabled. |
| `ValidateAudience` | Enables audience validation. The default is `false`. |
| `ValidateLifetime` | Enables token lifetime validation. The default is `false`. |
| `RequireHttpsMetadata` | Requires HTTPS for authority metadata. The default is `false`. |
| `SaveToken` | Stores the bearer token in authentication properties. The default is `false`. |
| `SigningKey` | Supplies a symmetric signing key. Omit it when authority metadata supplies the signing keys. |
| `ValidateSigningKey` | Enables issuer signing-key validation. The default is `false`. |
| `RequireSignedTokens` | Rejects unsigned tokens. The default is `false`. |

The current implementation uses a fixed five-minute `ClockSkew` in `TokenValidationParameters`. The `AuthenticationOptions.ClockSkew` property is not applied.

Most validation flags default to `false`. Set the required checks explicitly. Production hosts should normally validate the issuer, the audience, the lifetime, and the signing key, require signed tokens, and require HTTPS metadata.

`SigningKey` is passed to `SymmetricSecurityKey` as UTF-8 text. It is not Base64-decoded. Keep symmetric keys in a secret store, not in a committed settings file.

## Current user access

`HttpCurrentUserAccessor` returns these values:

| Property | Claim source |
| --- | --- |
| `Principal` | `HttpContext.User`, or an empty `ClaimsPrincipal` outside a request |
| `IsAuthenticated` | `HttpContext.User.Identity.IsAuthenticated` |
| `UserId` | `ClaimTypes.NameIdentifier` |
| `UserName` | `ClaimTypes.Name`, then the literal `name` claim |
| `Email` | `ClaimTypes.Email` |
| `Roles` | All `ClaimTypes.Role` values |

Missing claims return `null` or an empty role array. The accessor does not infer a user ID from another claim name. Configure the identity provider or claims mapping so the principal contains the claims that the application expects.

Application code should depend on `ICurrentUserAccessor`, not `HttpCurrentUserAccessor` or `IHttpContextAccessor`. This keeps the application layer independent of ASP.NET Core and lets tests provide a small substitute.

## Role and policy metadata

`AuthorizeRolesAttribute` joins its role names with commas. ASP.NET Core treats the roles in one attribute as alternatives:

```csharp
[AuthorizeRoles("Administrators", "Operators")]
public sealed class OperationsEndpoints : EndpointsBase
{
	// Either role satisfies this attribute.
}
```

`AuthorizePermissionAttribute` uses its string as the policy name:

```csharp
builder.Services.AddAuthorization(options =>
	options.AddPolicy("inventory.write", policy =>
		policy.RequireClaim("permission", "inventory.write")));

[AuthorizePermission("inventory.write")]
public sealed class InventoryEndpoints : EndpointsBase
{
}
```

Register every named policy before the application starts. `AuthorizePermissionAttribute` does not create the policy or evaluate the claim by itself.

## Cookie handler

Add the cookie handler to the authentication builder:

```csharp
builder.Services
	.AddJwtBearerAuthentication(builder.Configuration)
	.AddCookieAuthentication(options =>
	{
		options.Cookie.Name = ".Inventory.Refresh";
		options.Cookie.HttpOnly = true;
		options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
		options.Cookie.SameSite = SameSiteMode.Strict;
		options.ExpireTimeSpan = TimeSpan.FromDays(7);
	});
```

Without a callback, the extension uses cookie name `.AspNetCore.Identity`, enables `HttpOnly`, requires HTTPS, and sets a 30-day expiration. It does not set a `SameSite` value or configure antiforgery protection for the host.

The JWT bearer scheme remains the default authentication, challenge, sign-in, sign-out, and forbid scheme. Code that uses the cookie handler should pass `CookieAuthenticationDefaults.AuthenticationScheme` explicitly to the relevant authentication operation.

## Middleware order

Call `UseAuthentication()` before `UseAuthorization()`. Both must run before protected endpoints execute:

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();
```

If authorization always sees an anonymous principal, first verify this order. Then check the authority, audience, issuer, signing keys, token lifetime, and claim mapping.

## Related documentation

- [Fake Identity Provider](./features-identityprovider.md) issues development tokens.
- [Entity Permissions](./features-entitypermissions.md) adds entity-aware authorization.
- [Presentation Endpoints](./features-presentation-endpoints.md) covers endpoint authorization options.
- [Requester and Notifier](./features-requester-notifier.md) covers handler-level authorization behaviors.
- [Fake Authentication](./testing-fake-authentication.md) covers test principals without token validation.
