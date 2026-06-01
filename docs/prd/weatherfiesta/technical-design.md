# WeatherFiesta Technical Design Document

## 1. Introduction

### 1.1 Purpose

This document defines the technical architecture for WeatherFiesta, a weather dashboard application built on the bITdevKit framework. It translates the product requirements (11 PRDs, 41 stories) and architectural decisions (6 ADRs) into concrete implementation guidance.

### 1.2 Scope

WeatherFiesta is a single-module application within a bITdevKit modular monolith. It provides:
- City subscription management (geocoding, subscribe, unsubscribe, reorder, primary)
- Weather data viewing (current, forecast, hourly, sun times, comparison, export)
- Data ingestion from Open-Meteo (scheduled and on-demand)
- Dashboard aggregation (primary city, highlights, alerts, recommendations)
- User profile and unit preferences
- Admin city management

### 1.3 Audience

Developers implementing WeatherFiesta, architects reviewing the design, and QA engineers writing tests.

### 1.4 Goals

- Map every PRD story to concrete types, endpoints, and database tables
- Ensure consistent use of bITdevKit patterns (Requester/Notifier, Pipeline, Result, Typed IDs)
- Define clear boundaries between vertical slices within the single module
- Provide implementation guidance for cross-cutting concerns (auth, staleness, unit preferences, soft-delete)

### 1.5 Non-goals

- Frontend/UI design or implementation
- Deployment infrastructure or CI/CD pipelines
- Mobile app architecture
- Push notification delivery (v2 consideration)

### 1.6 Assumptions

- The application runs within an existing bITdevKit modular monolith host
- Authentication is handled by the host application (ASP.NET Core Identity or external provider)
- SQL Server is the database engine
- Open-Meteo API is free and requires no API key
- The host provides Serilog structured logging

### 1.7 Constraints

- Single module boundary (ADR-0001) — no cross-module references
- Clean Onion layering (ADR-0001) — Domain has no outer dependencies
- API always returns metric data (ADR-0003) — frontend handles unit conversion
- Staleness computed at query time (ADR-0002) — no stored staleness flag
- Alerts and recommendations computed at query time (ADR-0005) — no database storage
- Pipeline pattern for city creation and ingestion (ADR-0004) — no orchestration for v1

## 2. Requirements Summary

### 2.1 Functional Requirements

| Slice | Stories | Key Capabilities |
|-------|---------|-----------------|
| CITIES | 8 | Geocoding, subscribe, unsubscribe, reactivate, primary city, reorder |
| WEATHER | 14 | Current weather, forecast, hourly, alerts, sun times, comparison, export |
| INGESTION | 3 | Scheduled ingestion, on-demand ingestion, admin reset |
| ADMIN | 4 | City CRUD, view subscriptions, trigger ingestion, list cities |
| USER | 4 | View/update profile, unit preferences, delete account |
| DASHBOARD | 5 | Dashboard view, primary highlight, cross-city highlights, alert summary, recommendation highlights |
| RECOMMENDATIONS | 3 | Per-city recommendations, 7 rules, categories & severity |

### 2.2 Non-Functional Requirements

| Category | Requirement | Target |
|----------|-------------|--------|
| Performance | Geocoding suggestions | < 2s |
| Performance | Weather data response | < 500ms |
| Performance | Dashboard aggregation | < 500ms |
| Performance | Recommendation computation | < 100ms |
| Performance | Sun times response | < 200ms |
| Performance | City reorder | < 200ms for 50 cities |
| Performance | CSV export | < 2s for 50 cities |
| Performance | Ingestion per city | < 10s |
| Reliability | Ingestion retry | 3 retries with 1s backoff |
| Reliability | Stale data serving | Cached data + warning when API down |
| Security | User endpoints | Authenticated, subscription-verified |
| Security | Admin endpoints | Authenticated, admin role |
| Security | Data isolation | Users can only access their own subscriptions |
| Availability | Open-Meteo down | Serve cached data with staleDataWarning |

## 3. Architectural Drivers

| Driver | Impact |
|--------|--------|
| Single module with shared City aggregate | All slices share DbContext, no integration events between slices |
| Query-time staleness computation | No background staleness job, RetrievedAt timestamp on every weather record |
| Query-time alert/recommendation computation | No Alert/Recommendation tables, shared WeatherRuleEngine |
| API returns metric only | No server-side unit conversion, preferences as metadata |
| Pipeline pattern for city creation | Composable steps, testable, idempotent |
| Soft-delete for UserCity | Reactivation on re-subscribe, DisplayOrder gap closing |
| Hard-delete for City (admin) | Cascading delete of weather data and subscriptions |
| Open-Meteo free API | No API key, rate limit ~10,000 requests/day |

## 4. System Context

```
┌─────────────────────────────────────────────────────────┐
│                     Core Module                          │
│                                                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────┐ │
│  │  CITIES   │  │ WEATHER  │  │INGESTION │  │ ADMIN  │ │
│  │  Slice    │  │  Slice   │  │  Slice   │  │ Slice  │ │
│  └─────┬─────┘  └────┬─────┘  └────┬─────┘  └───┬────┘ │
│        │              │             │             │       │
│  ┌─────┴──────────────┴─────────────┴─────────────┴────┐ │
│  │              CoreDbContext                            │ │
│  │   City | UserCity | CurrentWeather | WeatherForecast │ │
│  │   UserProfile | Outbox                               │ │
│  └──────────────────────────┬──────────────────────────┘ │
│                             │                             │
│  ┌──────────────────────────┴──────────────────────────┐ │
│  │              Open-Meteo Integration                   │ │
│  │   Geocoding API | Forecast API | Lookup API          │ │
│  └──────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
         │                    │
         ▼                    ▼
   ┌──────────┐       ┌──────────────┐
   │  Host App │       │  SQL Server  │
   │  (Auth,   │       │  Database    │
   │  Logging) │       └──────────────┘
   └──────────┘
```

**Inside the system**: Core module, its DbContext, Open-Meteo client, Quartz scheduler.

**Outside the system**: Host application (authentication, logging, middleware), SQL Server database, Open-Meteo API (external, free, no API key).

## 5. Architecture Overview

WeatherFiesta follows the bITdevKit Clean Onion architecture within a single module:

```
┌─────────────────────────────────────────────────────┐
│                  Presentation Layer                   │
│  Minimal API endpoints (27 endpoints)                │
│  Route prefix: api/core/                             │
│  Depends on: Application                              │
├─────────────────────────────────────────────────────┤
│                  Application Layer                    │
│  Commands, Queries, Handlers, Specifications, DTOs  │
│  Pipeline steps, Validators, Rule engines           │
│  Depends on: Domain                                  │
├─────────────────────────────────────────────────────┤
│                  Domain Layer                        │
│  Aggregates, Entities, Value Objects, Enumerations   │
│  Domain Events, Business Rules                      │
│  Depends on: Nothing (only bITdevKit abstractions)  │
├─────────────────────────────────────────────────────┤
│                  Infrastructure Layer                │
│  DbContext, Repositories, Open-Meteo Client          │
│  Quartz Jobs, EF Configurations, Migrations          │
│  Depends on: Domain + Application                    │
└─────────────────────────────────────────────────────┘
```

**Vertical slices** within the Application and Presentation layers:

```
Core.Application/
├── CITIES/           # Commands, Queries, Handlers, Specs
├── WEATHER/          # Commands, Queries, Handlers, Specs
├── INGESTION/        # Pipeline steps, Handlers, Specs
├── ADMIN/            # Commands, Queries, Handlers, Specs
├── USER/             # Commands, Queries, Handlers, Specs
├── DASHBOARD/        # Queries, Handlers, Specs
└── RECOMMENDATIONS/  # Queries, Handlers, Specs
```

## 6. Terminology

| Term | Definition |
|------|-----------|
| City | Global shared entity representing a geographic location with weather data |
| UserCity | Per-user subscription linking a user to a city, with IsPrimary and DisplayOrder |
| CurrentWeather | Single record per city, upserted every 30 minutes, with RetrievedAt timestamp |
| WeatherForecast | One record per city per day (up to 16 days), with HourlyForecasts as JSON column |
| UserProfile | User identity and preferences (temperatureUnit, windSpeedUnit) |
| StaleDataWarning | Computed flag: true when RetrievedAt > 60 minutes ago |
| WeatherRuleEngine | Shared domain service that evaluates alert and recommendation rules |
| CityCreatePipeline | 5-step pipeline: Geocode → CheckExists → PersistCity → PersistUserCity → EnqueueIngestion |
| IngestWeatherPipeline | 4-step pipeline: FetchCurrent → UpsertCurrent → FetchForecast → UpsertForecasts |
| DisplayOrder | Integer on UserCity controlling list order; gaps closed on unsubscribe |
| CityCreatedDomainEvent | Domain event published when a new City is created. Payload: CityId, Latitude, Longitude, TimeZone. Published via outbox for reliable delivery. Triggers weather ingestion. |
| IsPrimary | Boolean on UserCity; exactly one per user; always first in list |
| Subscription | Per-user entity linking a user to a plan with status and billing cycle. Each user has exactly one subscription. |
| SubscriptionPlan | Smart enumeration defining plan tiers (Free, Basic, Pro, Enterprise) with usage limits (MaxCities, MaxForecastDays, AllowsComparison, AllowsExport) |
| SubscriptionStatus | Smart enumeration: Pending, Active, Cancelled, Expired |
| SubscriptionBillingCycle | Smart enumeration: Never, Monthly, Yearly |

## 7. Components and Responsibilities

| Component | Responsibility | Layer |
|-----------|---------------|-------|
| City (Aggregate) | Global city entity with geocoding data, dedup keys | Domain |
| UserCity (Entity) | Per-user subscription with order and primary flag | Domain |
| CurrentWeather (Entity) | Latest weather snapshot per city | Domain |
| WeatherForecast (Entity) | Daily forecast with hourly JSON | Domain |
| UserProfile (Entity) | User identity and unit preferences | Domain |
| WeatherCode (Smart Enum) | WMO code → description mapping | Domain |
| WeatherRuleEngine | Alert and recommendation rule evaluation | Domain |
| CityCreateCommand | Subscribe to city (triggers pipeline) | Application |
| IngestWeatherCommand | Fetch and store weather data (triggers pipeline) | Application |
| 27 endpoint handlers | Request/response orchestration | Application |
| OpenMeteoClient | HTTP client for Open-Meteo APIs | Infrastructure |
| CoreDbContext | EF Core DbContext for all entities | Infrastructure |
| Quartz ingestion job | Scheduled 30-minute weather ingestion | Infrastructure |
| CoreModule | Module registration (WebModuleBase) | Presentation |
| 27 minimal API endpoints | HTTP request/response mapping | Presentation |

### 7.5 API Endpoint Mapping

#### User Endpoints (20)

| Method | Route | Command/Query | Handler | Auth | PRD |
|--------|-------|---------------|---------|------|-----|
| GET | /api/core/cities/suggestions | CitySuggestionQuery | CitySuggestionQueryHandler | User | PRD-0000 S1 |
| POST | /api/core/cities | CityCreateCommand | CityCreateCommandHandler (via Pipeline) | User | PRD-0000 S2 |
| GET | /api/core/cities | UserCitiesQuery | UserCitiesQueryHandler | User | PRD-0000 S3 |
| DELETE | /api/core/cities/{cityId} | CityUnsubscribeCommand | CityUnsubscribeCommandHandler | User | PRD-0000 S4 |
| PUT | /api/core/cities/{cityId}/primary | SetPrimaryCityCommand | SetPrimaryCityCommandHandler | User | PRD-0000 S5 |
| PUT | /api/core/cities/reorder | ReorderCitiesCommand | ReorderCitiesCommandHandler | User | PRD-0001 S1 |
| GET | /api/core/cities/{cityId}/weather | CityWeatherQuery | CityWeatherQueryHandler | User | PRD-0100 S1-S3 |
| POST | /api/core/cities/{cityId}/ingest | CityIngestCommand | CityIngestCommandHandler | User | PRD-0100 S4 |
| GET | /api/core/cities/alerts | CityAlertsQuery | CityAlertsQueryHandler | User | PRD-0100 S5 |
| GET | /api/core/cities/{cityId}/sun | CitySunQuery | CitySunQueryHandler | User | PRD-0101 S1-S3 |
| GET | /api/core/cities/compare | CityCompareQuery | CityCompareQueryHandler | User | PRD-0102 S1-S3 |
| GET | /api/core/cities/export | CityExportQuery | CityExportQueryHandler | User | PRD-0103 S1 |
| GET | /api/core/cities/{cityId}/weather/export | CityWeatherExportQuery | CityWeatherExportQueryHandler | User | PRD-0103 S2 |
| GET | /api/core/cities/{cityId}/recommendations | CityRecommendationsQuery | CityRecommendationsQueryHandler | User | PRD-0600 S1 |
| GET | /api/core/dashboard | DashboardQuery | DashboardQueryHandler | User | PRD-0500 S1-S5 |
| GET | /api/core/users/me | UserProfileQuery | UserProfileQueryHandler | User | PRD-0400 S1 |
| PUT | /api/core/users/me | UserProfileUpdateCommand | UserProfileUpdateCommandHandler | User | PRD-0400 S2 |
| GET | /api/core/users/preferences | UserPreferencesQuery | UserPreferencesQueryHandler | User | PRD-0400 S3 |
| PUT | /api/core/users/preferences | UserPreferencesUpdateCommand | UserPreferencesUpdateCommandHandler | User | PRD-0400 S3 |
| DELETE | /api/core/users/me | UserDeleteCommand | UserDeleteCommandHandler | User | PRD-0400 S4 |
| GET | /api/core/users/subscription | UserSubscriptionQuery | UserSubscriptionQueryHandler | User | PRD-0700 S2 |

#### Admin Endpoints (7)

| Method | Route | Command/Query | Handler | Auth | PRD |
|--------|-------|---------------|---------|------|-----|
| POST | /api/core/admin/cities | AdminCityCreateCommand | AdminCityCreateCommandHandler | Admin | PRD-0300 S1 |
| PUT | /api/core/admin/cities/{cityId} | AdminCityUpdateCommand | AdminCityUpdateCommandHandler | Admin | PRD-0300 S1 |
| DELETE | /api/core/admin/cities/{cityId} | AdminCityDeleteCommand | AdminCityDeleteCommandHandler | Admin | PRD-0300 S1 |
| GET | /api/core/admin/cities | AdminCitiesQuery | AdminCitiesQueryHandler | Admin | PRD-0300 S4 |
| GET | /api/core/admin/cities/{cityId}/subscriptions | AdminCitySubscriptionsQuery | AdminCitySubscriptionsQueryHandler | Admin | PRD-0300 S2 |
| POST | /api/core/admin/cities/{cityId}/ingest | AdminCityIngestCommand | AdminCityIngestCommandHandler | Admin | PRD-0300 S3 |
| DELETE | /api/core/admin/cities/{cityId}/weather | AdminCityWeatherResetCommand | AdminCityWeatherResetCommandHandler | Admin | PRD-0200 S3 |
| GET | /api/core/admin/subscriptions | AdminSubscriptionsQuery | AdminSubscriptionsQueryHandler | Admin | PRD-0700 S3 |
| GET | /api/core/admin/subscriptions/{userId} | AdminSubscriptionQuery | AdminSubscriptionQueryHandler | Admin | PRD-0700 S3 |
| PUT | /api/core/admin/subscriptions/{userId} | AdminSubscriptionUpdateCommand | AdminSubscriptionUpdateCommandHandler | Admin | PRD-0700 S3 |

## 8. Runtime Architecture

### 8.1 City Creation Flow

```
User → POST /api/core/cities
  → Endpoint → IRequester.SendAsync(CityCreateCommand)
    → ValidationPipelineBehavior
      → CityCreatePipeline
        → GeocodeCityStep (Open-Meteo Geocoding API)
        → CheckCityExistsStep (dedup by ExternalId or Lat/Lng)
        → PersistCityStep (save City aggregate)
        → PersistUserCityStep (save UserCity subscription)
        → EnqueueIngestionStep (queue IngestWeatherCommand)
    ← Result<CityCreateResult>
  ← 201 Created
```

### 8.2 Weather Ingestion Flow

```
Quartz Job (every 30 min) OR Manual Trigger
  → IRequester.SendAsync(IngestWeatherCommand)
    → RetryPipelineBehavior (3 retries, 1s backoff)
      → IngestWeatherPipeline
        → FetchCurrentWeatherStep (Open-Meteo Forecast API)
        → UpsertCurrentWeatherStep (upsert by CityId)
        → FetchDailyForecastStep (Open-Meteo Forecast API)
        → UpsertForecastsStep (upsert by CityId + ForecastDate)
      ← Result<IngestWeatherResult>
```

### 8.3 Dashboard Read Flow

```
User → GET /api/core/dashboard
  → Endpoint → IRequester.SendAsync(GetDashboardQuery)
    → Handler
      → Load UserCity subscriptions (with IsPrimary, DisplayOrder)
      → Load CurrentWeather for subscribed cities
      → Compute staleness (RetrievedAt > 60 min)
      → Compute highlights (warmest, coldest, wettest, windiest)
      → Compute alerts (WeatherRuleEngine.EvaluateAlerts)
      → Compute recommendations (WeatherRuleEngine.EvaluateRecommendations)
      → Load UserProfile for unit preferences
    ← DashboardResponse
  ← 200 OK
```

### 8.4 Admin City Hard-Delete Flow

```
Admin → DELETE /api/core/admin/cities/{cityId}
  → Endpoint → IRequester.SendAsync(AdminDeleteCityCommand)
    → Handler
      → Verify admin role
      → Load City aggregate
      → Delete CurrentWeather records (by CityId)
      → Delete WeatherForecast records (by CityId)
      → Delete UserCity records (by CityId)
      → Delete City aggregate
    ← Result<Success>
  ← 204 No Content
```

### 8.5 Staleness Computation

```
On every weather-related read:
  var isStale = weather.RetrievedAt < DateTime.UtcNow - WeatherConstants.StaleThreshold;
  if (isStale) {
      response.StaleDataWarning = true;
      response.StaleDataWarningMessage = $"Data may be outdated — last updated {minutesSinceUpdate} minutes ago";
  }
  // When fresh, StaleDataWarning is omitted (not false)
```

### 8.6 Unit Preferences Metadata

```
On every weather-related response:
  var preferences = await userProfileRepository.GetPreferencesAsync(userId);
  response.UnitPreferences = new UnitPreferencesDto {
      TemperatureUnit = preferences.TemperatureUnit,  // Celsius or Fahrenheit
      WindSpeedUnit = preferences.WindSpeedUnit        // Kmh, Mph, Ms, or Knots
  };
  // All numeric values remain in metric units
```

## 9. Data/Persistence Architecture

### 9.1 Database Schema

#### Cities Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | uniqueidentifier | PK, NOT NULL | Typed as CityId |
| Name | nvarchar(200) | NOT NULL | City display name |
| Country | nvarchar(100) | NOT NULL | Full country name |
| CountryCode | nvarchar(2) | NOT NULL | ISO 3166-1 alpha-2 |
| TimeZone | nvarchar(50) | NOT NULL | IANA timezone ID |
| Latitude | decimal(10,7) | NOT NULL | Range: -90 to 90 |
| Longitude | decimal(10,7) | NOT NULL | Range: -180 to 180 |
| Elevation | decimal(8,2) | NULL | Meters above sea level |
| ExternalId | bigint | NULL, UNIQUE | Open-Meteo geocoding ID |
| CreatedAt | datetime2 | NOT NULL | UTC |
| UpdatedAt | datetime2 | NOT NULL | UTC |
| RowVersion | rowversion | NOT NULL | Optimistic concurrency |

**Indexes**: IX_Cities_ExternalId (UNIQUE), IX_Cities_Latitude_Longitude (filtered, tolerance handled in code)

**Note**: The `admin1` field from Open-Meteo geocoding results (PRD-0000 S1 AC5) is returned in the suggestions response but NOT persisted in the City table. It is only needed for the geocoding search response and is not required for weather data or dashboard queries. If admin1 is needed for subscribed cities in the future, a migration can add it.

#### UserCities Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | uniqueidentifier | PK, NOT NULL | Typed as UserCityId |
| UserId | uniqueidentifier | NOT NULL | FK to UserProfile |
| CityId | uniqueidentifier | NOT NULL | FK to Cities |
| IsPrimary | bit | NOT NULL, DEFAULT 0 | Exactly one per user |
| DisplayOrder | int | NOT NULL, DEFAULT 0 | 0-based ordering |
| IsDeleted | bit | NOT NULL, DEFAULT 0 | Soft-delete flag |
| DeleteReason | nvarchar(500) | NULL | Optional reason |
| CreatedAt | datetime2 | NOT NULL | UTC |
| UpdatedAt | datetime2 | NOT NULL | UTC |
| RowVersion | rowversion | NOT NULL | Optimistic concurrency |

**Indexes**: IX_UserCities_UserId_CityId (UNIQUE, filtered WHERE IsDeleted = 0), IX_UserCities_UserId_DisplayOrder (filtered WHERE IsDeleted = 0)

#### CurrentWeather Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | uniqueidentifier | PK, NOT NULL | Typed as CurrentWeatherId |
| CityId | uniqueidentifier | NOT NULL | FK to Cities, UNIQUE |
| Temperature | decimal(5,2) | NOT NULL | Celsius |
| ApparentTemperature | decimal(5,2) | NOT NULL | Celsius |
| Humidity | int | NOT NULL | Percentage |
| WeatherCode | int | NOT NULL | WMO code |
| WindSpeed | decimal(5,2) | NOT NULL | km/h |
| WindDirection | int | NOT NULL | Degrees |
| WindGusts | decimal(5,2) | NOT NULL | km/h |
| Precipitation | decimal(5,3) | NOT NULL | mm |
| CloudCover | int | NOT NULL | Percentage |
| Pressure | decimal(7,2) | NOT NULL | hPa |
| RetrievedAt | datetime2 | NOT NULL | UTC, used for staleness |
| CreatedAt | datetime2 | NOT NULL | UTC |
| UpdatedAt | datetime2 | NOT NULL | UTC |
| RowVersion | rowversion | NOT NULL | Optimistic concurrency |

**Indexes**: IX_CurrentWeather_CityId (UNIQUE)

#### WeatherForecasts Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | uniqueidentifier | PK, NOT NULL | Typed as WeatherForecastId |
| CityId | uniqueidentifier | NOT NULL | FK to Cities |
| ForecastDate | date | NOT NULL | Local date for the city's timezone |
| DayWeatherCode | int | NOT NULL | WMO code |
| TemperatureMax | decimal(5,2) | NOT NULL | Celsius |
| TemperatureMin | decimal(5,2) | NOT NULL | Celsius |
| ApparentTemperatureMax | decimal(5,2) | NOT NULL | Celsius |
| ApparentTemperatureMin | decimal(5,2) | NOT NULL | Celsius |
| PrecipitationSum | decimal(5,3) | NOT NULL | mm |
| PrecipitationProbabilityMax | int | NOT NULL | Percentage |
| WindSpeedMax | decimal(5,2) | NOT NULL | km/h |
| WindGustsMax | decimal(5,2) | NOT NULL | km/h |
| DominantWindDirection | int | NOT NULL | Degrees |
| UvIndexMax | decimal(3,1) | NOT NULL | UV index |
| SunshineDurationSeconds | int | NOT NULL | Seconds |
| DaylightDurationSeconds | int | NOT NULL | Seconds |
| Sunrise | datetime2 | NOT NULL | UTC |
| Sunset | datetime2 | NOT NULL | UTC |
| HourlyForecasts | nvarchar(max) | NOT NULL | JSON column |
| RetrievedAt | datetime2 | NOT NULL | UTC, used for staleness |
| CreatedAt | datetime2 | NOT NULL | UTC |
| UpdatedAt | datetime2 | NOT NULL | UTC |
| RowVersion | rowversion | NOT NULL | Optimistic concurrency |

**Indexes**: IX_WeatherForecasts_CityId_ForecastDate (UNIQUE)

#### UserProfiles Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | uniqueidentifier | PK, NOT NULL | Typed as UserProfileId, same as UserId |
| Email | nvarchar(256) | NOT NULL | |
| Name | nvarchar(200) | NOT NULL | |
| TemperatureUnit | int | NOT NULL, DEFAULT 0 | Enumeration: 0=Celsius, 1=Fahrenheit |
| WindSpeedUnit | int | NOT NULL, DEFAULT 0 | Enumeration: 0=Kmh, 1=Mph, 2=Ms, 3=Knots |
| IsDeleted | bit | NOT NULL, DEFAULT 0 | Soft-delete flag |
| CreatedAt | datetime2 | NOT NULL | UTC |
| UpdatedAt | datetime2 | NOT NULL | UTC |
| RowVersion | rowversion | NOT NULL | Optimistic concurrency |

**Indexes**: IX_UserProfiles_Email (UNIQUE, filtered WHERE IsDeleted = 0)

#### OutboxMessages Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | uniqueidentifier | PK, NOT NULL | |
| Type | nvarchar(200) | NOT NULL | Event type (e.g., CityCreatedDomainEvent) |
| Data | nvarchar(max) | NOT NULL | JSON payload |
| CreatedAt | datetime2 | NOT NULL | UTC |
| ProcessedAt | datetime2 | NULL | UTC, null if not yet processed |

**Indexes**: IX_OutboxMessages_ProcessedAt (WHERE ProcessedAt IS NULL) for polling unprocessed messages.

Note: This follows the bITdevKit outbox pattern (ADR-0006). The outbox worker polls every 30 seconds for unprocessed messages and publishes them.

#### Subscriptions Table

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | uniqueidentifier | PK, NOT NULL | Typed as SubscriptionId |
| UserId | uniqueidentifier | NOT NULL, UNIQUE | FK to UserProfile, one subscription per user |
| Plan | int | NOT NULL | Enumeration: 0=Free, 1=Basic, 2=Pro, 3=Enterprise |
| Status | int | NOT NULL | Enumeration: 0=Pending, 1=Active, 2=Cancelled, 3=Expired |
| BillingCycle | int | NOT NULL | Enumeration: 0=Never, 1=Monthly, 2=Yearly |
| StartDate | datetime2 | NOT NULL | UTC |
| EndDate | datetime2 | NULL | NULL = no end date (Free plan) |
| CreatedAt | datetime2 | NOT NULL | UTC |
| UpdatedAt | datetime2 | NOT NULL | UTC |
| RowVersion | rowversion | NOT NULL | Optimistic concurrency |

**Indexes**: IX_Subscriptions_UserId (UNIQUE)

### 9.2 Entity Relationships

```
UserProfile 1──1 Subscription
UserProfile 1──* UserCity *──1 City 1──1 CurrentWeather
                                  City 1──* WeatherForecast
```

- City is the root aggregate. CurrentWeather and WeatherForecast are entities within the City aggregate boundary.
- UserCity is a separate entity linking UserProfile to City.
- UserProfile is a standalone entity.

### 9.3 JSON Column Mapping

HourlyForecasts is stored as a JSON column on WeatherForecast. EF Core maps it using `OwnsMany` with JSON serialization:

```csharp
builder.OwnsMany(w => w.HourlyForecasts, h =>
{
    h.Property(hf => hf.Hour).HasColumnName("Hour");
    h.Property(hf => hf.Temperature).HasColumnName("Temperature");
    // ... other properties
});
```

Each HourlyForecast entry contains: Hour, Temperature, RelativeHumidity, ApparentTemperature, PrecipitationProbability, Precipitation, WeatherCode, WindSpeed, WindDirection, WindGusts, CloudCover, Visibility, IsDay.

### 9.4 Soft-Delete Filtering

EF Core global query filter on UserCity and UserProfile:

```csharp
modelBuilder.Entity<UserCity>().HasQueryFilter(uc => !uc.IsDeleted);
modelBuilder.Entity<UserProfile>().HasQueryFilter(up => !up.IsDeleted);
```

Admin endpoints use `IgnoreQueryFilters()` to include soft-deleted records.

City does NOT have a soft-delete filter — cities are hard-deleted by admin only.

### 9.5 Hard-Delete Cascade Strategy

When admin hard-deletes a City:
1. Delete all CurrentWeather records (by CityId)
2. Delete all WeatherForecast records (by CityId)
3. Delete all UserCity records (by CityId) — including soft-deleted ones
4. Delete the City record

This is executed in a single transaction. EF Core cascade delete is NOT used (to avoid accidental cascades). Each delete is explicit in the handler.

## 10. Integration Architecture

### 10.1 Open-Meteo Integration

| API | Purpose | Base URL | Rate Limit |
|-----|---------|----------|------------|
| Geocoding API | City search by name | https://geocoding-api.open-meteo.com/v1/search | ~10,000/day |
| Forecast API | Current + daily + hourly weather | https://api.open-meteo.com/v1/forecast | ~10,000/day |
| Lookup API | Exact geocoding by externalId | https://geocoding-api.open-meteo.com/v1/get | ~10,000/day |

### 10.2 Client Abstraction

```csharp
public interface IOpenMeteoClient
{
    Task<GeocodingResult> SearchCitiesAsync(string name, string? countryCode, CancellationToken ct);
    Task<GeocodingResult?> LookupCityAsync(long externalId, CancellationToken ct);
    Task<WeatherData> GetWeatherAsync(decimal latitude, decimal longitude, string timeZone, int forecastDays, CancellationToken ct);
}
```

Implementation uses `IHttpClientFactory` with typed client, Polly retry policy (3 retries, 1s backoff), and 100ms delay between sequential calls.

### 10.3 Failure Handling

| Failure | Strategy |
|---------|----------|
| Transient HTTP error (5xx, timeout) | Retry 3 times with 1s backoff (RetryPipelineBehavior) |
| Permanent error (4xx, invalid coordinates) | Log error, skip city, continue ingestion |
| Rate limit (429) | Log warning, back off, retry after delay |
| Open-Meteo completely unavailable | Serve cached data with staleDataWarning flag |
| Geocoding returns no results | Return 400 "No geocoding results found" to user |

### 10.4 Rate Limiting

- 100ms delay between sequential API calls for different cities
- No per-user rate limiting for v1 (Open-Meteo's 10,000/day limit is generous for expected traffic)
- Quartz job spaces ingestion across all cities sequentially

## 11. Security

### 11.1 Authentication

- All user endpoints require authentication
- UserId resolved from authenticated claims: `User.FindFirst(ClaimTypes.NameIdentifier)?.Value`
- Admin endpoints require "Admin" role claim
- Unauthenticated requests return 401 Unauthorized

### 11.2 Authorization

#### Feature Gating by Subscription Plan

| Plan | Max Cities | Forecast Days | Comparison | Export |
|------|-----------|---------------|------------|--------|
| Free | 3 | 7 | No | No |
| Basic | 10 | 16 | Yes | Yes |
| Pro | 25 | 16 | Yes | Yes |
| Enterprise | Unlimited | 16 | Yes | Yes |

| Endpoint Category | Required Role | Policy |
|-------------------|--------------|--------|
| User city/weather/dashboard endpoints | Authenticated user | Default policy |
| User profile/preferences endpoints | Authenticated user | Default policy |
| Admin city management endpoints | Admin | "CoreAdmin" policy |
| Admin weather reset/ingest endpoints | Admin | "CoreAdmin" policy |

### 11.3 Data Isolation

- Users can only access their own subscriptions (filtered by UserId)
- Users can only view weather data for cities they are subscribed to
- Admin endpoints bypass subscription checks but require admin role
- No cross-user data leakage in any query

### 11.4 Input Validation

- FluentValidation on all commands (via ValidationPipelineBehavior)
- City name minimum 3 characters for geocoding
- Days parameter range 1-16 for forecasts
- CityIds array 2-10 for comparison
- TemperatureUnit and WindSpeedUnit enum validation

## 12. Resilience

### 12.1 Retry Policy

- Open-Meteo API calls: 3 retries with 1-second exponential backoff (handled by RetryPipelineBehavior)
- City creation pipeline: No retry (idempotent by dedup)
- Weather ingestion pipeline: Retry on transient errors, skip on permanent errors

### 12.2 Idempotency

| Operation | Idempotency Key | Behavior |
|-----------|----------------|----------|
| City creation | ExternalId or (Latitude, Longitude) | Returns existing city if found |
| UserCity creation | (UserId, CityId) | Returns 409 if active, reactivates if soft-deleted |
| Weather ingestion | (CityId) | Upsert semantics — overwrites existing data |
| Manual ingestion trigger | (CityId) | Returns 202 even if already queued |
| Set primary city | (UserId, CityId) | Sets IsPrimary=true, all others false |

### 12.3 Concurrency

- All entities implement `IConcurrency` (RowVersion)
- Optimistic concurrency conflicts return 409 Conflict with message "The data was modified by another user. Please refresh and try again."
- DisplayOrder renumbering uses atomic UPDATE with WHERE clause

### 12.4 Data Integrity

- City deduplication prevents duplicate cities (ExternalId unique constraint, Lat/Lng proximity check)
- UserCity unique constraint prevents duplicate subscriptions (UserId + CityId, filtered WHERE IsDeleted = 0)
- WeatherForecast unique constraint prevents duplicate forecasts (CityId + ForecastDate)
- CurrentWeather unique constraint ensures one record per city (CityId)

## 13. Observability

### 13.1 Structured Logging

All handlers log using Serilog with structured message templates:

```csharp
logger.LogInformation("City created {CityId} with external ID {ExternalId}", city.Id, city.ExternalId);
logger.LogWarning("Ingestion failed for city {CityId}: {Error}", city.Id, ex.Message);
logger.LogInformation("Weather data ingested for city {CityId}, records: {CurrentCount} current, {ForecastCount} forecasts", city.Id, currentCount, forecastCount);
```

### 13.2 Metrics

| Metric | Type | Labels |
|--------|------|--------|
| core_ingestion_total | Counter | city_id, status (success/failure) |
| core_ingestion_duration_seconds | Histogram | city_id |
| core_geocoding_requests_total | Counter | status (success/no_results/error) |
| core_stale_data_served_total | Counter | city_id |
| core_dashboard_requests_total | Counter | user_id |
| core_alerts_computed_total | Counter | alert_type, severity |

### 13.3 Health Checks

- Open-Meteo API connectivity check (ping geocoding endpoint)
- Database connectivity check
- Quartz scheduler status check
- Stale data check: count cities with RetrievedAt > 60 minutes ago

## 14. Deployment Architecture

### 14.1 Hosting Model

WeatherFiesta runs as part of the bITdevKit modular monolith host. No separate deployment needed.

- **Host**: ASP.NET Core minimal API application
- **Module registration**: `builder.Services.AddModules().WithModule<CoreModule>()`
- **Database**: Shared SQL Server instance, Core schema
- **Scheduling**: Quartz.NET for 30-minute ingestion job

### 14.2 Scaling Strategy

- Single instance deployment for v1
- Horizontal scaling possible (stateless API, shared database)
- Quartz clustering mode for multi-instance ingestion (prevent duplicate jobs)

## 15. Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| OpenMeteo:GeocodingBaseUrl | string | https://geocoding-api.open-meteo.com/v1 | Geocoding API base URL |
| OpenMeteo:ForecastBaseUrl | string | https://api.open-meteo.com/v1/forecast | Forecast API base URL |
| OpenMeteo:LookupBaseUrl | string | https://geocoding-api.open-meteo.com/v1/get | Lookup API base URL |
| OpenMeteo:TimeoutSeconds | int | 10 | HTTP client timeout |
| OpenMeteo:RetryCount | int | 3 | Number of retries for transient errors |
| OpenMeteo:RetryDelayMs | int | 1000 | Delay between retries |
| OpenMeteo:InterCallDelayMs | int | 100 | Delay between sequential API calls |
| Core:StaleThresholdMinutes | int | 60 | Minutes before data is considered stale |
| Core:IngestionCron | string | 0 */30 * * * ? | Quartz cron for scheduled ingestion |
| Core:ForecastDays | int | 16 | Number of forecast days to ingest |
| Core:GeocodingMinQueryLength | int | 3 | Minimum characters for geocoding search |
| Core:ComparisonMaxCities | int | 10 | Maximum cities in comparison |
| Core:AdminRoleName | string | CoreAdmin | Role name for admin authorization |
| Core:DefaultPlan | string | Free | Default plan assigned to new users |

## 16. Architecture Decisions

| Decision | ADR | Rationale |
|----------|-----|-----------|
| Single module (not 7) | ADR-0001 | City is shared root entity; dashboard needs cross-slice queries |
| Staleness computed at query time | ADR-0002 | No stored flag; always accurate; simple DateTime comparison |
| API returns metric, frontend converts | ADR-0003 | Cacheable responses; one data format; consistent |
| Pipeline pattern for city creation and ingestion | ADR-0004 | Composable steps; testable; idempotent; no saga needed for v1 |
| Alerts/recommendations computed at query time | ADR-0005 | No storage; deterministic; always current; shared rule engine. Alert severities: Thunderstorm=Warning, Hail=Warning, Severe wind=Severe, Extreme heat=Warning, Blizzard=Warning, Hurricane=Extreme |
| ExternalId dedup, UserCity soft-delete, City hard-delete | ADR-0006 | ExternalId is exact; soft-delete enables reactivation; hard-delete for admin cleanup |
| Clean Onion architecture | ADR-0001 (bITdevKit) | Domain isolation; testability; framework independence |
| Modular monolith | ADR-0003 (bITdevKit) | Single deployment; clear module boundaries |
| Requester/Notifier pattern | ADR-0005 (bITdevKit) | Decoupled handlers; pipeline behaviors; consistent validation |
| Typed entity IDs | ADR-0008 (bITdevKit) | Type-safe IDs; compile-time checking |
| Outbox pattern for domain events | ADR-0006 (bITdevKit) | Atomic event persistence; reliable delivery |

## 17. Operational Considerations

### 17.1 Monitoring

- Dashboard response time: alert if > 1 second
- Ingestion failure rate: alert if > 10% of cities fail in a single run
- Open-Meteo API latency: alert if > 5 seconds
- Stale data count: alert if > 50% of cities have stale data

### 17.2 Maintenance

- Database migrations: EF Core migrations applied on startup
- Open-Meteo API changes: WeatherCode mapping may need updates if WMO codes change
- Index maintenance: Rebuild indexes if query performance degrades

### 17.3 Capacity Planning

- 10 cities × 48 ingestions/day = 480 API calls/day (well within 10,000 limit)
- 100 cities × 48 ingestions/day = 4,800 API calls/day (still within limit)
- 1,000 cities × 48 ingestions/day = 48,000 API calls/day (exceeds limit — need rate limiting or caching strategy)

## 18. Testing Strategy

### 18.1 Test Pyramid

| Level | Count | Focus | Tools |
|-------|-------|-------|-------|
| Domain unit tests | ~50 | Business rules, enumerations, rule engine | xUnit, Shouldly |
| Application unit tests | ~80 | Command/query handlers, validators, pipeline steps | xUnit, NSubstitute, Shouldly |
| Infrastructure integration tests | ~20 | EF mappings, repository queries, Open-Meteo client | xUnit, WebApplicationFactory |
| API integration tests | ~30 | Endpoint contracts, auth, error handling | xUnit, WebApplicationFactory |

### 18.2 Domain Tests

- City deduplication logic (ExternalId match, Lat/Lng proximity)
- UserCity soft-delete and reactivation
- DisplayOrder gap closing
- IsPrimary enforcement (only one per user)
- WeatherCode smart enum mapping
- Alert rule evaluation (all 6 alert rules: Thunderstorm=Warning, Hail=Warning, Severe wind=Severe, Extreme heat=Warning, Blizzard=Warning, Hurricane=Extreme)
- Recommendation rule evaluation (all 7 recommendation types)
- Staleness threshold computation

### 18.3 Application Tests

- CityCreateCommand validation (name length, countryCode format)
- CityCreatePipeline step execution order
- IngestWeatherPipeline step execution order
- All 27 endpoint request/response contracts
- Authorization checks (user vs admin)
- Concurrency conflict handling

### 18.4 Infrastructure Tests

- EF Core entity configurations (column types, constraints, indexes)
- HourlyForecasts JSON column mapping
- Soft-delete query filters
- Repository specifications (active cities, user subscriptions, stale data)
- Open-Meteo client deserialization (canned payloads)

### 18.5 Test Doubles

- `FakeOpenMeteoClient`: Returns canned geocoding and weather responses
- `FakeClock`: Mock `DateTime.UtcNow` for staleness tests
- `FakeUserIdProvider`: Mock authenticated user for handler tests
- Seeded data: 10 cities with weather data, 5 user subscriptions

### 18.6 Traceability Matrix

Every PRD story maps to at least one test. Key mappings:

| PRD Story | Test Category | Key Tests |
|-----------|--------------|-----------|
| PRD-0000 S1 (Suggestions) | Application | Geocoding validation, min length, API error |
| PRD-0000 S2 (Subscribe) | Application | Dedup, reactivation, geocoding failure |
| PRD-0000 S3 (View cities) | Application | Subscription filter, primary city, stale data |
| PRD-0000 S4 (Unsubscribe) | Application | Soft-delete, primary city cleared, DisplayOrder gap |
| PRD-0000 S5 (Set primary) | Application | Toggle primary, only one primary |
| PRD-0001 S1 (Reorder) | Application | Full-list replacement, validation |
| PRD-0100 S1 (Current weather) | Application | Staleness, unit preferences, subscription check |
| PRD-0100 S5 (Alerts) | Domain | All 6 alert rules, stale data flag |
| PRD-0200 S1 (Scheduled ingestion) | Infrastructure | Upsert, retry, stale data on failure |
| PRD-0400 S4 (Delete account) | Application | Soft-delete cascade, primary city cleared |
| PRD-0500 S1 (Dashboard) | Application | Aggregation, primary city, highlights |
| PRD-0600 S2 (Recommendation rules) | Domain | All 7 rules, severity sorting |

## 19. Risks and Trade-offs

| Decision | Benefit | Cost |
|----------|---------|------|
| Single module | Simple deployment, shared DbContext, no integration events | Module will grow large; no independent deployment |
| Query-time staleness | Always accurate, no background job | Computed on every read; negligible cost |
| Query-time alerts/recommendations | No storage, deterministic | Computed on every request; may need caching at scale |
| Metric-only API responses | Cacheable, consistent, one format | Frontend must implement conversion |
| Pipeline pattern | Composable, testable, idempotent | No compensation logic; partial failures leave stale data until next ingestion |
| UserCity soft-delete | Reactivation, data preservation | DisplayOrder gap closing requires UPDATE on unsubscribe |
| City hard-delete (admin) | Clean removal, no orphan data | Users lose subscriptions silently (v1: no notification) |
| ExternalId dedup | Exact matching, no duplicates | Some geocoding results lack ExternalId; falls back to Lat/Lng |
| HourlyForecasts as JSON | Simple storage, no separate table | Cannot query hourly data with LINQ; must deserialize |
| No push notifications (v1) | Simpler implementation | Users must poll for alerts |

## 20. Open Questions

| # | Question | Impact | Resolution |
|---|----------|--------|------------|
| 1 | How is UserId resolved from the authenticated principal? | All user endpoints need UserId | Host application provides claims; Core reads ClaimTypes.NameIdentifier |
| 2 | What admin role/policy name is used? | Admin endpoints need authorization | Configured via `Core:AdminRoleName` setting, default "CoreAdmin" |
| 3 | Does bITdevKit support `AddPipeline<TCmd,TResult>().WithStep<T>()` natively? | CityCreatePipeline and IngestWeatherPipeline registration | Needs validation against bITdevKit source; may need custom pipeline infrastructure |
| 4 | Should CityCreatedDomainEvent or EnqueueIngestionStep trigger ingestion? | ADR-0004 shows pipeline step; ADR-0006 mentions domain event | Both: pipeline step enqueues immediately for synchronous flow; domain event published via outbox for reliability. Dedup is handled by idempotent upsert semantics on the ingestion side — if the same city is ingested twice, the upsert overwrites existing data without error. |
| 5 | How are HourlyForecasts deserialized from JSON? | EF Core JSON column mapping strategy | Use `OwnsMany` with JSON serialization; SQL Server JSON functions for querying if needed later |
| 6 | What happens if DisplayOrder gap closing conflicts with concurrent unsubscribes? | Same user unsubscribing two cities simultaneously | UserCity updates are per-user; row-level locking prevents conflicts |
| 7 | Should the dashboard use a single aggregated query or multiple queries? | Performance for N cities | Single query with JOINs for v1; consider read model if performance degrades |
| 8 | Localization strategy for staleDataWarningMessage and recommendation messages? | Currently hardcoded English | v1: English only. v2: resource files with culture-specific strings |
| 9 | How to mock Open-Meteo client in tests? | Testability | `IOpenMeteoClient` interface with `FakeOpenMeteoClient` test double |
| 10 | EF Core cascade delete vs manual delete for admin hard-delete? | Data integrity | Manual explicit deletes in handler (no EF cascade) for control and auditability |
