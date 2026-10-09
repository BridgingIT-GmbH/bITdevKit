# WeatherFiesta

Weather dashboard module built on [bITdevKit](../../README.md). Demonstrates ActiveEntity pattern, service agent abstraction, subscription plans, and the full IRequester pipeline.

## Overview

WeatherFiesta lets authenticated users subscribe to cities, view current weather and forecasts, receive alerts and recommendations, and export data. A subscription plan system gates features like city limits, forecast depth, comparison, and export.

Weather data comes from the [Open-Meteo](https://open-meteo.com/) free API via a service agent abstraction — the Application layer never touches HTTP directly.

## Architecture

Clean Onion / Modular vertical slices:

```
Domain          →  Aggregates, Value Objects, Enumerations, Specifications, Domain Events, Rules
Application     →  Commands, Queries, Models, Abstractions (IWeatherAgent, IWeatherGeocodingClient), Tasks
Infrastructure  →  EF Core (CoreDbContext, configurations), OpenMeteo client, WeatherAgent implementation
Presentation    →  Minimal API endpoints, CoreModule registration
```

### Domain

| Artifact | Examples |
|---|---|
| **Aggregates / Entities** | `City`, `UserCity`, `UserProfile`, `UserSubscription`, `CurrentWeather`, `WeatherForecast`, `HourlyForecast`, `WeatherAlert`, `WeatherRecommendation` |
| **Value Objects** | `Location` (latitude/longitude) |
| **Enumerations** | `TemperatureUnit`, `WindSpeedUnit`, `WeatherConditionCode` (WMO codes), `AlertType`, `AlertSeverity`, `RecommendationCategory`, `RecommendationSeverity`, `SubscriptionPlan`, `SubscriptionStatus`, `SubscriptionBillingCycle` |
| **Specifications** | `CitySpecifications`, `UserSubscriptionSpecifications` |
| **Domain Events** | `CityCreatedDomainEvent` |
| **Rules** | `WeatherRuleEngine` — generates alerts and recommendations from weather data |

### Application

| Artifact | Purpose |
|---|---|
| **Commands** | `CityCreateCommand`, `CityUnsubscribeCommand`, `CityIngestCommand`, `SetPrimaryCityCommand`, `ReorderCitiesCommand`, `UserProfileUpdateCommand`, `UserPreferencesUpdateCommand`, `UserDeleteCommand`, `AdminCityCreateCommand`, `AdminCityUpdateCommand`, `AdminCityDeleteCommand`, `AdminCityIngestCommand`, `AdminCityWeatherResetCommand`, `AdminUserSubscriptionUpdateCommand` |
| **Queries** | `CitySuggestionQuery`, `UserCitiesQuery`, `CityWeatherQuery`, `CitySunQuery`, `CityAlertsQuery`, `CityCompareQuery`, `CityExportQuery`, `CityWeatherExportQuery`, `CityRecommendationsQuery`, `DashboardQuery`, `UserProfileQuery`, `UserPreferencesQuery`, `UserSubscriptionQuery`, `AdminCitiesQuery`, `AdminCitySubscriptionsQuery`, `AdminUserSubscriptionsQuery`, `AdminUserSubscriptionQuery` |
| **Models (DTOs)** | `CityModel`, `UserCityModel`, `CitySuggestionModel`, `CurrentWeatherModel`, `WeatherForecastModel`, `HourlyForecastModel`, `WeatherAlertModel`, `WeatherRecommendationModel`, `DashboardModel`, `UserProfileModel`, `UnitPreferencesModel`, `UserSubscriptionModel`, `AdminCityModel`, `AdminCityCreateModel`, `AdminCityUpdateModel`, `AdminCitySubscriptionModel` |
| **Abstractions** | `IWeatherAgent` — ingests weather data; `IWeatherGeocodingClient` — city search/geocoding |
| **Tasks** | `WeatherIngestionJob` (Jobs scheduled weather ingestion), `WeatherCleanupJob` (Jobs scheduled weather retention cleanup), `CoreSubscriptionSeederTask` (seeds default subscriptions on startup) |
| **Messages** | `WeatherActivityMessage` / `WeatherActivityMessageHandler` |

### Infrastructure

| Artifact | Purpose |
|---|---|
| **EF Core** | `CoreDbContext` with entity type configurations for all aggregates |
| **Configurations** | `CityEntityTypeConfiguration`, `UserCityEntityTypeConfiguration`, `UserProfileEntityTypeConfiguration`, `UserSubscriptionEntityTypeConfiguration`, `CurrentWeatherEntityTypeConfiguration`, `WeatherForecastEntityTypeConfiguration` |
| **OpenMeteo** | `OpenMeteoClient` (HTTP), `OpenMeteoWeatherAgent` (implements `IWeatherAgent`), `WeatherGeocodingClientAdapter` (implements `IWeatherGeocodingClient`), `IOpenMeteoClient`, `GeocodingResult`, `WeatherData` |

### Presentation

| Artifact | Purpose |
|---|---|
| **Endpoints** | `CoreCityEndpoints`, `CoreWeatherEndpoints`, `CoreUserEndpoints`, `CoreDashboardEndpoints`, `CoreAdminEndpoints`, `CoreSubscriptionEndpoints` |
| **Module** | `CoreModule` — registers all services, DbContext, Jobs scheduler, startup tasks |
| **Configuration** | `CoreModuleConfiguration` with `OpenMeteoOptions` |
| **Mapping** | `CoreModuleMapperRegister` (Mapster profiles) |

## Key Patterns

### ActiveEntity (not IGenericRepository)

Domain entities extend `ActiveEntity<TEntity, TId>` and implement `IAuditable`, `IConcurrency`:

```csharp
public class City : ActiveEntity<City, CityId>, IAuditable, IConcurrency
{
    public string Name { get; set; }
    public string CountryCode { get; set; }
    public Location Location { get; set; }
    public AuditState AuditState { get; set; } = new();
    public Guid ConcurrencyVersion { get; set; }
}
```

ActiveEntity provides built-in domain event publishing, audit state tracking, and typed IDs via `[TypedEntityId<Guid>]`.

### Service Agent Abstraction

Application defines `IWeatherAgent` and `IWeatherGeocodingClient`. Infrastructure implements them with OpenMeteo HTTP calls. Business logic never depends on external APIs directly.

### IRequester Pipeline

All commands/queries flow through `IRequester` with these behaviors (in order):

1. **Metrics** — request metrics collection
2. **Tracing** — distributed tracing
3. **ModuleScope** — module-scoped service resolution
4. **DatabaseTransaction** — automatic transaction management
5. **Validation** — FluentValidation pipeline
6. **Retry** — automatic retry on transient failures
7. **Timeout** — request timeout enforcement

### Domain Specifications

Queries use `CitySpecifications` and `UserSubscriptionSpecifications` for reusable, testable query logic against the domain model.

### Subscription Plans

| Plan | Max Cities | Max Forecast Days | Comparison | Export |
|---|---|---|---|---|
| **Free** | 3 | 7 | No | No |
| **Basic** | 10 | 16 | Yes | Yes |
| **Pro** | 25 | 16 | Yes | Yes |
| **Enterprise** | Unlimited | 16 | Yes | Yes |

Each plan has a `SubscriptionPlanDetails` with `MaxCities`, `MaxForecastDays`, `AllowsComparison`, `AllowsExport`. The `SubscriptionSeederTask` assigns the configured default plan to new users.

## API Endpoints

Base URL: `https://localhost:5001`

### City Endpoints (`api/core/cities`)

| Method | Route | Description |
|---|---|---|
| GET | `/api/core/cities/suggestions?search={search}&countryCode={countryCode}` | City suggestions via geocoding |
| POST | `/api/core/cities` | Create city and subscribe current user |
| GET | `/api/core/cities` | List user's city subscriptions with weather |
| DELETE | `/api/core/cities/{cityId}` | Unsubscribe from a city |
| PUT | `/api/core/cities/{cityId}/primary` | Set primary city |
| PUT | `/api/core/cities/reorder` | Reorder city subscriptions |
| GET | `/api/core/cities/{cityId}/weather?forecastDays={days}` | Weather + forecasts for a city |
| POST | `/api/core/cities/{cityId}/ingest` | Trigger weather data ingestion |

### Weather Endpoints (`api/core/cities`)

| Method | Route | Description |
|---|---|---|
| GET | `/api/core/cities/alerts` | Weather alerts for all subscribed cities |
| GET | `/api/core/cities/{cityId}/sun?days={days}` | Sunrise/sunset data |
| POST | `/api/core/cities/compare` | Compare weather across cities (body: city IDs) |
| GET | `/api/core/cities/export` | Export all subscribed cities weather as CSV |
| GET | `/api/core/cities/{cityId}/weather/export?days={days}` | Export forecast for specific city |
| GET | `/api/core/cities/{cityId}/recommendations` | Weather recommendations |

### User Endpoints (`api/core/users`)

| Method | Route | Description |
|---|---|---|
| GET | `/api/core/users/me` | Get current user profile |
| PUT | `/api/core/users/me` | Update profile |
| GET | `/api/core/users/preferences` | Get unit preferences |
| PUT | `/api/core/users/preferences` | Update unit preferences |
| DELETE | `/api/core/users/me` | Soft-delete user and all data |

### Dashboard Endpoint

| Method | Route | Description |
|---|---|---|
| GET | `/api/core/dashboard` | Full dashboard: cities, highlights, alerts, recommendations |

### Admin Endpoints (`api/core/admin/cities`) — requires `CoreAdmin` role

| Method | Route | Description |
|---|---|---|
| POST | `/api/core/admin/cities` | Create city (no geocoding) |
| PUT | `/api/core/admin/cities/{cityId}` | Update city details |
| DELETE | `/api/core/admin/cities/{cityId}` | Hard-delete city + all data |
| GET | `/api/core/admin/cities` | List all cities with subscription counts |
| GET | `/api/core/admin/cities/{cityId}/subscriptions` | List city subscriptions (incl. soft-deleted) |
| POST | `/api/core/admin/cities/{cityId}/ingest` | Ingest weather (no subscription check) |
| DELETE | `/api/core/admin/cities/{cityId}/weather` | Delete all weather data for city |

### Subscription Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/core/users/subscription` | Get current user's subscription |
| GET | `/api/core/admin/subscriptions` | List all subscriptions (admin) |
| GET | `/api/core/admin/subscriptions/{userId}` | Get user subscription (admin) |
| PUT | `/api/core/admin/subscriptions/{userId}` | Update user subscription (admin) |

## Configuration

`CoreModuleConfiguration` (bound from `appsettings.json` section `CoreModule`):

```json
{
  "CoreModule": {
    "ConnectionStrings": {
      "Default": "Server=...;Database=WeatherFiesta;..."
    },
    "SeederTaskStartupDelay": "00:00:05",
    "StaleThresholdMinutes": 60,
    "Jobs": {
      "IngestionCron": "*/30 * * * *",
      "CleanupCron": "0 2 * * *",
      "CleanupRetentionDays": 31
    },
    "ForecastDays": 16,
    "GeocodingMinQueryLength": 3,
    "ComparisonMaxCities": 10,
    "DefaultPlan": "Free",
    "OpenMeteo": {
      "GeocodingBaseUrl": "https://geocoding-api.open-meteo.com/v1",
      "ForecastBaseUrl": "https://api.open-meteo.com/v1/forecast",
      "LookupBaseUrl": "https://geocoding-api.open-meteo.com/v1/get",
      "TimeoutSeconds": 10,
      "RetryCount": 3,
      "RetryDelayMs": 1000,
      "InterCallDelayMs": 100
    }
  }
}
```

## Running the App

```bash
# From repo root
dotnet run --project examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server

# Or via HTTPS
dotnet run --project examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server --urls https://localhost:5001
```

### FakeIdentityProvider

In Development mode, a `FakeIdentityProvider` is registered with Star Wars test users. Token endpoint:

```
POST /_bdk/api/identity/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=luke.skywalker&password=password&client_id=weatherfiesta-api&scope=openid profile email offline_access
```

Available users: `luke.skywalker`, `leia.organa`, `han.solo`, `darth.vader`, etc. (all with password `password`).

### API Documentation

- OpenAPI spec: `https://localhost:5001/openapi.json`
- Scalar UI: `https://localhost:5001/scalar/`

### Profiling

Development enables Runtime, Operation and Request Profiling with the in-memory provider. The dashboard keeps its existing OpenID Connect authentication and Administrators role requirement. Profiling and its dashboard are disabled outside Development.

The registration in [Program.cs](WeatherFiesta.Presentation.Web.Server/Program.cs) is:

```csharp
builder.Services.AddProfiling(o => o.Enabled(builder.Environment.IsDevelopment()))
	.WithRuntimeProfiling()
	.WithOperationProfiling()
	.WithRequestProfiling(o => o.StripPathPrefix("/api"))
	.AddConsoleCommands(builder.Environment.IsDevelopment());
```

Eligible API requests are captured by default. The shared defaults select in-memory storage and exclude `/_bdk/**`, `/health*`, `/swagger/**`, `/scalar/**` and `/openapi/**`. The outer middleware follows request correlation; the exception observer runs after exception handling and before routing. The default operation key strips `/api`, while the recorded HTTP path retains it. Completed records become visible after periodic persistence; dashboard reads do not force a flush. In-memory history belongs to this process and is lost when it exits.

Core's seven Active Entity registrations include `AddProfilingBehavior()`. Entity operations contribute segments such as `activeentity:City:FindAll` to the current HTTP or job operation. The compare endpoint's `Query` segment contains the entity work performed by its handler. Outside an active operation, entity calls own independent operations. Disabled or omitted Profiling registration leaves entity execution unchanged.

1. Sign in to `/_bdk/dashboard` as an administrator. Runtime, Operations and Requests are separate profiling views.
2. Call `GET /api/core/cities/alerts`, then copy its `X-Request-Profiling-Id` response header into **Find exact ID** on the Requests view. The record includes the default `/core/cities/alerts` key, HTTP status/method, duration and response-byte quality.
3. With a subscription that allows comparison, call `POST /api/core/cities/compare` with a JSON array of city IDs. [WeatherEndpoints.cs](WeatherFiesta.Presentation.Web.Server/Modules/Core/Endpoints/WeatherEndpoints.cs) sets `weather:compare`, the numeric `cityCount` dimension and a `Query` segment. Group by `cityCount` to compare matching workloads. A rejected comparison also records the failed segment while preserving its business response.
4. Start a Runtime profiling session and manually dispatch `core_profiling_stress` from the Jobs dashboard. The development-only [WeatherProfilingStressJob](WeatherFiesta.Presentation.Web.Server/Modules/Core/Jobs/WeatherProfilingStressJob.cs) owns or joins `weather:profiling-stress` and records `Cpu`, `Allocate` and `Retain` segments alongside its Runtime measurement. Its operation detail relates Runtime observations by executing-process identity and timestamps. Runtime metrics describe the process, not CPU or memory attributed to that operation.

The workload deliberately generates CPU, allocation and GC pressure; dispatch it only when that load is intended. It is a manual example, not an automatic production job. Shared profiling contracts, defaults and provider configuration are documented in the [application-neutral profiling guide](../../docs/features-profiling.md).

## Testing

Integration tests in `WeatherFiesta.IntegrationTests` use:

- **Isolated SQL Server databases** — Docker-backed fixtures give each application factory its own database; the factory also supports an InMemory fallback
- **No mocks for internal logic** — real handlers, ActiveEntity, and IRequester pipeline execute
- **Only external HTTP services mocked** — `IWeatherAgent` and `IWeatherGeocodingClient` via NSubstitute
- **Test authentication** — `TestAuthenticationHandler` returns a fully authenticated user with `CoreAdmin` role
- **Seeded test data** — `TestData.SeedAsync` populates the isolated database

[ProfilingEndpointsTests.cs](WeatherFiesta.IntegrationTests/Modules/Core/Presentation/ProfilingEndpointsTests.cs) verifies default and enriched HTTP records, natural periodic persistence, the non-HTTP stress job and Runtime overlay, blacklist exclusion, disabled-host behavior, and authorization on all profiling views and internal reads. Its bounded workload and test authentication are fixture-only; the application retains its normal authentication configuration.

```bash
# Run integration tests
dotnet test examples/WeatherFiesta/WeatherFiesta.IntegrationTests
```

## Project Structure

```
WeatherFiesta/
├── WeatherFiesta.Domain/
│   └── Modules/
│       └── Core/
│           ├── Events/
│           │   └── CityCreatedDomainEvent.cs
│           ├── Model/
│           │   ├── City.cs
│           │   ├── CurrentWeather.cs
│           │   ├── Enumerations.cs
│           │   ├── HourlyForecast.cs
│           │   ├── Location.cs
│           │   ├── UserCity.cs
│           │   ├── UserProfile.cs
│           │   ├── UserSubscription.cs
│           │   ├── WeatherAlert.cs
│           │   ├── WeatherForecast.cs
│           │   └── WeatherRecommendation.cs
│           ├── Rules/
│           │   └── WeatherRuleEngine.cs
│           └── Specifications/
│               ├── CitySpecifications.cs
│               └── UserSubscriptionSpecifications.cs
├── WeatherFiesta.Application/
│   └── Modules/
│       └── Core/
│           ├── Abstractions/
│           │   ├── IWeatherAgent.cs
│           │   └── IWeatherGeocodingClient.cs
│           ├── Commands/
│           │   ├── AdminCityCreateCommand.cs
│           │   ├── AdminCityDeleteCommand.cs
│           │   ├── AdminCityIngestCommand.cs
│           │   ├── AdminCityUpdateCommand.cs
│           │   ├── AdminCityWeatherResetCommand.cs
│           │   ├── AdminUserSubscriptionUpdateCommand.cs
│           │   ├── CityCreateCommand.cs
│           │   ├── CityIngestCommand.cs
│           │   ├── CityUnsubscribeCommand.cs
│           │   ├── ReorderCitiesCommand.cs
│           │   ├── SetPrimaryCityCommand.cs
│           │   ├── UserDeleteCommand.cs
│           │   ├── UserPreferencesUpdateCommand.cs
│           │   └── UserProfileUpdateCommand.cs
│           ├── Messages/
│           │   ├── WeatherActivityMessage.cs
│           │   └── WeatherActivityMessageHandler.cs
│           ├── Models/
│           │   ├── AdminCityCreateModel.cs
│           │   ├── AdminCityModel.cs
│           │   ├── AdminCitySubscriptionModel.cs
│           │   ├── AdminCityUpdateModel.cs
│           │   ├── CityModel.cs
│           │   ├── CitySuggestionModel.cs
│           │   ├── CurrentWeatherModel.cs
│           │   ├── DashboardModel.cs
│           │   ├── HourlyForecastModel.cs
│           │   ├── UnitPreferencesModel.cs
│           │   ├── UserCityModel.cs
│           │   ├── UserProfileModel.cs
│           │   ├── UserSubscriptionModel.cs
│           │   ├── WeatherAlertModel.cs
│           │   ├── WeatherForecastModel.cs
│           │   └── WeatherRecommendationModel.cs
│           ├── Queries/
│           │   ├── AdminCitiesQuery.cs
│           │   ├── AdminCitySubscriptionsQuery.cs
│           │   ├── AdminUserSubscriptionQuery.cs
│           │   ├── AdminUserSubscriptionsQuery.cs
│           │   ├── CityAlertsQuery.cs
│           │   ├── CityCompareQuery.cs
│           │   ├── CityExportQuery.cs
│           │   ├── CityRecommendationsQuery.cs
│           │   ├── CitySuggestionQuery.cs
│           │   ├── CitySunQuery.cs
│           │   ├── CityWeatherExportQuery.cs
│           │   ├── CityWeatherQuery.cs
│           │   ├── DashboardQuery.cs
│           │   ├── UserCitiesQuery.cs
│           │   ├── UserPreferencesQuery.cs
│           │   ├── UserProfileQuery.cs
│           │   └── UserSubscriptionQuery.cs
│           ├── Jobs/
│           │   ├── WeatherCleanupJob.cs
│           │   └── WeatherIngestionJob.cs
│           └── Tasks/
│               └── CoreSeederTask.cs
├── WeatherFiesta.Infrastructure/
│   └── Modules/
│       └── Core/
│           ├── EntityFramework/
│           │   ├── Configurations/
│           │   │   ├── CityEntityTypeConfiguration.cs
│           │   │   ├── CurrentWeatherEntityTypeConfiguration.cs
│           │   │   ├── UserCityEntityTypeConfiguration.cs
│           │   │   ├── UserProfileEntityTypeConfiguration.cs
│           │   │   ├── UserSubscriptionEntityTypeConfiguration.cs
│           │   │   └── WeatherForecastEntityTypeConfiguration.cs
│           │   ├── CoreDbContext.cs
│           │   └── CoreDbContextFactory.cs
│           └── OpenMeteo/
│               ├── GeocodingResult.cs
│               ├── IOpenMeteoClient.cs
│               ├── OpenMeteoClient.cs
│               ├── OpenMeteoWeatherAgent.cs
│               ├── WeatherData.cs
│               └── WeatherGeocodingClientAdapter.cs
├── WeatherFiesta.Presentation.Web.Server/
│   ├── Modules/
│   │   └── Core/
│   │       ├── CoreModule.cs
│   │       ├── CoreModuleMapperRegister.cs
│   │       └── Endpoints/
│   │           ├── CoreAdminEndpoints.cs
│   │           ├── CoreCityEndpoints.cs
│   │           ├── CoreDashboardEndpoints.cs
│   │           ├── CoreSubscriptionEndpoints.cs
│   │           ├── CoreUserEndpoints.cs
│   │           └── CoreWeatherEndpoints.cs
│   ├── Program.cs
│   └── ProgramExtensions.cs
└── WeatherFiesta.IntegrationTests/
    ├── ApplicationCommandTests.cs
    ├── ApplicationQueryTests.cs
    ├── CoreAdminEndpointsTests.cs
    ├── CoreCityEndpointsTests.cs
    ├── CoreDashboardEndpointsTests.cs
    ├── CoreSubscriptionEndpointsTests.cs
    ├── CoreUserEndpointsTests.cs
    ├── CoreWeatherEndpointsTests.cs
    ├── WeatherFiestaApplicationFactory.cs
    └── TestData.cs
```
