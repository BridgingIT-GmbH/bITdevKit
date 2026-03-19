# Security Checklist

Use this checklist to identify security vulnerabilities in C#/.NET code. Security issues are typically 🔴 CRITICAL and must be fixed before merge.

## Hardcoded Secrets (🔴 CRITICAL)

**Rule**: NEVER hardcode sensitive information in source code.

- [ ] **No connection strings**: Database connection strings are not hardcoded
- [ ] **No API keys**: API keys and tokens are not in code
- [ ] **No passwords**: Passwords, secrets, or private keys are not in code
- [ ] **No URLs with credentials**: URLs with usernames/passwords are not hardcoded
- [ ] **No encryption keys**: Cryptographic keys are not in source files
- [ ] **Check all file types**: .cs, .json, .xml, .config files checked

### Example

```csharp
// 🔴 CRITICAL: Hardcoded secrets
public class DatabaseService
{
    private const string ConnectionString = "Server=prod.db.com;Database=mydb;User Id=admin;Password=Secret123!;";
    private const string ApiKey = "sk_live_51ABC123XYZ789";
    
    public void Connect()
    {
        using var connection = new SqlConnection(ConnectionString);
        // ...
    }
}

// ✅ CORRECT: Configuration-based secrets
public class DatabaseService
{
    private readonly IConfiguration configuration;
    
    public DatabaseService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }
    
    public void Connect()
    {
        var connectionString = this.configuration.GetConnectionString("MyDatabase");
        using var connection = new SqlConnection(connectionString);
        // ...
    }
}
```

**Storage options**:
- **Local development**: `appsettings.Development.json` (git-ignored) or User Secrets
- **Production**: Azure Key Vault, AWS Secrets Manager, environment variables
- **CI/CD**: Pipeline secrets, not in repository

**How to fix**: 
1. Remove hardcoded values from code
2. Store in configuration system (IConfiguration)
3. Use secret management for production (Key Vault, environment variables)
4. Add sensitive files to `.gitignore`

## Configuration Usage (🔴 CRITICAL)

- [ ] **IConfiguration injected**: Services accept `IConfiguration` or `IOptions<T>` via constructor
- [ ] **IOptions<T> for typed configuration**: Complex configuration uses strongly-typed options
- [ ] **No direct appsettings access**: Code doesn't directly read `appsettings.json` file
- [ ] **Environment-specific config**: Different configs for Development, Staging, Production
- [ ] **Secrets not in appsettings.json**: `appsettings.json` committed to git has no secrets

### Example

```csharp
// 🔴 CRITICAL: Reading configuration file directly
public class EmailService
{
    public void SendEmail()
    {
        var json = File.ReadAllText("appsettings.json");
        var config = JsonSerializer.Deserialize<AppSettings>(json);
        var apiKey = config.EmailApiKey; // WRONG!
    }
}

// ✅ CORRECT: IConfiguration injection
public class EmailService
{
    private readonly IConfiguration configuration;
    
    public EmailService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }
    
    public void SendEmail()
    {
        var apiKey = this.configuration["Email:ApiKey"];
        // ...
    }
}

// ✅ BETTER: IOptions<T> for typed configuration
public class EmailSettings
{
    public string ApiKey { get; set; }
    public string FromAddress { get; set; }
}

public class EmailService
{
    private readonly EmailSettings settings;
    
    public EmailService(IOptions<EmailSettings> options)
    {
        this.settings = options.Value;
    }
    
    public void SendEmail()
    {
        var apiKey = this.settings.ApiKey;
        // ...
    }
}
```

**Why it matters**: Configuration injection enables environment-specific settings, testing with mock configs, and secure secret management.

## Input Validation (🔴 CRITICAL)

- [ ] **All external inputs validated**: API parameters, file uploads, user input checked
- [ ] **Whitelist validation**: Validate against allowed values, not just blacklist
- [ ] **Type validation**: Ensure inputs are expected types (numbers, dates, GUIDs)
- [ ] **Length validation**: Check string lengths, file sizes, collection counts
- [ ] **Format validation**: Email, URL, phone number formats validated
- [ ] **Range validation**: Numeric values within acceptable ranges
- [ ] **FluentValidation used**: Complex validation uses FluentValidation library

### Example

```csharp
// 🔴 CRITICAL: No input validation
public class CustomerService
{
    public async Task<Customer> CreateCustomer(string email, int age)
    {
        var customer = new Customer { Email = email, Age = age };
        await repository.InsertAsync(customer);
        return customer;
    }
}

// ✅ CORRECT: Input validation with guards
public class CustomerService
{
    public async Task<Result<Customer>> CreateCustomer(string email, int age)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result<Customer>.Failure(new ValidationError("Email is required"));
        
        if (!IsValidEmail(email))
            return Result<Customer>.Failure(new ValidationError("Email format invalid"));
        
        if (age < 0 || age > 150)
            return Result<Customer>.Failure(new ValidationError("Age must be between 0 and 150"));
        
        var customer = new Customer { Email = email, Age = age };
        await repository.InsertAsync(customer);
        return Result<Customer>.Success(customer);
    }
    
    private bool IsValidEmail(string email) => 
        new EmailAddressAttribute().IsValid(email);
}

// ✅ BETTER: FluentValidation for complex rules
public class CreateCustomerCommand
{
    public string Email { get; set; }
    public int Age { get; set; }
    
    public class Validator : AbstractValidator<CreateCustomerCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email format invalid");
            
            RuleFor(x => x.Age)
                .InclusiveBetween(0, 150).WithMessage("Age must be between 0 and 150");
        }
    }
}
```

**Why it matters**: Unvalidated input is the root cause of many vulnerabilities (injection, buffer overflow, logic errors).

## SQL Injection Prevention (🔴 CRITICAL)

**Rule**: NEVER construct SQL queries using string concatenation or interpolation.

- [ ] **Parameterized queries**: All SQL uses parameters (EF Core, Dapper, ADO.NET)
- [ ] **No string concatenation**: SQL queries don't use `+` or `$""` with user input
- [ ] **ORM usage**: Use EF Core LINQ queries (safe by default)
- [ ] **Stored procedures parameterized**: Stored procedure calls use parameters
- [ ] **No dynamic SQL**: Avoid dynamic SQL; if needed, use parameterized queries

### Example

```csharp
// 🔴 CRITICAL: SQL injection vulnerability
public async Task<Customer> GetCustomerByName(string name)
{
    var sql = $"SELECT * FROM Customers WHERE Name = '{name}'";
    var customer = await context.Database
        .SqlQueryRaw<Customer>(sql)
        .FirstOrDefaultAsync();
    return customer;
}

// User input: "'; DROP TABLE Customers; --"
// Results in: SELECT * FROM Customers WHERE Name = ''; DROP TABLE Customers; --'

// ✅ CORRECT: Parameterized query (raw SQL)
public async Task<Customer> GetCustomerByName(string name)
{
    var sql = "SELECT * FROM Customers WHERE Name = {0}";
    var customer = await context.Database
        .SqlQueryRaw<Customer>(sql, name)
        .FirstOrDefaultAsync();
    return customer;
}

// ✅ BETTER: EF Core LINQ (safe by default)
public async Task<Customer> GetCustomerByName(string name)
{
    return await context.Customers
        .Where(c => c.Name == name)
        .FirstOrDefaultAsync();
}
```

**Why it matters**: SQL injection can lead to data theft, data destruction, or complete system compromise. It's one of the most critical web vulnerabilities.

**How to detect**:
- Search for `SqlQueryRaw`, `ExecuteSqlRaw`, `FromSqlRaw` - ensure they use parameters
- Look for string concatenation or interpolation in SQL strings
- Check for dynamic `WHERE` clauses built from user input

## Cryptography Best Practices (🟡 IMPORTANT)

- [ ] **Use BCL cryptography**: Use System.Security.Cryptography classes (not custom crypto)
- [ ] **No weak algorithms**: Avoid MD5, SHA1, DES (use SHA256, AES)
- [ ] **Proper key management**: Keys stored securely, not hardcoded
- [ ] **Secure random numbers**: Use `RandomNumberGenerator`, not `Random` for security
- [ ] **Password hashing**: Use `PasswordHasher<T>` or BCrypt, not plain hashing
- [ ] **HTTPS enforced**: Require HTTPS for sensitive data transmission

### Example

```csharp
// 🔴 CRITICAL: Weak hashing algorithm (MD5)
public string HashPassword(string password)
{
    using var md5 = MD5.Create();
    var bytes = Encoding.UTF8.GetBytes(password);
    var hash = md5.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}

// 🟡 IMPORTANT: Insecure random number generation
public string GenerateToken()
{
    var random = new Random();
    return random.Next(100000, 999999).ToString();
}

// ✅ CORRECT: Secure password hashing
public class UserService
{
    private readonly PasswordHasher<User> passwordHasher = new();
    
    public string HashPassword(User user, string password)
    {
        return this.passwordHasher.HashPassword(user, password);
    }
    
    public bool VerifyPassword(User user, string password, string hash)
    {
        var result = this.passwordHasher.VerifyHashedPassword(user, hash, password);
        return result == PasswordVerificationResult.Success;
    }
}

// ✅ CORRECT: Secure random number generation
public string GenerateToken()
{
    var bytes = new byte[32];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(bytes);
    return Convert.ToBase64String(bytes);
}
```

**Why it matters**: Weak cryptography can be broken, exposing sensitive data. Custom crypto is notoriously difficult to implement correctly.

## Authentication and Authorization (🔴 CRITICAL)

- [ ] **Authentication required**: Sensitive endpoints require authentication
- [ ] **Authorization checks**: Operations verify user has required permissions
- [ ] **No role checks in UI only**: Server-side authorization enforced
- [ ] **Secure session management**: Session tokens are secure, HttpOnly, SameSite
- [ ] **Token validation**: JWT tokens are validated (signature, expiration, issuer)
- [ ] **Password policies**: Strong password requirements enforced

### Example

```csharp
// 🔴 CRITICAL: No authorization check
public async Task<Result> DeleteCustomer(Guid customerId)
{
    var customer = await repository.FindOneAsync(customerId);
    await repository.DeleteAsync(customer);
    return Result.Success();
}

// ✅ CORRECT: Authorization check
[Authorize(Roles = "Admin")]
public async Task<Result> DeleteCustomer(Guid customerId, ClaimsPrincipal user)
{
    // Double-check authorization in code (defense in depth)
    if (!user.IsInRole("Admin"))
        return Result.Failure(new UnauthorizedError("Admin role required"));
    
    var customer = await repository.FindOneAsync(customerId);
    
    // Check if user has permission for this specific customer
    if (customer.OwnerId != user.GetUserId() && !user.IsInRole("Admin"))
        return Result.Failure(new ForbiddenError("Cannot delete customer owned by another user"));
    
    await repository.DeleteAsync(customer);
    return Result.Success();
}
```

**Why it matters**: Missing authorization allows unauthorized access to data or operations, leading to data breaches or system compromise.

## Dependency Vulnerabilities (🟡 IMPORTANT)

- [ ] **Dependencies up to date**: NuGet packages are reasonably current
- [ ] **No known vulnerabilities**: Check for known CVEs in dependencies
- [ ] **Minimal dependencies**: Only necessary packages are included
- [ ] **Trusted sources**: Packages from official NuGet.org or trusted feeds
- [ ] **Regular audits**: Periodic security audits of dependencies

### How to Check

```bash
# Check for vulnerable packages
dotnet list package --vulnerable

# Check for outdated packages
dotnet list package --outdated

# Update packages
dotnet add package <PackageName>
```

**Why it matters**: Vulnerable dependencies are a common attack vector. Keeping dependencies updated reduces risk.

## Logging and Error Handling (🟡 IMPORTANT)

- [ ] **No sensitive data in logs**: Passwords, tokens, credit card numbers not logged
- [ ] **Structured logging**: Use structured logging (Serilog) with safe properties
- [ ] **Error messages sanitized**: Don't expose internal details in user-facing errors
- [ ] **Exception details hidden**: Stack traces not shown to end users
- [ ] **Security events logged**: Failed login attempts, authorization failures logged

### Example

```csharp
// 🔴 CRITICAL: Logging sensitive data
logger.LogInformation("User {Email} logged in with password {Password}", email, password);

// 🔴 CRITICAL: Exposing stack trace to user
catch (Exception ex)
{
    return BadRequest(ex.ToString()); // Exposes internal details!
}

// ✅ CORRECT: Safe logging
logger.LogInformation("User {Email} logged in successfully", email);
// Password not logged

// ✅ CORRECT: Sanitized error response
catch (Exception ex)
{
    logger.LogError(ex, "Error processing customer creation");
    return Problem(
        title: "An error occurred",
        detail: "Unable to create customer. Please try again.",
        statusCode: 500
    );
}
```

**Why it matters**: Logs can leak sensitive information. Detailed error messages can help attackers understand system internals.

## File Upload Security (🔴 CRITICAL)

- [ ] **File type validation**: Validate file extensions and MIME types (whitelist)
- [ ] **File size limits**: Enforce maximum file sizes
- [ ] **Antivirus scanning**: Scan uploaded files for malware (if applicable)
- [ ] **Secure storage**: Store files outside webroot, use blob storage
- [ ] **Randomized filenames**: Don't use user-provided filenames directly
- [ ] **Content inspection**: Validate file contents match extension

### Example

```csharp
// 🔴 CRITICAL: No validation on file upload
public async Task<string> UploadFile(IFormFile file)
{
    var path = Path.Combine("wwwroot/uploads", file.FileName);
    using var stream = new FileStream(path, FileMode.Create);
    await file.CopyToAsync(stream);
    return path;
}

// ✅ CORRECT: Validated file upload
public async Task<Result<string>> UploadFile(IFormFile file)
{
    // Validate file size
    const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    if (file.Length > MaxFileSize)
        return Result<string>.Failure(new ValidationError("File too large"));
    
    // Validate file type (whitelist)
    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(extension))
        return Result<string>.Failure(new ValidationError("File type not allowed"));
    
    // Validate MIME type
    var allowedMimeTypes = new[] { "image/jpeg", "image/png", "application/pdf" };
    if (!allowedMimeTypes.Contains(file.ContentType))
        return Result<string>.Failure(new ValidationError("Invalid file content type"));
    
    // Generate secure filename (prevent path traversal)
    var fileName = $"{Guid.NewGuid()}{extension}";
    var path = Path.Combine("uploads", fileName); // Outside wwwroot
    
    using var stream = new FileStream(path, FileMode.Create);
    await file.CopyToAsync(stream);
    
    return Result<string>.Success(fileName);
}
```

**Why it matters**: Unrestricted file uploads can lead to code execution, stored XSS, or denial of service.

## Summary

Security checklist ensures critical vulnerabilities are caught:

✅ **No hardcoded secrets** (🔴 CRITICAL - use IConfiguration/Key Vault)  
✅ **Configuration properly injected** (🔴 CRITICAL - IConfiguration or IOptions<T>)  
✅ **All external inputs validated** (🔴 CRITICAL - whitelist, type, length, format)  
✅ **SQL injection prevented** (🔴 CRITICAL - parameterized queries, EF Core LINQ)  
✅ **Strong cryptography** (🟡 IMPORTANT - BCL classes, secure random, password hashing)  
✅ **Authentication and authorization enforced** (🔴 CRITICAL - server-side checks)  
✅ **Dependencies checked for vulnerabilities** (🟡 IMPORTANT - `dotnet list package --vulnerable`)  
✅ **Logging sanitized** (🟡 IMPORTANT - no sensitive data, safe error messages)  
✅ **File uploads validated** (🔴 CRITICAL - type, size, content validation)  

**Quick security check**: Search codebase for:
- `Password`, `ApiKey`, `Secret`, `ConnectionString` - ensure not hardcoded
- `SqlQueryRaw`, `ExecuteSqlRaw`, `FromSqlRaw` - ensure parameterized
- `MD5`, `SHA1`, `DES` - replace with stronger algorithms
- `new Random()` - replace with `RandomNumberGenerator` for security

**Reference**: See `examples/security-examples.md` for detailed WRONG vs CORRECT examples.
