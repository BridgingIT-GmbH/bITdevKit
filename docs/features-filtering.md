# Filtering

> Simplify complex entity queries with a unified filtering solution.

[TOC]

## Overview

The Filtering feature lets clients describe filtering, ordering, includes, hierarchy traversal, and paging in a `FilterModel`. Server-side builders translate that model into domain specifications and repository `FindOptions<TEntity>`.

Filtering is a consumer of two lower-level domain features:

- [Domain Specifications](./features-domain-specifications.md) for reusable criteria and named specifications
- [Domain Repositories](./features-domain-repositories.md) for query execution, includes, paging, and ordering

Its JSON-based filter payloads and converter conventions are also closely related to the shared infrastructure documented in [Common Serialization](./common-serialization.md).

```mermaid
graph LR
    R[Client Request]-->|filter|E[API Endpoint]-->|filter|Q[QueryHandler or Service]-->|filter|P[Repository]
    P-->|query|D[(Database)]
    P-.->|Result_IEnumerable_T|R
```

## Challenges

Applications commonly need to:

- Filter data based on multiple conditions
- Combine different filter types (equality, ranges, text search, etc.)
- Sort results by multiple fields
- Include related entities (eager loading)
- Paginate results for better performance
- Handle nested entity relationships
- Support dynamic query building

Representing each query shape as a separate endpoint or set of query parameters can lead to:

- Multiple specialized endpoints for different query scenarios
- Complex URL parameters that are hard to maintain
- Limited query capabilities
- Poor reusability across different entity types

## Solution

The Filtering feature provides one model for query criteria and a set of builders that translate the model into repository specifications and options.

### Unified query interface

- Single, consistent way to express complex queries
- Works across different entity types
- Supports both simple and complex filtering scenarios
- Reduces the need for an endpoint for each query scenario

### Typed server implementation

- Typed server models and generated OpenAPI schemas
- A documented `FilterModel` contract between clients and endpoints
- Expression-based builder methods for server-created filters

### Repository integration

- Built-in custom filter types and named specifications
- Translation to repository `FindOptions<TEntity>`
- Support for additional domain specifications

### Query controls

- Paging and multiple orderings
- Selective eager-loading includes
- No-tracking queries by default

## Key Features

- Standard comparison, string, null, empty, and collection operators.
- Custom date, time, text, numeric, enum, and specification filters.
- Multiple orderings, paging, navigation includes, and hierarchy options.
- JSON query-string and request-body parsing for ASP.NET Core endpoints.
- OpenAPI metadata through `WithFilterSchema(...)`.
- Type-safe server-side construction through `FilterModelBuilder.For<TEntity>()`.
- Direct execution through bITdevKit repository extensions.

## Architecture

`FilterModel` and its JSON representation are defined in `Common.Abstractions`. The Domain layer translates filter criteria into specifications and converts ordering, include, hierarchy, paging, and tracking fields into `FindOptions<TEntity>`. Presentation helpers read the model from HTTP and add its schema to OpenAPI operations. Repository extensions execute the resulting query.

The [request flow diagram](#request-flow-diagram) shows these components in sequence.

## Use Cases

### Data grids, tables, and lists

- Dynamic column filtering
- Multi-column sorting
- Server-side pagination

### Search interfaces

- Full-text search across multiple fields
- Combined filters (date ranges, categories, status)
- Related entity filtering

### Lookup lists

- Dynamic data loading for select components
- Type-ahead/autocomplete requests

## Basic Usage

The following minimal API accepts a JSON filter model, validates that a body was supplied, executes a paged repository query, and returns either the result or a visible problem response:

```csharp
app.MapPost("/api/users/search", async Task<Microsoft.AspNetCore.Http.IResult> (
    HttpContext context,
    IGenericReadOnlyRepository<User> repository,
    CancellationToken cancellationToken) =>
{
    var filter = await context.FromBodyFilterAsync();
    if (filter is null)
    {
        return context.Response.HasStarted
            ? Results.Empty
            : Results.BadRequest(new { error = "A valid filter model is required." });
    }

    var result = await repository.FindAllResultPagedAsync(
        filter,
        cancellationToken: cancellationToken);

    return result.IsSuccess
        ? Results.Ok(result)
        : Results.Problem(
            title: "User search failed",
            detail: string.Join("; ", result.Errors.Select(error => error.Message)));
})
.WithFilterSchema(isRequestBody: true);
```

A request body such as `{"page":1,"pageSize":20,"filters":[{"field":"LastName","operator":"startswith","value":"S"}]}` returns the first page of matching users and its pagination metadata.

## Request flow diagram

```mermaid
sequenceDiagram
    participant C as Client
    participant A as API Controller
    participant H as Query Handler
    participant R as Repository
    participant S as SpecificationBuilder
    participant O as OrderOptionBuilder
    participant I as IncludeOptionBuilder
    participant D as Database

    C->>+A: HTTP Request with FilterModel
    A->>+H: Send Query(FilterModel)
    H->>+R: FindAllResultPagedAsync(FilterModel)

    par Build FindOptions
        R->>+S: Build
        S-->>-R: Specifications
        R->>+O: Build
        O-->>-R: OrderOptions
        R->>+I: Build
        I-->>-R: IncludeOptions
    end

    R->>+D: Execute Query (FindOptions)
    D-->>-R: Raw Results
    R-->>-H: ResultPaged
    H-->>-A: Response
    A-->>-C: HTTP Response (ResultPaged)
```

The following sections detail the implementation and usage of the Filtering feature, providing
comprehensive examples and best practices for common scenarios.

## Filter model structure

```json
{
  "page": 1,
  "pageSize": 10,
  "filters": [
    {
      "field": "Name",
      "operator": "eq|neq|isnull|isnotnull|isempty|isnotempty|gt|gte|lt|lte|contains|doesnotcontain|startswith|doesnotstartwith|endswith|doesnotendwith|any|all|none",
      "value": "any",
      "logic": "and|or",
      "customType": "none|fulltextsearch|daterange|daterelative|timerange|timerelative|numericrange|isnull|isnotnull|enumvalues|textin|textnotin|numericin|numericnotin|namedspecification|compositespecification",
      "customParameters": {
        "key": "value"
      },
      "specificationName": "name",
      "specificationArguments": [],
      "compositeSpecification": {
        "nodes": []
      }
    }
  ],
  "orderings": [
    {
      "field": "Name",
      "direction": "asc|desc"
    }
  ],
  "includes": [
    "name"
  ]
}
```

`field`, `fields`, ordering fields, and include paths are CLR property paths. Their segments are case-sensitive because the builders resolve them with expression-tree property access. For example, use `Department.Name`, not the JSON-style name `department.name`.

## API implementation

### ASP.NET controller example

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ResultPaged<User>>> GetAll(
        [FromQueryFilter] FilterModel filter)
    {
        // or: var filter await this.HttpContext.FromQueryFilterAsync();
        var response = await mediator.Send(new UserFindAllQuery(filter)); // handler calls repository.FindAllResultPagedAsync(filter)

        return Ok(response); // should ideally return a ResultPaged<UserModel> (mapped)
    }

    [HttpPost("search")]
    public async Task<ActionResult<ResultPaged<User>>> Search(
        [FromBodyFilter] FilterModel filter)
    {
        // or: var filter await this.HttpContext.FromBodyFilterAsync();
        var response = await mediator.Send(new UserSearchQuery(filter)); // handler calls repository.FindAllResultPagedAsync(filter)

        return Ok(response); // should ideally return a ResultPaged<UserModel> (mapped)
    }
}
```

### ASP.NET minimal API example

```csharp
app.MapGet("/api/users/search", async Task<Results<Ok<ResultPaged<User>>, NotFound>>
  (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
{
    var filter = await context.FromQueryFilterAsync();
    var response = await mediator.Send(
        new UserSearchQuery(filter), cancellationToken); // handler calls repository.FindAllResultPagedAsync(filter)

    return TypedResults.Ok(response); // should ideally return a ResultPaged<UserModel> (mapped)
}).WithFilterSchema(); // adds openapi schema for the filter model
```

```csharp
app.MapPost("/api/users/search", async Task<Results<Ok<ResultPaged<User>>, NotFound>>
  (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
{
    var filter = await context.FromBodyFilterAsync();
    var response = await mediator.Send(
        new UserSearchQuery(filter), cancellationToken); // handler calls repository.FindAllResultPagedAsync(filter)

    return TypedResults.Ok(response); // should ideally return a ResultPaged<UserModel> (mapped)
}).WithFilterSchema(true); // adds openapi schema for the filter model
```

### Repository usage (QueryHandler)

```csharp
public class UserQueryHandler : IRequestHandler<UserFindAllQuery, ResultPaged<User>>
{
    private readonly IGenericReadOnlyRepository<User> repository;

    public UserQueryHandler(IGenericReadOnlyRepository<User> repository)
    {
        this.repository = repository;
    }

    public async Task<ResultPaged<User>> Handle(
        UserFindAllQuery query,
        CancellationToken cancellationToken)
    {
        return await repository.FindAllResultPagedAsync(
            query.Filter,
            cancellationToken: cancellationToken);
    }
}
```

## HTTP request examples

### GET request

Simple filter as URL parameters:

```http
GET /api/core/cities?filter={"page":1,"pageSize":10,"filters":[{"field":"Name","operator":"eq","value":"Berlin"}]} HTTP/1.1
Accept: application/json
```

URL-encoded for more complex filters:

[URL-encode](https://en.wikipedia.org/wiki/Percent-encoding) the filter JSON and put it into a
single query string parameter named `filter`:

```json
{
  "page": 1,
  "pageSize": 10,
  "filters": [
    {
      "field": "Name",
      "operator": "eq",
      "value": "John"
    }
  ]
}
```

The encoded value starts with `%7B%22page%22%3A1%2C%22pageSize%22...`.

```http
GET /api/users?filter=%7B%22page%22%3A1%2C%22pageSize%22%3A10%2C%22filters%22%3A%5B%7B%22field%22%3A%22name%22%2C%22operator%22%3A%22eq%22%2C%22value%22%3A%22John%22%7D%5D%7D HTTP/1.1
Accept: application/json
```

The following considerations apply to HTTP GET requests:

- HTTP GET requests should be URL-encoded to prevent issues with special characters.
- HTTP GET requests size limits may apply, consider using POST for large filter models.
- HTTP GET requests parameters are visible in logs and browser history.
- HTTP GET requests should be kept short and readable for maintainability.

### POST request

```http
POST /api/users/search HTTP/1.1
Host: api.example.com
Content-Type: application/json
Accept: application/json

{
    "page": 1,
    "pageSize": 20,
    "filters": [
        {
            "customType": "daterange",
            "customParameters": {
                "field": "CreatedAt",
                "startDate": "2024-01-01T00:00:00Z",
                "endDate": "2024-12-31T23:59:59Z",
                "inclusive": true
            }
        },
        {
            "field": "Department.Name",
            "operator": "eq",
            "value": "Engineering",
            "logic": "and"
        }
    ],
    "orderings": [
        {
            "field": "LastName",
            "direction": "asc"
        }
    ],
    "includes": [
        "department",
        "assignments"
    ]
}
```

The following considerations apply to HTTP POST requests:

- HTTP POST requests can handle larger payloads than GET requests.
- HTTP POST request bodies are less likely than query strings to appear in browser history, but HTTPS and appropriate logging controls are still required.
- HTTP POST requests can be used for complex filter models.
- HTTP POST requests are not cached by browsers.

## HTTP response format

### Successful response

```json
{
  "isSuccess": true,
  "messages": [
    "Data retrieved successfully"
  ],
  "errors": [],
  "value": [
    {
      "id": 1,
      "firstName": "John",
      "lastName": "Doe",
      "email": "john.doe@example.com",
      "department": {
        "id": 1,
        "name": "Engineering"
      }
    }
  ],
  "currentPage": 1,
  "totalPages": 5,
  "totalCount": 100,
  "pageSize": 20,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Error response

```json
{
  "isSuccess": false,
  "messages": [
    "Failed to retrieve data"
  ],
  "errors": [
    {
      "code": "INVALID_FILTER",
      "message": "Invalid filter parameters provided"
    }
  ],
  "value": [],
  "currentPage": 1,
  "totalPages": 0,
  "totalCount": 0,
  "pageSize": 10,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

### Response properties

#### Base result properties

- `isSuccess`: Indicates whether the request succeeded
- `messages`: Array of informational or error messages
- `errors`: Array of structured error objects when `isSuccess` is `false`
- `value`: Collection of items for the current page

#### Pagination metadata

- `currentPage`: Current page number (1-based)
- `totalPages`: Total number of pages available
- `totalCount`: Total number of items across all pages
- `pageSize`: Number of items per page
- `hasPreviousPage`: Indicates if a previous page exists
- `hasNextPage`: Indicates if a next page exists

## Best practices

### Request method selection

- Use GET for simple queries and basic filtering
- Use POST for complex filters or when URL length might be an issue
- Do not put secrets in either form; POST does not replace HTTPS or safe logging

### Performance considerations

- Keep page sizes reasonable (recommended: 10-50 items)
- Use includes selectively to prevent excessive data loading
- Consider adding indexes for commonly filtered fields

### Error handling

- Always check the `isSuccess` property in responses
- Handle error messages appropriately in your client application
- Log error details for debugging purposes

### Security

- Validate or allowlist exposed fields, orderings, includes, and specification names
- Implement appropriate rate limiting
- Enforce an application-specific maximum page size; the Filtering feature does not impose one

## Standard filter operators

### Comparison operators

#### Equal (`eq`)

Matches exact values

```json
{
  "field": "Status",
  "operator": "eq",
  "value": "active"
}
```

#### Not equal (`neq`)

Matches values that are not equal

```json
{
  "field": "Status",
  "operator": "neq",
  "value": "deleted"
}
```

#### Greater than (`gt`)

```json
{
  "field": "Age",
  "operator": "gt",
  "value": 18
}
```

#### Greater than or equal (`gte`)

```json
{
  "field": "Price",
  "operator": "gte",
  "value": 100.00
}
```

#### Less than (`lt`)

```json
{
  "field": "Stock",
  "operator": "lt",
  "value": 10
}
```

#### Less than or equal (`lte`)

```json
{
  "field": "Temperature",
  "operator": "lte",
  "value": 25.5
}
```

### String operators

#### Contains

```json
{
  "field": "Description",
  "operator": "contains",
  "value": "premium"
}
```

#### Does not contain

```json
{
  "field": "Title",
  "operator": "doesnotcontain",
  "value": "test"
}
```

#### Starts with

```json
{
  "field": "Email",
  "operator": "startswith",
  "value": "admin"
}
```

#### Does not start with

```json
{
  "field": "Code",
  "operator": "doesnotstartwith",
  "value": "TMP"
}
```

#### Ends with

```json
{
  "field": "FileName",
  "operator": "endswith",
  "value": ".pdf"
}
```

#### Does not end with

```json
{
  "field": "Url",
  "operator": "doesnotendwith",
  "value": "/temp"
}
```

### Null checks

#### Is null

```json
{
  "field": "DeletedAt",
  "operator": "isnull"
}
```

#### Is not null

```json
{
  "field": "Email",
  "operator": "isnotnull"
}
```

### Empty checks

#### Is empty

```json
{
  "field": "Notes",
  "operator": "isempty"
}
```

#### Is not empty

```json
{
  "field": "PhoneNumber",
  "operator": "isnotempty"
}
```

### Collection operators

#### Any

Matches if any child element satisfies the condition

```json
{
  "field": "Orders",
  "operator": "any",
  "value": {
    "field": "Total",
    "operator": "gt",
    "value": 1000
  }
}
```

#### All

Matches if all child elements satisfy the condition

```json
{
  "field": "OrderItems",
  "operator": "all",
  "value": {
    "field": "Quantity",
    "operator": "gt",
    "value": 0
  }
}
```

#### None

Matches if no child elements satisfy the condition

```json
{
  "field": "Reviews",
  "operator": "none",
  "value": {
    "field": "Rating",
    "operator": "lt",
    "value": 3
  }
}
```

## Custom filter types

> Custom filter types provide more specialized filtering capabilities. They are used by setting the
`customType` property instead of using the standard `operator`.

Standard filter and ordering fields support dotted property paths. The current custom-filter builder resolves `customParameters.field` and full-text `fields` as direct properties only, so do not use dotted paths for those values.

### Date and time filters

#### Date range

Filter entries within a specific date range

```json
{
  "customType": "daterange",
  "customParameters": {
    "field": "CreatedAt",
    "startDate": "2024-01-01T00:00:00Z",
    "endDate": "2024-12-31T23:59:59Z",
    "inclusive": true
  }
}
```

#### Date relative

Filter based on relative date periods

```json
{
  "customType": "daterelative",
  "customParameters": {
    "field": "LastLogin",
    "unit": "day",
    "amount": 7,
    "direction": "past"
  }
}
```

#### Time range

Filter entries within a specific time range

```json
{
  "customType": "timerange",
  "customParameters": {
    "field": "ShiftStart",
    "startTime": "09:00:00",
    "endTime": "17:00:00",
    "inclusive": true
  }
}
```

#### Time relative

`timerelative` is part of the serialized `FilterCustomType` contract, but `CustomSpecificationBuilder` does not currently dispatch it. Sending this filter to the standard builder throws `NotSupportedException`. The payload shape reserved by the contract is:

```json
{
  "customType": "timerelative",
  "customParameters": {
    "field": "LastActivity",
    "unit": "hour",
    "amount": 2,
    "direction": "past"
  }
}
```

### Text search filters

#### Full-text search

Search across multiple fields

```json
{
  "customType": "fulltextsearch",
  "customParameters": {
    "searchTerm": "important document",
    "fields": [
      "Title",
      "Description",
      "Content"
    ]
  }
}
```

#### Text in

Match against a list of possible values

```json
{
  "customType": "textin",
  "customParameters": {
    "field": "Status",
    "values": "active;pending;review"
  }
}
```

#### Text not in

Exclude matches from a list of values

```json
{
  "customType": "textnotin",
  "customParameters": {
    "field": "Category",
    "values": "archived;deleted;draft"
  }
}
```

### Numeric filters

#### Numeric range

Filter numbers within a range

```json
{
  "customType": "numericrange",
  "customParameters": {
    "field": "Price",
    "min": 10.00,
    "max": 50.00,
    "inclusive": true
  }
}
```

#### Numeric in

Match against a list of numeric values

```json
{
  "customType": "numericin",
  "customParameters": {
    "field": "Priority",
    "values": "1;2;3"
  }
}
```

#### Numeric not in

Exclude specific numeric values

```json
{
  "customType": "numericnotin",
  "customParameters": {
    "field": "ErrorCode",
    "values": "404;500;503"
  }
}
```

### Enum filters

#### Enum values

Filter by enum values using names or integers

```json
{
  "customType": "enumvalues",
  "customParameters": {
    "field": "Status",
    "values": "Active;Pending"
  }
}
```

### Null-check filters

#### Is null

Explicit null check filter

```json
{
  "customType": "isnull",
  "customParameters": {
    "field": "CanceledAt"
  }
}
```

#### Is not null

Explicit non-null check filter

```json
{
  "customType": "isnotnull",
  "customParameters": {
    "field": "CompletedAt"
  }
}
```

### Specification filters

#### Named specification

Use pre-registered domain specifications

For the underlying specification model itself, including `ISpecification<T>`, composition, and built-in uniqueness specifications, see [Domain Specifications](./features-domain-specifications.md).

```json
{
  "customType": "namedspecification",
  "specificationName": "IsActive",
  "specificationArguments": []
}
```

#### Composite specification

Combine multiple specifications with logical operators

```json
{
  "customType": "compositespecification",
  "compositeSpecification": {
    "nodes": [
      {
        "name": "IsActive",
        "arguments": []
      },
      {
        "logic": "and",
        "nodes": [
          {
            "name": "HasValidLicense",
            "arguments": []
          },
          {
            "name": "IsInRegion",
            "arguments": [
              "EU"
            ]
          }
        ]
      }
    ]
  }
}
```

## Complex filter examples

### Overview

> Complex filters allow you to create sophisticated queries by combining different filter types,
> using nested conditions, and applying custom filter types. They are particularly useful when
> simple
> equality or comparison filters aren't sufficient.

### Use cases and examples

#### 1. Date range with status filter

Useful for finding records within a specific date range that match certain status criteria.

```json
{
  "page": 1,
  "pageSize": 20,
  "filters": [
    {
      "customType": "daterange",
      "customParameters": {
        "field": "CreatedAt",
        "startDate": "2024-01-01T00:00:00Z",
        "endDate": "2024-12-31T23:59:59Z",
        "inclusive": true
      }
    },
    {
      "field": "Status",
      "operator": "eq",
      "value": "active",
      "logic": "and"
    }
  ]
}
```

**Use Case:** Finding all active users who registered during 2024.

#### 2. Full-text search with multiple fields

Perfect for implementing search functionality across multiple text fields.

```json
{
  "filters": [
    {
      "customType": "fulltextsearch",
      "customParameters": {
        "searchTerm": "project management",
        "fields": [
          "Title",
          "Description",
          "Skills",
          "Notes"
        ]
      }
    }
  ]
}
```

**Use Case:** Searching for employees with specific skills or experience across their profile data.

#### 3. Nested entity filtering

Useful when you need to filter based on related entity properties.

```json
{
  "filters": [
    {
      "field": "Department.Name",
      "operator": "eq",
      "value": "Engineering",
      "logic": "and"
    },
    {
      "field": "Projects",
      "operator": "any",
      "value": {
        "field": "Status",
        "operator": "eq",
        "value": "Active"
      }
    }
  ],
  "includes": [
    "Department",
    "Projects"
  ]
}
```

**Use Case:** Finding engineers who are assigned to active projects.

#### 4. Multiple date-related conditions

Combines multiple date-based filters for temporal analysis.

```json
{
  "filters": [
    {
      "customType": "daterange",
      "customParameters": {
        "field": "HireDate",
        "startDate": "2023-01-01T00:00:00Z",
        "endDate": "2023-12-31T23:59:59Z",
        "inclusive": true
      }
    },
    {
      "customType": "daterelative",
      "customParameters": {
        "field": "LastActivity",
        "unit": "day",
        "amount": 30,
        "direction": "past"
      },
      "logic": "and"
    }
  ]
}
```

**Use Case:** Finding employees hired in 2023 who have been active in the last 30 days.

#### 5. Complex numeric conditions

Useful for financial or metric-based filtering.

```json
{
  "filters": [
    {
      "customType": "numericrange",
      "customParameters": {
        "field": "Salary",
        "min": 50000,
        "max": 100000
      }
    },
    {
      "field": "Performance.Rating",
      "operator": "gte",
      "value": 4,
      "logic": "and"
    },
    {
      "field": "Projects",
      "operator": "any",
      "value": {
        "field": "Budget",
        "operator": "gt",
        "value": 100000
      },
      "logic": "and"
    }
  ]
}
```

**Use Case:** Finding high-performing employees within a specific salary range working on
high-budget projects.

#### 6. Time-based working-hours filter

Useful for scheduling and availability queries.

```json
{
  "filters": [
    {
      "customType": "timerange",
      "customParameters": {
        "field": "WorkingHoursStart",
        "startTime": "09:00:00",
        "endTime": "17:00:00",
        "inclusive": true
      }
    },
    {
      "field": "TimeZone",
      "operator": "eq",
      "value": "UTC+1",
      "logic": "and"
    }
  ]
}
```

**Use Case:** Finding employees working during specific hours in a particular timezone.

#### 7. Enum and collection filtering

Combines enum values with collection checks.

```json
{
  "filters": [
    {
      "customType": "enumvalues",
      "customParameters": {
        "field": "EmploymentType",
        "values": "FullTime;PartTime"
      }
    },
    {
      "field": "Skills",
      "operator": "all",
      "value": {
        "field": "Level",
        "operator": "eq",
        "value": "Expert"
      },
      "logic": "and"
    }
  ]
}
```

**Use Case:** Finding full-time or part-time employees whose listed skills are all at the expert level.

#### 8. Complex text-pattern matching

Useful for advanced text search scenarios.

```json
{
  "filters": [
    {
      "field": "Email",
      "operator": "endswith",
      "value": "@company.com"
    },
    {
      "customType": "textin",
      "customParameters": {
        "field": "Department",
        "values": "Engineering;Research;Development"
      },
      "logic": "and"
    },
    {
      "field": "Notes",
      "operator": "contains",
      "value": "leadership",
      "logic": "and"
    }
  ]
}
```

**Use Case:** Finding internal employees from specific departments with leadership mentions in their
notes.

### Advanced combinations

#### Combined project and team filter

```json
{
  "filters": [
    {
      "field": "Teams",
      "operator": "any",
      "value": {
        "field": "Members",
        "operator": "all",
        "value": {
          "field": "Skills",
          "operator": "any",
          "value": {
            "field": "Level",
            "operator": "gte",
            "value": 3
          }
        }
      }
    },
    {
      "customType": "daterange",
      "customParameters": {
        "field": "ProjectDeadline",
        "startDate": "2024-01-01T00:00:00Z",
        "endDate": "2024-12-31T23:59:59Z",
        "inclusive": true
      },
      "logic": "and"
    }
  ],
  "includes": [
    "Teams",
    "Teams.Members",
    "Teams.Members.Skills",
    "Projects"
  ]
}
```

**Use Case:** Finding teams where all members have advanced skills (level ≥ 3) and are working on
projects due in 2024.

## Appendix A: Angular usage guide

> This appendix provides detailed information about using the Filtering feature in an
> Angular application.

This implementation provides a complete Angular solution including:

- Type-safe interfaces
- Reusable service layer
- Component implementation with pagination
- Error handling
- HTTP parameter building

### Type definitions

#### Core filter-model interfaces

```typescript
// models/filter.model.ts
export interface FilterCriteria {
  field?: string;
  operator?: string;
  value?: any;
  logic?: 'and' | 'or';
  filters?: FilterCriteria[];
  customType?: string;
  customParameters?: Record<string, any>;
  specificationName?: string;
  specificationArguments?: any[];
}

export interface FilterModel {
  page: number;
  pageSize: number;
  noTracking?: boolean;
  filters: FilterCriteria[];
  orderings?: Array<{
    field: string;
    direction: 'asc' | 'desc';
  }>;
  includes?: string[];
  hierarchy?: string;
  hierarchyMaxDepth?: number;
}

export interface ResultPaged<T> {
  isSuccess: boolean;
  value: T[];
  messages: string[];
  errors: Array<{ message: string }>;
  currentPage: number;
  totalCount: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
```

### Service implementation

#### API service

```typescript
// services/api.service.ts
import {Injectable} from '@angular/core';
import {HttpClient, HttpParams} from '@angular/common/http';
import {Observable} from 'rxjs';
import {environment} from '../environments/environment';
import {FilterModel, ResultPaged} from '../models';

@Injectable({
  providedIn: 'root'
})
export class ApiService<T> {
  constructor(
    private http: HttpClient,
    private baseUrl: string
  ) {
  }

  // POST (body)
  searchFiltered(filterModel: FilterModel): Observable<ResultPaged<T>> {
    return this.http.post<ResultPaged<T>>(`${this.baseUrl}/search`, filterModel);
  }

  // GET (querystring)
  getFiltered(filterModel: FilterModel): Observable<ResultPaged<T>> {
    const params = new HttpParams()
      .set('filter', JSON.stringify(filterModel));

    return this.http.get<ResultPaged<T>>(this.baseUrl, {params});
  }
}
```

#### Entity-specific service

```typescript
// services/user.service.ts
import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {environment} from '../environments/environment';
import {User} from '../models';
import {ApiService} from './api.service';

@Injectable({
  providedIn: 'root'
})
export class UserService extends ApiService<User> {
  constructor(http: HttpClient) {
    super(http, `${environment.apiBaseUrl}/api/users`);
  }
}
```

### Component implementation

#### List component example

```typescript
// components/user-list/user-list.component.ts
import {Component, OnInit} from '@angular/core';
import {UserService} from '../../services/user.service';
import {User, FilterModel, ResultPaged} from '../../models';
import {finalize} from 'rxjs/operators';

@Component({
  selector: 'app-user-list',
  template: `
        <div class="filters">
            <button (click)="applyDepartmentFilter('Engineering')">
                Engineering Only
            </button>
            <button (click)="applyDateRangeFilter()">
                Last 30 Days
            </button>
        </div>

        <div *ngIf="loading">Loading...</div>

        <div *ngIf="error" class="error">
            {{ error }}
        </div>

        <table *ngIf="users">
            <thead>
                <tr>
                    <th>Name</th>
                    <th>Email</th>
                    <th>Department</th>
                </tr>
            </thead>
            <tbody>
                <tr *ngFor="let user of users.value">
                    <td>{{ user.firstName }} {{ user.lastName }}</td>
                    <td>{{ user.email }}</td>
                    <td>{{ user.department }}</td>
                </tr>
            </tbody>
        </table>

        <div class="pagination" *ngIf="users">
            <button (click)="previousPage()" [disabled]="!users.hasPreviousPage">
                Previous
            </button>
            <span>Page {{ users.currentPage }} of {{ users.totalPages }}</span>
            <button (click)="nextPage()" [disabled]="!users.hasNextPage">
                Next
            </button>
        </div>
    `
})
export class UserListComponent implements OnInit {
  users: ResultPaged<User> | null = null;
  loading = false;
  error: string | null = null;

  private currentFilter: FilterModel = {
    page: 1,
    pageSize: 10,
    filters: []
  };

  constructor(private userService: UserService) {
  }

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.loading = true;
    this.error = null;

    this.userService.getFiltered(this.currentFilter)
      .pipe(
        finalize(() => this.loading = false)
      )
      .subscribe({
        next: (result) => {
          if (result.isSuccess) {
            this.users = result;
          } else {
            this.error = result.errors.map(error => error.message).join('; ');
          }
        },
        error: (error) => {
          this.error = 'Failed to load users. Please try again.';
          console.error('Error loading users:', error);
        }
      });
  }

  applyDepartmentFilter(department: string) {
    this.currentFilter = {
      ...this.currentFilter,
      filters: [
        {
          field: 'Department',
          operator: 'eq',
          value: department
        }
      ]
    };
    this.loadUsers();
  }

  applyDateRangeFilter() {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - 30);

    this.currentFilter = {
      ...this.currentFilter,
      filters: [
        {
          customType: 'daterange',
          customParameters: {
            field: 'CreatedAt',
            startDate: startDate.toISOString(),
            endDate: endDate.toISOString(),
            inclusive: true
          }
        }
      ]
    };
    this.loadUsers();
  }

  nextPage() {
    if (this.users && this.currentFilter.page < this.users.totalPages) {
      this.currentFilter.page++;
      this.loadUsers();
    }
  }

  previousPage() {
    if (this.currentFilter.page > 1) {
      this.currentFilter.page--;
      this.loadUsers();
    }
  }
}
```

## Appendix B: Flow diagram

```mermaid
graph TD
    A[Client Request] -->|FilterModel JSON| B[API Controller]
    B -->|FilterModel| C[Query Handler]
    C -->|FilterModel| D[Repository FindAllAsync]
    D -->|Build| E[SpecificationBuilder]
    D -->|Build| F[OrderOptionBuilder]
    D -->|Build| G[IncludeOptionBuilder]
    E -->|Specifications| FO[FindOptions]
    F -->|OrderOptions| FO
    G -->|IncludeOptions| FO
    FO -->|-| H[(Database Query)]
    H -->|ResultPaged| I[Response]
```

## Appendix C: Filter model builder

> Build a Filter Model using Fluent C# syntax.

Can be used in a Blazor or server side environment to construct complex filters.

### Basic example

```csharp
var filterModel = FilterModelBuilder.For<PersonStub>()
      .SetPaging(2, PageSize.Large) // Fluent paging setup
      .AddFilter(p => p.Age, FilterOperator.GreaterThan, 25) // Age > 25
      .AddFilter(p => p.FirstName, FilterOperator.Contains, "A") // FirstName contains "A"
      .AddFilter(p => p.Locations,
          FilterOperator.Any, b => b
              .AddFilter(loc => loc.City, FilterOperator.Equal, "Berlin")
              .AddFilter(loc => loc.PostalCode, FilterOperator.StartsWith, "100")) // Any location with City = New York or ZipCode starts with "100"
      .AddCustomFilter(FilterCustomType.FullTextSearch)
      .AddParameter("searchTerm", "John")
      .AddParameter("fields", new[] { "FirstName", "LastName" }).Done()
      .AddOrdering(p => p.LastName, OrderDirection.Descending) // Order by LastName Descending
      .AddOrdering(p => p.FirstName, OrderDirection.Ascending) // Then order by FirstName Ascending
      .AddInclude(p => p.Locations)
      .Build();

filterModel.Page.ShouldBe(2);
filterModel.PageSize.ShouldBe((int)PageSize.Large);
// etc.
```

### `AddInclude` methods

The `AddInclude` method is available in two overloads to support different scenarios:

#### Expression-based include (type-safe)

Use lambda expressions for compile-time safety and refactoring support:

```csharp
var filterModel = FilterModelBuilder.For<Customer>()
    .AddInclude(c => c.Orders)           // Single navigation property
    .AddInclude(c => c.Addresses)        // Multiple includes
    .Build();
```

#### String-based include (flexible)

Use string paths for dynamic includes or nested navigation properties:

```csharp
var filterModel = FilterModelBuilder.For<Customer>()
    .AddInclude("Orders")                // Simple property path
    .AddInclude("Orders.OrderItems")     // Nested navigation path
    .AddInclude("Addresses.City")        // Multiple levels deep
    .Build();
```

#### Conditional includes

Both overloads support conditional inclusion using the `condition` parameter:

```csharp
var includeOrders = true;
var includeAddresses = false;

var filterModel = FilterModelBuilder.For<Customer>()
    .AddInclude(c => c.Orders, condition: includeOrders)        // Will be included
    .AddInclude("Addresses", condition: includeAddresses)       // Will be skipped
    .Build();
```

#### When to use each overload

**Expression-Based (`AddInclude(c => c.Property)`):**

- Provides compile-time safety and IntelliSense support
- Best for known, statically-defined relationships
- Automatically refactored when property names change
- Limited to direct property access (single level)

**String-Based (`AddInclude("Property.Nested")`):**

- More flexible for dynamic scenarios
- Supports deeply nested navigation paths
- Useful when property names come from configuration or user input
- Can specify complex paths like `"Orders.OrderItems.Product"`

#### Combined example

```csharp
var filterModel = FilterModelBuilder.For<Order>()
    .SetPaging(1, 20)
    .AddFilter(o => o.Status, FilterOperator.Equal, "Shipped")
    .AddInclude(o => o.Customer)                    // Type-safe
    .AddInclude("Customer.Addresses")               // Nested path
    .AddInclude("OrderItems.Product")               // Multi-level navigation
    .AddInclude("OrderItems.Product.Category")      // Deep navigation
    .Build();
```

### `ThenInclude` - nested navigation properties

The `ThenInclude` feature enables type-safe chaining of navigation properties for eager loading deeply nested entity graphs.

#### Basic usage

**Reference Navigation** (single related entity):

```csharp
var filterModel = FilterModelBuilder.For<Customer>()
    .AddInclude(c => c.BillingAddress)
        .ThenInclude(a => a.City)
        .ThenInclude(c => c.Country)
    .Build();
```

**Collection Navigation** (collection of related entities):

```csharp
var filterModel = FilterModelBuilder.For<Customer>()
    .AddInclude(c => c.Orders)             // ICollection<Order>
        .ThenInclude(o => o.OrderItems)    // Lambda parameter is element type
        .ThenInclude(i => i.Product)
    .Build();
```

Supports all common collection types: `IEnumerable<T>`, `ICollection<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `List<T>`.

#### Multiple include chains

```csharp
var filterModel = FilterModelBuilder.For<Order>()
    .AddInclude(o => o.ShippingAddress)
        .ThenInclude(a => a.City)
    .AddInclude(o => o.OrderItems)
        .ThenInclude(i => i.Product)
    .AddInclude(o => o.Customer)
        .ThenInclude(c => c.BillingAddress)
    .Build();
```

#### Conditional includes

```csharp
var filterModel = FilterModelBuilder.For<Product>()
    .AddInclude(p => p.Category)
        .ThenInclude(c => c.ParentCategory, condition: includeDetails)
    .Build();
```

When `condition: false`, all subsequent ThenIncludes in that chain are skipped.

#### Integration with filters and ordering

ThenInclude works seamlessly with other builder methods:

```csharp
var filterModel = FilterModelBuilder.For<Customer>()
    .AddFilter(c => c.IsActive, FilterOperator.Equal, true)
    .AddInclude(c => c.Orders)
        .ThenInclude(o => o.OrderItems)
    .AddOrdering(c => c.LastName, OrderDirection.Ascending)
    .SetPaging(1, 25)
    .Build();
```

#### Example

```csharp
var filterModel = FilterModelBuilder.For<Order>()
    .AddFilter(o => o.Status, FilterOperator.Equal, OrderStatus.Active)
    .AddInclude(o => o.Customer)
        .ThenInclude(c => c.BillingAddress)
        .ThenInclude(a => a.City)
    .AddInclude(o => o.OrderItems)
        .ThenInclude(i => i.Product)
        .ThenInclude(p => p.Category)
    .AddOrdering(o => o.OrderDate, OrderDirection.Descending)
    .SetPaging(1, 20)
    .Build();
```

## Appendix D: disclaimer

The Filtering feature provides a structured filtering model for REST APIs and repositories.

It is not intended to replace or compete with comprehensive query technologies like:

- **GraphQL**: A complete query language that provides a type system and allows clients to specify
  exactly what data they need
- **OData**: A standardized protocol for building and consuming RESTful APIs with rich query
  capabilities

### When to use the Filtering feature

- When already using bITdevKit repository and specification patterns
- For REST APIs needing structured filtering
- When requiring a balance between flexibility and simplicity
- Need for a typed, maintainable filtering solution without the overhead of implementing larger
  query frameworks

If the application requires complex schema definitions, introspection, or full query language
capabilities, consider using GraphQL or OData instead. The Filtering feature focuses on providing a
structured approach to common filtering scenarios while using bITdevKit repository and specification features.

Remember: Choose the simplest tool that meets your requirements. The feature provides a
lightweight, code-based approach to handle filtering, while staying consistent with
the bITdevKit philosophy of simple, effective solutions to common development problems.
