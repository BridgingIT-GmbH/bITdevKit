# Entity Permissions

> Enforce fine-grained, entity-level authorization with fluent configuration and runtime evaluation.

[TOC]

## Overview

The `Application.Identity` feature manages permissions for entity types and individual entity instances. It supports predefined values such as `Permission.Read`, application-defined values such as `"Review"`, user and role grants, optional hierarchy inheritance, and default permission providers. Entity Framework Core stores grants, while application and presentation components expose evaluation and authorization APIs.

## Challenges

- **Granular Access Control**: Traditional role-based access control is often too coarse when access must be restricted to specific entity instances.
- **Configuration Complexity**: Setting up permissions across multiple entities and endpoints becomes hard to maintain without a central model.
- **Hierarchical Permissions**: Parent-child structures often need inherited access, such as a manager's access flowing to subordinate entities.
- **Runtime Management**: Applications need programmatic and operational ways to grant, revoke, inspect, and validate permissions.
- **Cross-Layer Consistency**: The same permission logic should be reusable in endpoints, application services, and rules without duplicating checks.

## Solution

The `Application.Identity` feature centralizes entity-permission configuration within `AddEntityAuthorization`, supports predefined `Permission` values and custom permission strings, and integrates with ASP.NET Core authorization.

Permissions can be applied at two levels:

- **Type-wide permissions**: wildcard permissions that apply to all instances of an entity type
- **Entity-specific permissions**: permissions granted for a single entity instance identified by an id

Key components include:

- **Entity Permissions**: persisted or defaulted rights for a specific entity type or entity instance
- **Fluent Configuration**: centralized setup through `AddEntityAuthorization(...)` and `WithEntityPermissions<TContext>(...)`
- **Permission Evaluation**: `IEntityPermissionEvaluator<TEntity>` for application-layer permission checks
- **Management Tools**: `IEntityPermissionProvider` plus `EntityPermissionProviderBuilder` for granting and revoking permissions
- **Hierarchy Support**: optional parent inheritance for entities configured through `AddHierarchicalEntity(...)`
- **Rules Integration**: `HasPermissionRule<TEntity>` and `HasNotPermissionRule<TEntity>` for rule-based authorization checks

## Key Features

- User and role grants for an entity type or a specific entity id.
- Built-in and application-defined permission names.
- Entity Framework Core persistence through `IEntityPermissionContext`.
- Optional default providers, hierarchy inheritance, and successful-result caching.
- Evaluator overloads for current-user, explicit-user, entity, entity-id, and type-wide checks.
- ASP.NET Core controller, Minimal API, and optional runtime endpoint integration.
- Rules that return structured authorization failures.

## Architecture

`Application.Identity` defines the permission model, evaluator, providers, and rules. `Infrastructure.EntityFramework` implements persisted grants and hierarchy lookup. `Presentation.Web` connects the evaluator to ASP.NET Core authorization and optional management and evaluation endpoints. `ICurrentUserAccessor` supplies the current user id and roles when a caller does not pass them explicitly.

### Permission evaluation flow diagram

This diagram illustrates the effective permission evaluation process:

```mermaid
graph TD
    A[Request or Application Check] --> B[IEntityPermissionEvaluator<TEntity>]
    B --> C{Cache Enabled?}
    C -->|Hit| D[Cached Permission Result]
    C -->|Miss| E[IEntityPermissionProvider]
    E -->|Direct Grants| F[EntityPermissions Store]
    E -->|Role Grants| F
    E -->|Hierarchy Path| G[Parent Entity Chain]
    B --> H[Default Permission Providers]
    F --> B
    G --> B
    H --> B
    D --> I[Allow or Deny]
    B --> I
```

- **Evaluation Flow Explanation**:
  1. Application code or ASP.NET Core authorization triggers a permission check.
  2. `IEntityPermissionEvaluator<TEntity>` optionally checks the cache first.
  3. If needed, it asks `IEntityPermissionProvider` for direct user and role grants.
  4. If the entity type is configured as hierarchical, parent ids are resolved and checked as inherited permission sources.
  5. Configured default permission providers are evaluated.
  6. The evaluator returns an allow/deny result and may cache successful resolutions.

### Permission granting flow diagram

This diagram depicts how permissions are granted:

```mermaid
graph TD
    L[Grant Request] --> M[IEntityPermissionProvider or Builder]
    M -->|GrantUserPermissionAsync / GrantRolePermissionAsync| N[EntityPermissions Store]
    N --> O[Persist Grant]
    O --> P[Subsequent Evaluation Sees Grant]
```

- **Granting Flow Explanation**:
  1. A grant request is initiated programmatically through `IEntityPermissionProvider` or the fluent `EntityPermissionProviderBuilder`.
  2. User- or role-based grants are persisted for an entity type or a concrete entity id.
  3. Later permission checks resolve those grants directly or via inherited or cached results.

## Use Cases

- Grant a user read access to one employee record.
- Grant a role a type-wide list or administration permission.
- Inherit access from a parent department or organizational node.
- Provide public or module-wide baseline access through a default provider.
- Protect controller actions, Minimal API routes, or application operations with the same evaluator.
- Inspect or administer grants through optional protected runtime endpoints.

## Basic Usage

The following setup gives authenticated users a default read permission for `Employee` and exposes an endpoint that checks a concrete employee id. A successful request returns a visible result; a denied request returns HTTP 403.

```csharp
services.AddDbContext<CoreDbContext>(options =>
    options.UseSqlServer(connectionString));
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();

services.AddEntityAuthorization(identity =>
{
    identity.WithEntityPermissions<CoreDbContext>(permissions =>
    {
        permissions
            .AddEntity<Employee>(Permission.Read)
            .AddDefaultPermissions<Employee>(Permission.Read)
            .UseDefaultPermissionProvider<Employee>();
    });
});

app.MapGet("/employees/{employeeId}", async Task<Microsoft.AspNetCore.Http.IResult> (
    string employeeId,
    IEntityPermissionEvaluator<Employee> evaluator,
    ICurrentUserAccessor currentUser,
    CancellationToken cancellationToken) =>
{
    var allowed = await evaluator.HasPermissionAsync(
        currentUser,
        employeeId,
        Permission.Read,
        cancellationToken: cancellationToken);

    return allowed
        ? Results.Ok(new { employeeId, permission = "Read", allowed = true })
        : Results.Forbid();
})
.RequireAuthorization();
```

For `employeeId = "emp1"`, the default provider produces:

```json
{
  "employeeId": "emp1",
  "permission": "Read",
  "allowed": true
}
```

## Getting started

### Prerequisites

- An ASP.NET Core application with dependency injection configured
- Entity Framework Core with a database context implementing `IEntityPermissionContext`
- An `ICurrentUserAccessor` implementation for user-aware evaluation in web requests
- A database migration that creates the mapped `__Identity_EntityPermissions` table after the context adds its `DbSet`

### Basic setup

Configure entity permissions in `Program.cs`:

```csharp
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;

services.AddEntityAuthorization(identity =>
{
    identity.WithEntityPermissions<CoreDbContext>(permissions =>
    {
        permissions.AddEntity<Employee>(
            Permission.Read,
            Permission.Write,
            Permission.Delete,
            Permission.List);
    });
});

services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
services.AddDbContext<CoreDbContext>(options =>
    options.UseSqlServer("Server=.;Database=YourDb;Trusted_Connection=True;"));
```

### First secured endpoint

Secure a Minimal API endpoint:

```csharp
app.MapGet("/employees", () => Results.Ok())
    .RequireEntityPermission<Employee>(Permission.List);
```

This keeps the example focused on authorization itself. Application-specific request handling can be plugged into the endpoint however the host prefers.

---

## Setup and configuration

### Fluent configuration

Define permissions using the fluent syntax:

```csharp
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;

services.AddEntityAuthorization(identity =>
{
    identity.WithEntityPermissions<CoreDbContext>(permissions =>
    {
        permissions
            .AddEntity<Employee>(
                Permission.Read,
                Permission.Write,
                Permission.Delete,
                Permission.List,
                Permission.For("Review"))
            .AddDefaultPermissions<Employee>(Permission.Read)
            .UseDefaultPermissionProvider<Employee>()
            .EnableCaching()
            .WithCacheLifetime(TimeSpan.FromMinutes(5));
    })
    .EnableEvaluationEndpoints()
    .EnableManagementEndpoints(options =>
    {
        options.RequireAuthorization = true;
    });
});
```

- **`AddEntity`** registers a regular entity type and the permission names that should be available for it.
- **`AddHierarchicalEntity`** registers a parent-link expression for inherited permissions.
- **`AddDefaultPermissions`** defines baseline permissions for an entity type.
- **`UseDefaultPermissionProvider`** activates either the built-in or a custom default provider.
- **`EnableCaching`** and **`WithCacheLifetime`** control evaluator caching.

### Hierarchical entities

For entities with parent-child relationships, use `AddHierarchicalEntity(...)`:

```csharp
permissions.AddHierarchicalEntity<Department>(
    d => d.ParentId,
    Permission.Read,
    Permission.Write,
    Permission.List);
```

This tells the evaluator how to walk the hierarchy when direct grants are absent.

### Database context

Define the persistence context:

```csharp
public class CoreDbContext : DbContext, IEntityPermissionContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<EntityPermission> EntityPermissions { get; set; }
}
```

### Securing controllers

For controller-based scenarios, use `EntityPermissionRequirementAttribute` from the presentation layer:

```csharp
[Authorize]
[Route("api/employees")]
[ApiController]
public class EmployeeController : ControllerBase
{
    [EntityPermissionRequirement(typeof(Employee), nameof(Permission.List))]
    [HttpGet]
    public IActionResult GetAll() => this.Ok();
}
```

### Securing Minimal APIs

Use `RequireEntityPermission(...)`:

```csharp
app.MapGet("/employees", () => Results.Ok())
    .RequireEntityPermission<Employee>(Permission.List);
```

For route groups:

```csharp
app.MapGroup("/employees")
    .RequireEntityPermission<Employee>(Permission.Read);
```

### Important boundary note

The feature spans multiple packages:

- `Application.Identity` defines the permission model, evaluator, provider abstractions, and rules
- `Infrastructure.EntityFramework` provides `WithEntityPermissions<TContext>(...)` and the EF-backed provider wiring
- `Presentation.Web` adds endpoint helpers and the optional evaluation/management endpoints

That split matters because some APIs that feel like part of the feature actually live outside the core application package.

---

## Managing permissions

Permissions can be managed programmatically using `IEntityPermissionProvider` or `EntityPermissionProviderBuilder`.

### Using `IEntityPermissionProvider` directly

```csharp
var provider = services.GetRequiredService<IEntityPermissionProvider>();

await provider.GrantUserPermissionAsync(
    "user123",
    typeof(Employee).FullName,
    "emp1",
    Permission.Write);

await provider.GrantRolePermissionAsync(
    "Admins",
    typeof(Employee).FullName,
    null,
    "Review");
```

The provider also supports:

- revoking single user or role permissions
- revoking all permissions for one user or role
- listing permissions for users, roles, or a concrete entity
- retrieving the hierarchy path for configured hierarchical entities

### Using `EntityPermissionProviderBuilder`

```csharp
var provider = new EntityPermissionProviderBuilder(
    services.GetRequiredService<IEntityPermissionProvider>())
    .ForUser("user123")
        .WithPermission<Employee>("emp1", Permission.Write)
        .WithPermission<Employee>("emp1", "Review")
    .ForRole("Admins")
        .WithPermission<Employee>(Permission.List)
    .Build();
```

The builder performs each asynchronous grant synchronously through `GetAwaiter().GetResult()`. Use it in controlled seeding or setup code; use the asynchronous provider methods in request handling and other asynchronous workflows.

### Default permission providers

Default providers supply permissions even when no explicit persisted grant exists.

Use cases include:

- public read access
- baseline permissions for specific modules
- environment- or tenant-dependent defaults

The core contract is `IDefaultEntityPermissionProvider<TEntity>`, which exposes `GetDefaultPermissions()`.

### Cache invalidation

The Entity Framework provider invalidates affected cache entries after its grant and revoke methods. Broader invalidation is useful when permissions or default-provider behavior change outside those methods. The cache extension helpers support patterns such as:

- invalidating all permissions for a specific user
- invalidating all permissions for an entity type

See also [Common Caching](./common-caching.md).

---

## Checking permissions

Permissions can be verified using `IEntityPermissionEvaluator<TEntity>` or ASP.NET Core authorization.

### Using `IEntityPermissionEvaluator<TEntity>`

```csharp
var evaluator = services.GetRequiredService<IEntityPermissionEvaluator<Employee>>();

var canWrite = await evaluator.HasPermissionAsync(
    "user123",
    ["Admins"],
    "emp1",
    Permission.Write);

var canReview = await evaluator.HasPermissionAsync(
    "user123",
    [],
    "emp1",
    "Review");
```

The evaluator supports several shapes:

- checks against a concrete entity instance
- checks against an entity id
- wildcard checks against the entity type
- checks for a single permission or any permission in a set
- permission inspection through `GetPermissionsAsync(...)`

### Using `ICurrentUserAccessor`

For application services or handlers already running in a user-aware context:

```csharp
var canRead = await evaluator.HasPermissionAsync(
    currentUserAccessor,
    employeeId,
    Permission.Read,
    cancellationToken: cancellationToken);
```

### Using ASP.NET Core authorization

ASP.NET Core authorization flows into the same permission system through authorization handlers. For Minimal APIs, `RequireEntityPermission(...)` adds an `EntityPermissionAuthorizationRequirement`. For controller-based scenarios, the feature can also participate through policy-based authorization and the attribute helper shown earlier.

### Via runtime evaluation endpoints

If enabled, the evaluation endpoints expose the evaluator over HTTP for operational inspection and debugging.

---

## Rules integration

`Application.Identity` integrates with the Rules feature through:

- `HasPermissionRule<TEntity>`
- `HasNotPermissionRule<TEntity>`

Example:

```csharp
var result = await Rule.CheckAsync(
    new HasPermissionRule<Employee>(
        currentUserAccessor,
        permissionEvaluator,
        employeeId,
        Permission.Write),
    cancellationToken: cancellationToken);
```

This is useful when permission failures should become structured `Result` failures instead of ad hoc branching.

For the broader rule style, see [Rules](./features-rules.md).

---

## API reference

The runtime API surface is optional and lives in `Presentation.Web`.

### Management endpoints

Default group path:

`/_bdk/api/identity/management/entities/permissions`

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/users/{userId}/grant` | `POST` | Grant a permission to a user for an entity type or entity id |
| `/users/{userId}/revoke` | `POST` | Revoke one permission from a user |
| `/users/{userId}/revoke/all` | `POST` | Revoke all permissions from a user |
| `/users/{userId}?entityType={type}&entityId={id}` | `GET` | Get granted permissions for one user and entity target |
| `/users?entityType={type}&entityId={id}` | `GET` | Get granted permissions for all users for an entity target |
| `/roles/{role}/grant` | `POST` | Grant a permission to a role |
| `/roles/{role}/revoke` | `POST` | Revoke one permission from a role |
| `/roles/{role}/revoke/all` | `POST` | Revoke all permissions from a role |
| `/roles/{role}?entityType={type}&entityId={id}` | `GET` | Get granted permissions for one role and entity target |
| `/roles?entityType={type}&entityId={id}` | `GET` | Get granted permissions for all roles for an entity target |

Request body for grant/revoke:

```json
{
  "entityType": "MyApp.Domain.Model.Employee",
  "entityId": "emp1",
  "permission": "Write"
}
```

The management `GET` endpoints return persisted grants. They do not add permissions supplied by default providers.

### Evaluation endpoints

Default group path:

`/_bdk/api/identity/evaluate/entities/permissions`

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/{permission}?entityType={type}&entityId={id}` | `GET` | Check whether the current user has a specific permission |
| `?entityType={type}&entityId={id}` | `GET` | Get the current user's effective permissions |

Example response:

```json
{
  "entityType": "MyApp.Domain.Model.Employee",
  "entityId": "emp1",
  "permission": "Read",
  "source": null,
  "hasAccess": true
}
```

Notes:

- `entityType` can be the configured short CLR name or full CLR type name; persisted grants use the full name
- `entityId` is optional for type-wide checks
- evaluation endpoints can be configured to bypass the cache through `IdentityEntityPermissionEvaluationEndpointsOptions`
- the single-permission evaluation response does not identify the grant source; the effective-permissions endpoint returns source values

---

## Best practices

- Define the smallest permission set that reflects real business needs.
- Use wildcard permissions sparingly for administrative or cross-cutting access.
- Prefer `IEntityPermissionEvaluator<TEntity>` in application code and reserve raw provider access for administration and seeding.
- Use hierarchical entities only when inheritance is a true domain rule.
- Enable caching for read-heavy systems, but understand when freshly granted or revoked permissions should bypass cached results.
- Protect management endpoints with strong authorization and operational access controls.
- Keep permission names consistent across endpoint protection, evaluator checks, and seeded grants.

---

## Troubleshooting

- **403 Forbidden**: Verify the current user is available through `ICurrentUserAccessor`, and check that the permission was granted for the correct full entity type name.
- **Permission Missing After Grant**: Check whether the evaluation path is hitting a cached result and whether the check should bypass the cache.
- **Hierarchy Not Applied**: Confirm the entity type was registered with `AddHierarchicalEntity(...)` and that the parent-id expression matches the entity id type.
- **Endpoints Not Available**: Confirm `EnableEvaluationEndpoints(...)` or `EnableManagementEndpoints(...)` was configured in the identity setup.
- **Entity Type Not Valid**: The runtime endpoints expect the full entity type name, not a short display name.

---

## Appendix: Working with hierarchical entities

### Example: Employee hierarchy

#### Structure

```text
CEO (ceo1)       <- "Read"
  Manager (mgr1) <- "Write", "Delete" (Admins role)
    Employee (emp1)
```

#### Step 1: Configure

```csharp
services.AddEntityAuthorization(identity =>
{
    identity.WithEntityPermissions<CoreDbContext>(permissions =>
    {
        permissions.AddHierarchicalEntity<Employee>(
            e => e.ManagerId,
            Permission.Read,
            Permission.Write,
            Permission.Delete);
    });
});
```

#### Step 2: Grant permissions

```csharp
var provider = services.GetRequiredService<IEntityPermissionProvider>();

await provider.GrantUserPermissionAsync(
    "user123",
    typeof(Employee).FullName,
    "ceo1",
    Permission.Read);

await provider.GrantUserPermissionAsync(
    "user123",
    typeof(Employee).FullName,
    "mgr1",
    Permission.Write);

await provider.GrantRolePermissionAsync(
    "Admins",
    typeof(Employee).FullName,
    "mgr1",
    Permission.Delete);
```

#### Step 3: Check effective permissions

```csharp
var evaluator = services.GetRequiredService<IEntityPermissionEvaluator<Employee>>();

var permissions = await evaluator.GetPermissionsAsync(
    "user123",
    ["Admins"],
    "emp1");

foreach (var permission in permissions)
{
    Console.WriteLine($"{permission.Permission} from {permission.Source}");
}
```

Possible output:

- `Read from Parent:Entity:ceo1`
- `Write from Parent:Entity:mgr1`
- `Delete from Parent:Role:Admins`

### Related documentation

- [Rules](./features-rules.md)
- [Common Caching](./common-caching.md)
- [Presentation Endpoints](./features-presentation-endpoints.md)
