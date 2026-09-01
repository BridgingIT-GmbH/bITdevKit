# CORS Configuration

> Configure browser cross-origin access through fluent, settings-driven CORS policies.

[TOC]

## Overview

CORS (Cross-Origin Resource Sharing) lets a server relax the browser's same-origin policy for selected cross-origin requests. It does not authenticate or authorize callers, and non-browser clients are not constrained by browser CORS enforcement.

> Builds on standard [ASP.NET Core CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors) and adds configuration binding from `appsettings`.

This feature uses a flexible, configuration-driven CORS setup that supports:

- **Multiple named policies** for different scenarios
- **Environment-specific configuration** (Development vs Production)
- **Global default policies** or per-endpoint control
- **Wildcard subdomain matching**
- **Fine-grained control** over origins, methods, headers, and credentials

## Challenges

Browser applications often run on a different scheme, host, or port from an API. The API must identify trusted origins and decide which methods, headers, response headers, and credentials each origin may use. Incorrect combinations can either block valid browser calls or expose authenticated endpoints too broadly.

## Solution

`AddCors(IConfiguration)` binds the `Cors` section, validates its named policies, and registers ASP.NET Core CORS services. `UseCors(IConfiguration)` conditionally adds the middleware. Applications can select one configured default policy or apply named policies through endpoint routing and controller attributes.

## Key Features

- settings-driven named CORS policies
- optional global default policy
- exact-origin and wildcard-subdomain matching
- method, request-header, exposed-header, and credential controls
- configurable preflight cache duration
- startup validation for missing policies and invalid credential combinations
- Minimal API, route-group, and controller policy application

## Architecture

The DevKit registration extension translates each `CorsPolicyOptions` value into an ASP.NET Core `CorsPolicyBuilder`. The application extension reads the same configuration and adds `UseCors()` only when CORS is enabled. ASP.NET Core middleware then evaluates request origin and endpoint metadata against the selected policy.

## Use Cases

- allow a browser SPA to call an authenticated API from a known origin
- expose a credential-free public API to any origin
- assign different policies to public, frontend, and administrative endpoints
- allow controlled subdomains owned by one organization
- tune preflight caching for production browser clients

## Basic Usage

Configure one exact frontend origin in `appsettings.json`:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "Frontend",
    "Policies": {
      "Frontend": {
        "AllowedOrigins": ["https://app.example.com"],
        "AllowAnyMethod": true,
        "AllowAnyHeader": true,
        "AllowCredentials": true
      }
    }
  }
}
```

Register the policies and place the conditional middleware after routing and before authorization:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseRouting();
app.UseCors(builder.Configuration);
app.UseAuthorization();

app.MapGet("/api/status", () => Results.Ok(new { Status = "ready" }));
app.Run();
```

A browser request to `/api/status` from `https://app.example.com` receives `Access-Control-Allow-Origin: https://app.example.com`. Other origins do not receive an allow-origin response header.

## Key concepts

- **Origin**: The combination of scheme, host, and port (e.g., `https://example.com:443`)
- **Preflight Request**: An OPTIONS request the browser sends before certain cross-origin requests
- **Simple Request**: A request that satisfies the browser's method, header, and content-type conditions and therefore does not require preflight
- **Credentials**: Cookies, authorization headers, or client certificates

## Configuration schema

### `CorsConfiguration` properties

| Property | Type | Required | Default | Description |
| ---------- | ------ | ---------- | --------- | ------------- |
| `Enabled` | `bool` | No | `false` | Whether DevKit registers and applies CORS. When false, the server emits no DevKit CORS headers. |
| `DefaultPolicy` | `string` | No | `null` | Name of the policy to apply globally. Leave null for endpoint-level control only. |
| `Policies` | `Dictionary<string, CorsPolicyOptions>` | Yes* | `{}` | Named policies. At least one required when Enabled is true. |

*Required when `Enabled` is `true`

### `CorsPolicyOptions` properties

| Property | Type | Required | Default | Description |
| ---------- | ------ | ---------- | --------- | ------------- |
| `AllowedOrigins` | `string[]` | No | `null` | Allowed origins. Ignored when `AllowAnyOrigin` is `true`. |
| `AllowedMethods` | `string[]` | No | `null` | Allowed HTTP methods. Ignored when `AllowAnyMethod` is `true`. |
| `AllowedHeaders` | `string[]` | No | `null` | Allowed request headers. Ignored when `AllowAnyHeader` is `true`. |
| `ExposeHeaders` | `string[]` | No | `null` | Array of response headers to expose to JavaScript. |
| `AllowCredentials` | `bool?` | No | `null` (false) | Allow credentials. Cannot be `true` with `AllowAnyOrigin = true`. |
| `AllowAnyOrigin` | `bool?` | No | `null` (false) | Allow any origin (`*`). Use only for credential-free public endpoints or controlled development scenarios. |
| `AllowAnyMethod` | `bool?` | No | `null` (false) | Allow any HTTP method. Overrides `AllowedMethods`. |
| `AllowAnyHeader` | `bool?` | No | `null` (false) | Allow any request header. Overrides `AllowedHeaders`. |
| `AllowWildcardSubdomains` | `bool?` | No | `null` (false) | Enable matching for wildcard origins such as `https://*.example.com`. |
| `PreflightMaxAgeSeconds` | `int?` | No | `null` | Preflight cache duration in seconds. Recommended: 600-3600 for production. |

## Configuration examples

### Example 1: Production configuration

Specific origins with credentials support:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "ProductionPolicy",
    "Policies": {
      "ProductionPolicy": {
        "AllowedOrigins": [
          "https://www.example.com",
          "https://app.example.com"
        ],
        "AllowAnyMethod": true,
        "AllowAnyHeader": true,
        "AllowCredentials": true,
        "PreflightMaxAgeSeconds": 3600
      }
    }
  }
}
```

### Example 2: Development configuration

Allow common localhost ports (included in `appsettings.Development.json`):

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "LocalhostPolicy",
    "Policies": {
      "LocalhostPolicy": {
        "AllowedOrigins": [
          "https://localhost:5001",
          "https://localhost:5000",
          "https://localhost:3000",
          "https://localhost:3001",
          "https://localhost:4200",
          "http://localhost:5001",
          "http://localhost:5000",
          "http://localhost:3000",
          "http://localhost:3001"
        ],
        "AllowAnyMethod": true,
        "AllowAnyHeader": true,
        "AllowCredentials": true
      }
    }
  }
}
```

Common ports:

- **5001/5000**: ASP.NET Core default ports
- **3000/3001**: React, Node.js default ports
- **4200**: Angular default port

### Example 3: Public API without credentials

Allow any origin without credentials:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "PublicApiPolicy",
    "Policies": {
      "PublicApiPolicy": {
        "AllowAnyOrigin": true,
        "AllowAnyMethod": true,
        "AllowAnyHeader": true,
        "PreflightMaxAgeSeconds": 600
      }
    }
  }
}
```

**Note**: Do not use `AllowCredentials: true` with `AllowAnyOrigin: true`; registration rejects that combination.

### Example 4: Wildcard subdomains

Allow any subdomain of example.com:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "SubdomainPolicy",
    "Policies": {
      "SubdomainPolicy": {
        "AllowedOrigins": [
          "https://*.example.com"
        ],
        "AllowWildcardSubdomains": true,
        "AllowAnyMethod": true,
        "AllowAnyHeader": true,
        "AllowCredentials": true
      }
    }
  }
}
```

This allows:

- `https://api.example.com`
- `https://app.example.com`
- `https://admin.example.com`
- Any other subdomain of `example.com`

### Example 5: Multiple named policies

Different policies for different endpoints:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": null,
    "Policies": {
      "FrontendPolicy": {
        "AllowedOrigins": ["https://app.example.com"],
        "AllowAnyMethod": true,
        "AllowAnyHeader": true,
        "AllowCredentials": true
      },
      "PublicApiPolicy": {
        "AllowAnyOrigin": true,
        "AllowedMethods": ["GET"],
        "AllowAnyHeader": true
      },
      "AdminPolicy": {
        "AllowedOrigins": ["https://admin.example.com"],
        "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
        "AllowAnyHeader": true,
        "AllowCredentials": true
      }
    }
  }
}
```

### Example 6: API with specific methods and headers

Restrictive configuration for internal API:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "RestrictivePolicy",
    "Policies": {
      "RestrictivePolicy": {
        "AllowedOrigins": ["https://internal.example.com"],
        "AllowedMethods": ["GET", "POST"],
        "AllowedHeaders": ["Content-Type", "Authorization"],
        "ExposeHeaders": ["X-Total-Count", "X-Page-Number"],
        "AllowCredentials": true,
        "PreflightMaxAgeSeconds": 1800
      }
    }
  }
}
```

## Applying policies

### Global default policy

Apply a policy to all endpoints by specifying `DefaultPolicy`:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "DefaultPolicy",
    "Policies": {
      "DefaultPolicy": {
        "AllowAnyOrigin": true,
        "AllowAnyMethod": true,
        "AllowAnyHeader": true
      }
    }
  }
}
```

In `Program.cs`:

```csharp
builder.Services.AddCors(builder.Configuration);
// ...
app.UseCors(builder.Configuration); // Applies DefaultPolicy globally
```

### Per-endpoint policy for Minimal APIs

Apply different policies to specific Minimal API endpoints using `RequireCors()`:

```csharp
// Use default policy (configured in DefaultPolicy setting)
app.MapGet("/api/products", () => Results.Ok(products))
   .RequireCors();

// Use specific named policy
app.MapGet("/api/products/frontend", () => Results.Ok(frontendData))
   .RequireCors("FrontendPolicy");

// Use different policy for admin endpoint
app.MapPost("/api/products/admin", (Product product) =>
{
    // Create product logic
    return Results.Created($"/api/products/{product.Id}", product);
})
.RequireCors("AdminPolicy");

// Group multiple endpoints with same policy
var productsGroup = app.MapGroup("/api/products")
                       .RequireCors("FrontendPolicy");

productsGroup.MapGet("/", () => Results.Ok(products));
productsGroup.MapGet("/{id}", (int id) => Results.Ok(GetProduct(id)));
productsGroup.MapPost("/", (Product product) => Results.Created($"/api/products/{product.Id}", product));
```

Use `.DisableCors()` on a Minimal API endpoint or route group when it must opt out of inherited CORS metadata. Omitting `RequireCors()` is sufficient only when no default or parent-group policy applies.

### Per-endpoint policy for controllers

If using controllers, apply policies using the `[EnableCors]` attribute:

```csharp
using Microsoft.AspNetCore.Cors;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    // Use default policy (configured in DefaultPolicy setting)
    [EnableCors()]
    [HttpGet]
    public IActionResult GetPublicProducts()
    {
        // ...
    }

    // Use specific named policy
    [EnableCors("FrontendPolicy")]
    [HttpGet("frontend")]
    public IActionResult GetFrontendData()
    {
        // ...
    }

    // Use different policy for admin endpoint
    [EnableCors("AdminPolicy")]
    [HttpPost("admin")]
    public IActionResult CreateProduct([FromBody] Product product)
    {
        // ...
    }

    // Disable CORS for specific endpoint
    [DisableCors]
    [HttpGet("internal")]
    public IActionResult GetInternalData()
    {
        // ...
    }
}
```

### Policy selection

Choose one clear policy model where possible:

1. Set `DefaultPolicy` and call `UseCors(builder.Configuration)` when one policy should apply globally.
2. Leave `DefaultPolicy` unset and use `RequireCors("PolicyName")` or `[EnableCors("PolicyName")]` for endpoint-level control.
3. `RequireCors()` and `[EnableCors()]` without a name use the registered default policy.
4. Route-group and controller metadata flow to their child endpoints.
5. Avoid combining a global middleware policy with controller `[EnableCors]` policies; ASP.NET Core can apply both rather than treating one as a simple override.
6. Use `.DisableCors()` for endpoint-routing metadata or `[DisableCors]` for controller attributes, while noting that `[DisableCors]` does not cancel CORS added through endpoint routing's `RequireCors`.

**Minimal API example:**

```csharp
// DefaultPolicy is null, so middleware uses endpoint metadata
app.UseCors(builder.Configuration);

// Group with specific policy
var apiGroup = app.MapGroup("/api")
                  .RequireCors("ApiPolicy");

apiGroup.MapGet("/products", () => Results.Ok(products)); // Uses ApiPolicy

// Use a separate group for another named policy
var specialGroup = app.MapGroup("/api/special")
                      .RequireCors("SpecialPolicy");

specialGroup.MapGet("/products", () => Results.Ok(specialProducts));

// No CORS policy applies to this endpoint
app.MapGet("/internal", () => Results.Ok("internal data"));
```

**Controller-based example:**

```csharp
public class ValuesController : ControllerBase
{
    [EnableCors("DefaultPolicy")]
    [HttpGet]
    public IActionResult Get() { }

    [EnableCors("SpecialPolicy")]
    [HttpGet("special")]
    public IActionResult GetSpecial() { }

    [DisableCors] // Disables CORS
    [HttpGet("internal")]
    public IActionResult GetInternal() { }
}
```

## Security best practices

### Production recommendations

1. **Never use `AllowAnyOrigin: true` with `AllowCredentials: true`**
   - This violates the CORS specification
   - The configuration will throw an exception at startup

2. **Always specify exact origins in production**

   ```json
   "AllowedOrigins": [
     "https://www.example.com",
     "https://app.example.com"
   ]
   ```

3. **Avoid `AllowAnyOrigin` in production**
   - Only use for public APIs without authentication
   - Prefer specific origins or wildcard subdomains

4. **Use HTTPS origins**
   - Always use `https://` in production
   - HTTP origins (`http://`) are only acceptable for localhost in development

5. **Limit methods to what's needed**

   ```json
   "AllowedMethods": ["GET", "POST", "PUT", "DELETE"]
   ```

   Instead of:

   ```json
   "AllowAnyMethod": true
   ```

6. **Set preflight cache duration**

   ```json
   "PreflightMaxAgeSeconds": 3600
   ```

   Reduces overhead by caching preflight responses

### Development and production

**Development** (`appsettings.Development.json`):

- Allow localhost origins with various ports
- Use `AllowCredentials: true` for testing authentication
- Shorter or no preflight cache for rapid iteration

**Production** (`appsettings.json`):

- Specific production origins only
- Longer preflight cache (3600 seconds)
- Minimal permissions (only required methods/headers)

### Wildcard subdomains

**Safe** when you control all subdomains:

```json
{
  "AllowedOrigins": ["https://*.example.com"],
  "AllowWildcardSubdomains": true,
  "AllowCredentials": true
}
```

**Unsafe** with public subdomains:

- Don't use if anyone can create subdomains (e.g., `*.github.io`)

## Troubleshooting

### Issue: CORS error "No 'Access-Control-Allow-Origin' header"

**Symptoms:**

```text
Access to fetch at 'https://api.example.com/data' from origin 'https://app.example.com'
has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present.
```

**Solutions:**

1. **Check if CORS is enabled**

   ```json
   "Cors": {
     "Enabled": true
   }
   ```

2. **Verify origin is in AllowedOrigins**
   - Origin must match exactly (including scheme and port)
   - Don't include trailing slashes: `https://example.com` ✅ not `https://example.com/` ❌

3. **Check middleware ordering in `Program.cs`**

   ```csharp
   app.UseRouting();
   app.UseCors(builder.Configuration); // Must be here
   app.UseAuthorization();
   ```

4. **Verify configuration is loaded**
   - Check `appsettings.json` syntax is valid
   - Ensure environment-specific settings are merged correctly

### Issue: "CORS policy: Response to preflight request doesn't pass"

**Symptoms:**

```text
Response to preflight request doesn't pass access control check:
The value of the 'Access-Control-Allow-Origin' header must not be the wildcard '*'
when the request's credentials mode is 'include'.
```

**Solution:**

Cannot use `AllowAnyOrigin: true` with `AllowCredentials: true`:

**Invalid:**

```json
{
  "AllowAnyOrigin": true,
  "AllowCredentials": true
}
```

**Valid:**

```json
{
  "AllowedOrigins": ["https://app.example.com"],
  "AllowCredentials": true
}
```

### Issue: Preflight OPTIONS request fails

**Symptoms:**

- Browser shows OPTIONS request with 204/200 response
- But actual request (GET/POST) fails

**Solutions:**

1. **Ensure methods are allowed**

   ```json
   "AllowedMethods": ["GET", "POST", "PUT", "DELETE"]
   ```

   Or:

   ```json
   "AllowAnyMethod": true
   ```

   Configure the method used by the actual request. The CORS middleware handles the `OPTIONS` preflight request.

2. **Check custom headers are allowed**

   ```json
   "AllowedHeaders": ["Content-Type", "Authorization", "X-Custom-Header"]
   ```

   Or:

   ```json
   "AllowAnyHeader": true
   ```

3. **Verify Content-Type is allowed**
   - For JSON requests, ensure `Content-Type: application/json` is allowed

### Issue: Credentials not being sent

**Symptoms:**

- Cookies or Authorization headers not included in cross-origin requests

**Solutions:**

1. **Server-side: Enable AllowCredentials**

   ```json
   "AllowCredentials": true
   ```

2. **Client-side: Set credentials mode**

   Fetch API:

   ```javascript
   fetch('https://api.example.com/data', {
     credentials: 'include' // Send cookies
   });
   ```

   Axios:

   ```javascript
   axios.get('https://api.example.com/data', {
     withCredentials: true
   });
   ```

   jQuery:

   ```javascript
   $.ajax({
     url: 'https://api.example.com/data',
     xhrFields: {
       withCredentials: true
     }
   });
   ```

### Issue: Configuration validation errors on startup

**Error:**

```text
InvalidOperationException: CORS is enabled but no policies are defined.
```

**Solution:**
Add at least one policy when `Enabled: true`:

```json
{
  "Cors": {
    "Enabled": true,
    "Policies": {
      "DefaultPolicy": { /* ... */ }
    }
  }
}
```

**Error:**

```text
InvalidOperationException: CORS DefaultPolicy 'MyPolicy' is not defined in Cors:Policies.
```

**Solution:**
Ensure `DefaultPolicy` name matches a policy in `Policies`:

```json
{
  "DefaultPolicy": "MyPolicy",
  "Policies": {
    "MyPolicy": { /* ... */ }
  }
}
```

### Debugging tips

1. **Check browser console** for detailed CORS error messages
2. **Use browser DevTools Network tab** to inspect:
   - OPTIONS preflight request and response
   - Response headers (`Access-Control-*`)
   - Request headers (`Origin`, `Access-Control-Request-*`)
3. **Test with curl** to isolate browser vs server issues:

   ```bash
   curl -X OPTIONS https://api.example.com/endpoint \
     -H "Origin: https://app.example.com" \
     -H "Access-Control-Request-Method: POST" \
     -i
   ```

4. **Temporarily use permissive settings** for debugging:

   ```json
   {
     "AllowAnyOrigin": true,
     "AllowAnyMethod": true,
     "AllowAnyHeader": true
   }
   ```

   Then narrow down to identify the specific restriction causing issues.

## Additional resources

### Official documentation

- [ASP.NET Core CORS Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [MDN CORS Guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- [Fetch Standard: CORS protocol](https://fetch.spec.whatwg.org/#http-cors-protocol)

### Common scenarios

| Scenario | Recommended Configuration |
| ---------- | --------------------------- |
| Frontend SPA + API | `AllowedOrigins` with specific domain, `AllowCredentials: true` |
| Public API | `AllowAnyOrigin: true`, `AllowCredentials: false` |
| Multiple subdomains | `AllowWildcardSubdomains: true` with base domain |
| Development | `AllowedOrigins` with localhost ports, `AllowCredentials: true` |
| Microservices | Named policies per service, no default policy |
