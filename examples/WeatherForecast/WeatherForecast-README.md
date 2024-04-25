
### Layers & Dependencies

``` 

                                                                    - Services, Jobs, Validators
                                                                    - Commands/Query + Handlers
                                                .----------------.  - Messages/Queues + Handlers
   - WebApi/Mvc/                            .-->| Application    |  - Adapter Interfaces, Exceptions
     SPA/Console program host              /    `----------------`  - View Models + Mappings
                                          /        |        ^
  .--------------.                       /         |        |
  .              |     .--------------. /          V        |  - Events, Aggregates, Services
  | Presentation |     |              |/        .--------.  |  - Entities, ValueObjects
  | .Web|Tool    |---->| Presentation |-------->| Domain |  |  - Repository interfaces
  |  Service|*   |     |              |\        `--------`  |  - Specifications, Rules
  |              |     `--------------` \          ^        |                                         
  `--------------`                       \         |        |                                    
                       - Composition Root \        |        |     
                       - Controllers       \    .----------------.  - Interface Implementierungen (Adapters/Repositories)  
                       - Razor Pages        `-->| Infrastructure |  - DbContext
                       - Hosted Services        `----------------`  - Data Entities + Mappings   

```

# Presentation Layer

#### Api Client
The Api Client is generated with the [Unchase extension](https://marketplace.visualstudio.com/items?itemName=Unchase.unchaseopenapiconnectedservice). 
This is a Visual Studio extension to generate C# (TypeScript) HttpClient (or C# Controllers) code for OpenAPI (formerly Swagger API) web service with NSwag.
The underlaying generator used is NSwag.

WeatherForecast.Presentation.Web.Client > Connected Services > [WeatherForecast](.\WeatherForecast.Presentation.Web.Client\Connected%20Services\WeatherForecast)

Right click and choose 'Update Unchase' to regenerate the [ApiClient](.\WeatherForecast.Presentation.Web.Client\Connected%20Services\WeatherForecast\ApiClient.cs)

# Application Layer

# Domain Layer

# Infrastructure

-------------------

# Domain

#### CITY 
- InMemoryRepository

#### FORECAST
- EntityFrameworkRepository


------------------------------

- Logging (Serilog)
- Jobs
- Commands/Queries
- Repositories + Decorators
- BusinessRules
- ValueObject
- AggregateRoot + TypedIds
- DomainEvents

# Application


# Presentation
biespiel requests [hier](WeatherForecast.REST.http)

# Development

### Entity Framework
- Setup: `dotnet tool install --global dotnet-ef` or `dotnet tool update dotnet-ef -g`
- Migrations: `dotnet ef migrations add [NAME] --context WeatherForecastDbContext --output-dir .\EntityFramework\Migrations --project .\examples\WeatherForecast.Infrastructure\WeatherForecast.Infrastructure.csproj --startup-project .\examples\WeatherForecast.Presentation.Web.Server\WeatherForecast.Presentation.Web.Server.csproj`
- Database Update: done when starting the service (MigrationsHostedService) 