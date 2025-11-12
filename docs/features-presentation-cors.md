# Cors Configuration Feature Documentation

## Overview

CORS (Cross-Origin Resource Sharing) is a security mechanism that controls which origins (domains) can access your API from a browser. By default, browsers block cross-origin requests to protect users from malicious websites.

> Builds on the standard [ASP.NET Cors](https://learn.microsoft.com/en-us/aspnet/core/security/cors) implementation and basicaly provides a configuration (appsettings) system for it.

This feature uses a flexible, configuration-driven CORS setup that supports:

- **Multiple named policies** for different scenarios
- **Environment-specific configuration** (Development vs Production)
- **Global default policies** or per-endpoint control
- **Wildcard subdomain matching**
- **Fine-grained control** over origins, methods, headers, and credentials

### Key Concepts

- **Origin**: The combination of scheme, host, and port (e.g., `https://example.com:443`)
- **Preflight Request**: An OPTIONS request the browser sends before certain cross-origin requests
- **Simple Request**: GET/POST requests that don't trigger preflight
- **Credentials**: Cookies, authorization headers, or client certificates

## Configuration Schema

### CorsConfiguration Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Enabled` | `bool` | Yes | `false` | Whether CORS is enabled. When false, all cross-origin requests are blocked. |
| `DefaultPolicy` | `string` | No | `null` | Name of the policy to apply globally. Leave null for endpoint-level control only. |
| `Policies` | `Dictionary<string, CorsPolicyOptions>` | Yes* | `{}` | Named policies. At least one required when Enabled is true. |

*Required when `Enabled` is `true`

### CorsPolicyOptions Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `AllowedOrigins` | `string[]` | No | `null` | Array of allowed origins (e.g., `["https://example.com"]`). Cannot use with `AllowAnyOrigin`. |
| `AllowedMethods` | `string[]` | No | `null` | Array of allowed HTTP methods (e.g., `["GET", "POST"]`). Cannot use with `AllowAnyMethod`. |
| `AllowedHeaders` | `string[]` | No | `null` | Array of allowed request headers. Cannot use with `AllowAnyHeader`. |
| `ExposeHeaders` | `string[]` | No | `null` | Array of response headers to expose to JavaScript. |
| `AllowCredentials` | `bool?` | No | `false` | Allow credentials (cookies, auth headers). Cannot use with `AllowAnyOrigin = true`. |
| `AllowAnyOrigin` | `bool?` | No | `false` | Allow any origin (*). Cannot use with `AllowCredentials = true`. **Use only in development!** |
| `AllowAnyMethod` | `bool?` | No | `false` | Allow any HTTP method. Overrides `AllowedMethods`. |
| `AllowAnyHeader` | `bool?` | No | `false` | Allow any header. Overrides `AllowedHeaders`. |
| `AllowWildcardSubdomains` | `bool?` | No | `false` | Enable wildcard subdomain matching (e.g., `*.example.com`). |
| `PreflightMaxAgeSeconds` | `int?` | No | `null` | Preflight cache duration in seconds. Recommended: 600-3600 for production. |

## Configuration Examples

### Example 1: Production (Secure Configuration)

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

### Example 2: Development (Permissive Configuration)

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

### Example 3: Public API (No Credentials)

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

⚠️ **Note**: Cannot use `AllowCredentials: true` with `AllowAnyOrigin: true`.

### Example 4: Wildcard Subdomains

Allow any subdomain of example.com:

```json
{
  "Cors": {
    "Enabled": true,
    "DefaultPolicy": "SubdomainPolicy",
    "Policies": {
      "SubdomainPolicy": {
        "AllowedOrigins": [
          "https://example.com"
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

### Example 5: Multiple Named Policies

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

### Example 6: API-Only (Specific Methods and Headers)

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

## Applying Policies

### Global Default Policy

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

```csMharp
builder.Services.AddAppCors(builder.Configuration);
// ...
app.UseAppCors(builder.Configuration); // Applies DefaultPolicy globally
```

### Per-Endpoint Policy (Minimal API)

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

**Note**: Unlike controller-based APIs, Minimal API does not support `[DisableCors]` directly. Simply omit `RequireCors()` on endpoints that should not allow CORS.

### Per-Endpoint Policy (Controller-Based)

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

### Policy Precedence

When both default and endpoint-level policies are configured:

1. **Endpoint-level** `RequireCors("PolicyName")` or `[EnableCors("PolicyName")]` takes precedence over all
2. **Endpoint-level** `RequireCors()` or `[EnableCors()]` without parameters uses the configured default policy
3. **Group-level** `MapGroup().RequireCors()` applies to all endpoints in the group (Minimal API)
4. **Controller-level** `[EnableCors()]` applies to all actions (Controller-based)
5. **Global default policy** applies when no endpoint configuration is present
6. **`[DisableCors]`** disables CORS for specific controller endpoints

**Minimal API Example:**

```csharp
// Global default policy applies to all endpoints
app.UseAppCors(builder.Configuration);

// Group with specific policy
var apiGroup = app.MapGroup("/api")
                  .RequireCors("ApiPolicy");

apiGroup.MapGet("/products", () => Results.Ok(products)); // Uses ApiPolicy

// Override group policy for specific endpoint
apiGroup.MapGet("/products/special", () => Results.Ok(specialProducts))
        .RequireCors("SpecialPolicy"); // Uses SpecialPolicy instead

// Endpoint without RequireCors uses global default
app.MapGet("/public", () => Results.Ok("public data")); // Uses global default policy
```

**Controller-based Example:**

```csharp
[EnableCors()] // Controller-level: uses configured default policy
public class ValuesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() { } // Uses default policy from controller

    [EnableCors("SpecialPolicy")] // Overrides with specific named policy
    [HttpGet("special")]
    public IActionResult GetSpecial() { }

    [DisableCors] // Disables CORS
    [HttpGet("internal")]
    public IActionResult GetInternal() { }
}
```

## Security Best Practices

### Production Recommendations

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

### Development vs Production

**Development** (`appsettings.Development.json`):

- Allow localhost origins with various ports
- Use `AllowCredentials: true` for testing authentication
- Shorter or no preflight cache for rapid iteration

**Production** (`appsettings.json`):

- Specific production origins only
- Longer preflight cache (3600 seconds)
- Minimal permissions (only required methods/headers)

### Wildcard Subdomains

**Safe** when you control all subdomains:

```json
{
  "AllowedOrigins": ["https://example.com"],
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
   app.UseAppCors(builder.Configuration); // Must be here
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
   "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
   ```

   Or:

   ```json
   "AllowAnyMethod": true
   ```

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

### Debugging Tips

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

## Additional Resources

### Official Documentation

- [ASP.NET Core CORS Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [MDN CORS Guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- [W3C CORS Specification](https://www.w3.org/TR/cors/)

### Common Scenarios

| Scenario | Recommended Configuration |
|----------|---------------------------|
| Frontend SPA + API | `AllowedOrigins` with specific domain, `AllowCredentials: true` |
| Public API | `AllowAnyOrigin: true`, `AllowCredentials: false` |
| Multiple subdomains | `AllowWildcardSubdomains: true` with base domain |
| Development | `AllowedOrigins` with localhost ports, `AllowCredentials: true` |
| Microservices | Named policies per service, no default policy |
