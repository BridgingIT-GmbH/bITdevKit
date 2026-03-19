# Security Examples

This file contains WRONG vs CORRECT examples for security best practices in C#/.NET code.

## Hardcoded Secrets (🔴 CRITICAL)

**Rule**: NEVER hardcode sensitive information in source code.

### Example 1: Hardcoded Database Connection String

```csharp
// 🔴 CRITICAL: Hardcoded connection string with password
public class DatabaseService
{
    private const string ConnectionString = 
        "Server=prod-sql.company.com;Database=CustomerDB;User Id=sa;Password=P@ssw0rd123!;";
    
    public async Task<List<Customer>> GetCustomersAsync()
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        // ...
    }
}

// ✅ CORRECT: Configuration-based connection string
public class DatabaseService
{
    private readonly IConfiguration configuration;
    
    public DatabaseService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }
    
    public async Task<List<Customer>> GetCustomersAsync()
    {
        var connectionString = this.configuration.GetConnectionString("CustomerDatabase");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        // ...
    }
}
```

**Storage**:
```json
// appsettings.Development.json (git-ignored)
{
  "ConnectionStrings": {
    "CustomerDatabase": "Server=localhost;Database=CustomerDB;Integrated Security=true;"
  }
}

// Production: Use Azure Key Vault or environment variables
```

### Example 2: Hardcoded API Keys

```csharp
// 🔴 CRITICAL: Hardcoded API key
public class EmailService
{
    private const string SendGridApiKey = "SG.1234567890abcdef.xyz_secretkey_abc123";
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var client = new SendGridClient(SendGridApiKey);
        // ...
    }
}

// ✅ CORRECT: Configuration-based API key
public class EmailService
{
    private readonly string apiKey;
    
    public EmailService(IConfiguration configuration)
    {
        this.apiKey = configuration["SendGrid:ApiKey"]
            ?? throw new InvalidOperationException("SendGrid API key not configured");
    }
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var client = new SendGridClient(this.apiKey);
        // ...
    }
}
```

**Better with IOptions**:
```csharp
// ✅ BETTER: Strongly-typed configuration
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
        
        if (string.IsNullOrWhiteSpace(this.settings.ApiKey))
            throw new InvalidOperationException("Email API key is required");
    }
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var client = new SendGridClient(this.settings.ApiKey);
        // ...
    }
}

// Program.cs registration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
```

### Example 3: Hardcoded Encryption Keys

```csharp
// 🔴 CRITICAL: Hardcoded encryption key
public class EncryptionService
{
    private static readonly byte[] EncryptionKey = new byte[]
    {
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
        0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10
    };
    
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        // ...
    }
}

// ✅ CORRECT: Key from secure storage
public class EncryptionService
{
    private readonly byte[] encryptionKey;
    
    public EncryptionService(IConfiguration configuration)
    {
        // Key stored in Azure Key Vault or secure environment variable
        var keyBase64 = configuration["Encryption:Key"];
        this.encryptionKey = Convert.FromBase64String(keyBase64);
    }
    
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = this.encryptionKey;
        // ...
    }
}
```

## SQL Injection Prevention (🔴 CRITICAL)

**Rule**: NEVER construct SQL queries using string concatenation or interpolation with user input.

### Example 4: SQL Injection Vulnerability

```csharp
// 🔴 CRITICAL: SQL injection vulnerability
public async Task<Customer> GetCustomerByNameAsync(string customerName)
{
    var sql = $"SELECT * FROM Customers WHERE Name = '{customerName}'";
    var customer = await context.Database
        .SqlQueryRaw<Customer>(sql)
        .FirstOrDefaultAsync();
    return customer;
}

// Attack: User provides: "'; DROP TABLE Customers; --"
// Result: SELECT * FROM Customers WHERE Name = ''; DROP TABLE Customers; --'
// Database table destroyed!
```

```csharp
// ✅ CORRECT: Parameterized raw SQL query
public async Task<Customer> GetCustomerByNameAsync(string customerName)
{
    var sql = "SELECT * FROM Customers WHERE Name = {0}";
    var customer = await context.Database
        .SqlQueryRaw<Customer>(sql, customerName)
        .FirstOrDefaultAsync();
    return customer;
}

// ✅ BETTER: EF Core LINQ (safe by default)
public async Task<Customer> GetCustomerByNameAsync(string customerName)
{
    return await context.Customers
        .Where(c => c.Name == customerName)
        .FirstOrDefaultAsync();
}
```

### Example 5: Dynamic WHERE Clause Vulnerability

```csharp
// 🔴 CRITICAL: Building WHERE clause from user input
public async Task<List<Customer>> SearchCustomersAsync(string searchField, string searchValue)
{
    var sql = $"SELECT * FROM Customers WHERE {searchField} = '{searchValue}'";
    return await context.Database
        .SqlQueryRaw<Customer>(sql)
        .ToListAsync();
}

// Attack: searchField = "Name = 'Admin' OR '1'='1"
// Result: Bypasses intended logic, returns all customers

// ✅ CORRECT: Whitelist allowed fields and use parameters
public async Task<List<Customer>> SearchCustomersAsync(string searchField, string searchValue)
{
    // Whitelist allowed fields
    var allowedFields = new[] { "Name", "Email", "City" };
    if (!allowedFields.Contains(searchField, StringComparer.OrdinalIgnoreCase))
        throw new ArgumentException($"Invalid search field: {searchField}");
    
    // Use parameterized query with validated field name
    var sql = $"SELECT * FROM Customers WHERE {searchField} = {{0}}";
    return await context.Database
        .SqlQueryRaw<Customer>(sql, searchValue)
        .ToListAsync();
}

// ✅ BETTER: Use expression trees or specification pattern
public async Task<List<Customer>> SearchCustomersAsync(string searchField, string searchValue)
{
    IQueryable<Customer> query = context.Customers;
    
    query = searchField.ToLowerInvariant() switch
    {
        "name" => query.Where(c => c.Name == searchValue),
        "email" => query.Where(c => c.Email.Value == searchValue),
        "city" => query.Where(c => c.Addresses.Any(a => a.City == searchValue)),
        _ => throw new ArgumentException($"Invalid search field: {searchField}")
    };
    
    return await query.ToListAsync();
}
```

## Input Validation (🔴 CRITICAL)

**Rule**: Validate all external inputs before processing.

### Example 6: Missing Input Validation

```csharp
// 🔴 CRITICAL: No input validation
public class CustomerController
{
    public async Task<IActionResult> CreateCustomer(string email, int age, decimal creditLimit)
    {
        var customer = new Customer
        {
            Email = email,
            Age = age,
            CreditLimit = creditLimit
        };
        
        await repository.InsertAsync(customer);
        return Ok(customer);
    }
}

// Issues:
// - Email not validated (could be "not-an-email", empty, or malicious)
// - Age not validated (could be -1000 or 999999)
// - CreditLimit not validated (could be negative or ridiculously high)
```

```csharp
// ✅ CORRECT: Input validation with guards
public async Task<Result<Customer>> CreateCustomer(string email, int age, decimal creditLimit)
{
    // Validate email
    if (string.IsNullOrWhiteSpace(email))
        return Result<Customer>.Failure(new ValidationError("Email is required"));
    
    if (!new EmailAddressAttribute().IsValid(email))
        return Result<Customer>.Failure(new ValidationError("Email format is invalid"));
    
    // Validate age
    if (age < 0 || age > 150)
        return Result<Customer>.Failure(new ValidationError("Age must be between 0 and 150"));
    
    // Validate credit limit
    if (creditLimit < 0)
        return Result<Customer>.Failure(new ValidationError("Credit limit cannot be negative"));
    
    if (creditLimit > 1000000)
        return Result<Customer>.Failure(new ValidationError("Credit limit cannot exceed $1,000,000"));
    
    var customer = new Customer
    {
        Email = email,
        Age = age,
        CreditLimit = creditLimit
    };
    
    await repository.InsertAsync(customer);
    return Result<Customer>.Success(customer);
}

// ✅ BETTER: FluentValidation for complex rules
public class CreateCustomerCommand
{
    public string Email { get; set; }
    public int Age { get; set; }
    public decimal CreditLimit { get; set; }
    
    public class Validator : AbstractValidator<CreateCustomerCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email format is invalid");
            
            RuleFor(x => x.Age)
                .InclusiveBetween(0, 150).WithMessage("Age must be between 0 and 150");
            
            RuleFor(x => x.CreditLimit)
                .GreaterThanOrEqualTo(0).WithMessage("Credit limit cannot be negative")
                .LessThanOrEqualTo(1000000).WithMessage("Credit limit cannot exceed $1,000,000");
        }
    }
}
```

### Example 7: File Upload Validation

```csharp
// 🔴 CRITICAL: No file upload validation
public async Task<string> UploadFileAsync(IFormFile file)
{
    var path = Path.Combine("wwwroot/uploads", file.FileName);
    using var stream = new FileStream(path, FileMode.Create);
    await file.CopyToAsync(stream);
    return path;
}

// Issues:
// - No file size limit (can cause DoS)
// - No file type validation (can upload malicious files)
// - Uses user-provided filename (path traversal vulnerability)
// - Stores in wwwroot (files are publicly accessible)
```

```csharp
// ✅ CORRECT: Validated file upload
public async Task<Result<string>> UploadFileAsync(IFormFile file)
{
    // Validate file size (5 MB max)
    const long MaxFileSize = 5 * 1024 * 1024;
    if (file.Length > MaxFileSize)
        return Result<string>.Failure(new ValidationError("File size exceeds 5 MB limit"));
    
    if (file.Length == 0)
        return Result<string>.Failure(new ValidationError("File is empty"));
    
    // Validate file type (whitelist)
    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(extension))
        return Result<string>.Failure(new ValidationError("File type not allowed. Allowed: jpg, jpeg, png, pdf"));
    
    // Validate MIME type (don't trust extension alone)
    var allowedMimeTypes = new[] { "image/jpeg", "image/png", "application/pdf" };
    if (!allowedMimeTypes.Contains(file.ContentType))
        return Result<string>.Failure(new ValidationError("Invalid file content type"));
    
    // Generate secure filename (prevent path traversal)
    var safeFileName = $"{Guid.NewGuid()}{extension}";
    
    // Store outside webroot (not publicly accessible)
    var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    Directory.CreateDirectory(uploadPath);
    
    var filePath = Path.Combine(uploadPath, safeFileName);
    
    using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);
    
    return Result<string>.Success(safeFileName);
}
```

## Cryptography Best Practices (🟡 IMPORTANT)

### Example 8: Weak Hashing Algorithm

```csharp
// 🔴 CRITICAL: Using MD5 (weak, broken algorithm)
public string HashPassword(string password)
{
    using var md5 = MD5.Create();
    var bytes = Encoding.UTF8.GetBytes(password);
    var hash = md5.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}

// Issues:
// - MD5 is cryptographically broken
// - No salt (vulnerable to rainbow table attacks)
// - Fast hashing (vulnerable to brute force)
```

```csharp
// ✅ CORRECT: Secure password hashing
public class PasswordService
{
    private readonly PasswordHasher<User> passwordHasher = new();
    
    public string HashPassword(User user, string password)
    {
        // Uses PBKDF2 with salt and iterations (secure)
        return this.passwordHasher.HashPassword(user, password);
    }
    
    public bool VerifyPassword(User user, string password, string hash)
    {
        var result = this.passwordHasher.VerifyHashedPassword(user, hash, password);
        return result == PasswordVerificationResult.Success;
    }
}

// Usage
var hashedPassword = passwordService.HashPassword(user, "MySecretPassword");
// Store hashedPassword in database

// Verify during login
var isValid = passwordService.VerifyPassword(user, loginPassword, user.HashedPassword);
```

### Example 9: Insecure Random Number Generation

```csharp
// 🔴 CRITICAL: Using Random for security tokens (predictable)
public string GenerateResetToken()
{
    var random = new Random();
    var token = random.Next(100000, 999999).ToString();
    return token;
}

// Issues:
// - Random is not cryptographically secure
// - Predictable sequence (attacker can guess tokens)
// - Only 900,000 possible values (easy to brute force)
```

```csharp
// ✅ CORRECT: Secure random number generation
public string GenerateResetToken()
{
    var bytes = new byte[32]; // 256 bits
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(bytes);
    return Convert.ToBase64String(bytes);
}

// Result: "xQ7z8vK2mN4pR9wL5tY3hA6jF1sD8gH0cV4bE7nM2qO="
// - Cryptographically secure
// - 2^256 possible values (impossible to brute force)
```

## Authorization Checks (🔴 CRITICAL)

### Example 10: Missing Authorization

```csharp
// 🔴 CRITICAL: No authorization check
public async Task<Result> DeleteCustomerAsync(Guid customerId)
{
    var customer = await repository.FindOneAsync(customerId);
    if (customer == null)
        return Result.Failure(new NotFoundError("Customer not found"));
    
    await repository.DeleteAsync(customer);
    return Result.Success();
}

// Issue: Any authenticated user can delete any customer!
```

```csharp
// ✅ CORRECT: Authorization check
[Authorize(Roles = "Admin,CustomerManager")]
public async Task<Result> DeleteCustomerAsync(Guid customerId, ClaimsPrincipal user)
{
    var customer = await repository.FindOneAsync(customerId);
    if (customer == null)
        return Result.Failure(new NotFoundError("Customer not found"));
    
    // Additional check: Can only delete own customers unless Admin
    if (!user.IsInRole("Admin"))
    {
        var userId = user.GetUserId();
        if (customer.OwnerId != userId)
            return Result.Failure(new ForbiddenError("Cannot delete customer owned by another user"));
    }
    
    await repository.DeleteAsync(customer);
    return Result.Success();
}
```

## Logging Security (🟡 IMPORTANT)

### Example 11: Logging Sensitive Data

```csharp
// 🔴 CRITICAL: Logging sensitive data
public async Task<Result> LoginAsync(string email, string password)
{
    logger.LogInformation("User {Email} attempting login with password {Password}", email, password);
    
    var user = await userRepository.FindByEmailAsync(email);
    if (user == null || !VerifyPassword(user, password))
    {
        logger.LogWarning("Login failed for {Email} with password {Password}", email, password);
        return Result.Failure(new AuthenticationError("Invalid credentials"));
    }
    
    return Result.Success();
}

// Issues:
// - Passwords logged in plain text
// - Logs can be read by operations team, compromising security
```

```csharp
// ✅ CORRECT: Safe logging (no sensitive data)
public async Task<Result> LoginAsync(string email, string password)
{
    logger.LogInformation("User {Email} attempting login", email);
    // Password NOT logged
    
    var user = await userRepository.FindByEmailAsync(email);
    if (user == null || !VerifyPassword(user, password))
    {
        logger.LogWarning("Login failed for {Email} - invalid credentials", email);
        // Still no password logged
        return Result.Failure(new AuthenticationError("Invalid credentials"));
    }
    
    logger.LogInformation("User {Email} logged in successfully", email);
    return Result.Success();
}
```

## Summary

Security examples demonstrate critical protections:

✅ **No hardcoded secrets** (🔴 CRITICAL - use IConfiguration, Key Vault, environment variables)  
✅ **SQL injection prevented** (🔴 CRITICAL - parameterized queries, EF Core LINQ)  
✅ **Input validation enforced** (🔴 CRITICAL - whitelist, type, length, format checks)  
✅ **File uploads validated** (🔴 CRITICAL - size, type, MIME, secure filenames)  
✅ **Strong cryptography** (🟡 IMPORTANT - PasswordHasher, RandomNumberGenerator, no MD5/SHA1)  
✅ **Authorization checks** (🔴 CRITICAL - role-based, resource-based checks)  
✅ **Safe logging** (🟡 IMPORTANT - no passwords, tokens, credit cards in logs)  

**Quick security scan**:
- Search for hardcoded strings: `"Password"`, `"ApiKey"`, `"Server="`, `"secret"`
- Search for SQL injection risks: `SqlQueryRaw`, `ExecuteSqlRaw`, `$"SELECT`
- Search for weak crypto: `MD5`, `SHA1`, `DES`, `new Random()`
- Search for missing validation: Check all `public` API methods

**Reference**: See `checklists/02-security.md` for complete security checklist.
