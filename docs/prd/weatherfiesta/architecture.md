# WeatherFiesta Architecture Document

## 1. System Boundaries

### 1.1 Inside the System

```
┌──────────────────────────────────────────────────────────────────┐
│                      Core                                        │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                    Presentation Layer                       │  │
│  │  27 Minimal API Endpoints (api/core/*)              │  │
│  └──────────────────────────┬─────────────────────────────────┘  │
│                             │ IRequester / INotifier             │
│  ┌──────────────────────────┴─────────────────────────────────┐  │
│  │                    Application Layer                        │  │
│  │  Commands · Queries · Handlers · Specifications             │  │
│  │  Pipelines · Validators · Rule Engine                      │  │
│  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  │  │
│  │  │CITIES│ │WEATHR│ │INGEST│ │ADMIN │ │ DASH │ │RECOMM│  │  │
│  │  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘  │  │
│  └──────────────────────────┬─────────────────────────────────┘  │
│                             │                                    │
│  ┌──────────────────────────┴─────────────────────────────────┐  │
│  │                    Domain Layer                             │  │
│  │  City · UserCity · CurrentWeather · WeatherForecast         │  │
│  │  UserProfile · WeatherCode · AlertType · Recommendation    │  │
│  │  Location VO · WeatherRuleEngine · Domain Events            │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                    Infrastructure Layer                      │  │
│  │  DbContext · Repositories · Open-Meteo Client               │  │
│  │  Quartz Jobs · EF Configurations · Migrations                │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 1.2 Outside the System

| External System | Purpose | Communication | Failure Mode |
|----------------|---------|---------------|---------------|
| Host Application | Auth, logging, middleware | In-process | N/A (same process) |
| SQL Server | Data persistence | ADO.NET/EF Core | Serve cached data + stale warning |
| Open-Meteo Geocoding API | City search by name | HTTPS GET | Return 400 to user |
| Open-Meteo Forecast API | Weather data ingestion | HTTPS GET | Retry 3x, then serve cached data |
| Open-Meteo Lookup API | Exact geocoding by ID | HTTPS GET | Return 400 to user |
| ASP.NET Core Identity | User authentication | In-process claims | N/A (host responsibility) |

### 1.3 System Boundary Rules

- WeatherFiesta module cannot reference other modules' internal layers
- All external HTTP calls go through `IOpenMeteoClient` abstraction
- All database access goes through repositories (no raw SQL)
- All commands/queries go through `IRequester` (no direct handler calls from endpoints)

## 2. Components and Responsibilities

| Component | Responsibility | Layer | Key Collaborators |
|-----------|---------------|-------|-------------------|
| **City** | Global shared entity with geocoding data, dedup keys | Domain | UserCity, CurrentWeather, WeatherForecast |
| **UserCity** | Per-user subscription with order and primary flag | Domain | City, UserProfile |
| **CurrentWeather** | Latest weather snapshot per city | Domain | City, WeatherRuleEngine |
| **WeatherForecast** | Daily forecast with hourly JSON | Domain | City, WeatherRuleEngine |
| **UserProfile** | User identity and unit preferences | Domain | UserCity |
| **Subscription (Aggregate)** | Per-user subscription with plan, status, billing cycle | Domain |
| **WeatherRuleEngine** | Evaluates alert and recommendation rules | Domain | CurrentWeather, WeatherForecast |
| **CityCreatePipeline** | 5-step city creation orchestration | Application | OpenMeteoClient, City, UserCity |
| **IngestWeatherPipeline** | 4-step weather data ingestion | Application | OpenMeteoClient, CurrentWeather, WeatherForecast |
| **CoreModule** | Module registration and wiring | Presentation | All components |
| **OpenMeteoClient** | HTTP client for Open-Meteo APIs | Infrastructure | Geocoding, Forecast, Lookup |
| **CoreDbContext** | EF Core DbContext for all entities | Infrastructure | SQL Server |
| **IngestionJob** | Quartz scheduled job (30 min) | Infrastructure | IngestWeatherPipeline |

## 3. Data Flow

### 3.1 City Creation Flow

```
User ──POST /cities──▶ Endpoint
                          │
                          ▼
                    IRequester.SendAsync(CityCreateCommand)
                          │
                    ┌─────┴─────┐
                    │ Validation │
                    └─────┬─────┘
                          │
                    ┌─────┴──────────────┐
                    │ CityCreatePipeline │
                    │                    │
                    │ 1. GeocodeCity    │──▶ Open-Meteo Geocoding API
                    │ 2. CheckExists    │──▶ DbContext (dedup check)
                    │ 3. PersistCity    │──▶ DbContext (save City)
                    │ 4. PersistUserCity│──▶ DbContext (save UserCity)
                    │ 5. EnqueueIngest │──▶ IRequester (IngestWeatherCommand)
                    └─────┬────────────┘
                          │
                          ▼
                    Result<CityCreateResult>
                          │
                          ▼
                    201 Created ──▶ User
```

### 3.2 Weather Ingestion Flow

```
┌─────────────────────────────────────────────────────┐
│  Triggers                                           │
│  • Quartz Job (every 30 min, all cities)            │
│  • POST /cities/{cityId}/ingest (user, subscribed)  │
│  • POST /admin/cities/{cityId}/ingest (admin, any)  │
│  • CityCreatePipeline Step 5 (new city)             │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
              IRequester.SendAsync(IngestWeatherCommand)
                       │
                 ┌─────┴─────┐
                 │   Retry   │ (3x, 1s backoff)
                 └─────┬─────┘
                       │
              ┌────────┴──────────┐
              │ IngestWeatherPipeline │
              │                     │
              │ 1. FetchCurrent    │──▶ Open-Meteo Forecast API
              │ 2. UpsertCurrent   │──▶ DbContext (upsert CurrentWeather)
              │ 3. FetchForecast   │──▶ Open-Meteo Forecast API
              │ 4. UpsertForecasts │──▶ DbContext (upsert WeatherForecast[])
              └────────┬──────────┘
                       │
                       ▼
              Result<IngestWeatherResult>
                       │
              ┌───────┴────────┐
              │ On Failure:     │
              │ • Log warning   │
              │ • Keep old data │
              │ • Mark stale    │
              └────────────────┘
```

### 3.3 Dashboard Read Flow

```
User ──GET /dashboard──▶ Endpoint
                            │
                            ▼
                    IRequester.SendAsync(GetDashboardQuery)
                            │
                    ┌───────┴───────┐
                    │ DashboardQuery │
                    │ Handler        │
                    │               │
                    │ 1. Load UserCity subscriptions (with IsPrimary, DisplayOrder)
                    │ 2. Load CurrentWeather for all subscribed cities
                    │ 3. Compute staleness (RetrievedAt > 60 min)
                    │ 4. Compute highlights (warmest, coldest, wettest, windiest)
                    │ 5. Compute alerts (WeatherRuleEngine.EvaluateAlerts)
                    │ 6. Compute recommendations (WeatherRuleEngine.EvaluateRecommendations)
                    │ 7. Load UserProfile for unit preferences
                    └───────┬───────┘
                            │
                            ▼
                    DashboardResponse
                            │
                            ▼
                    200 OK ──▶ User
```

### 3.4 Alert and Recommendation Computation

```
CurrentWeather + WeatherForecast
            │
            ▼
    ┌───────────────────┐
    │ WeatherRuleEngine  │
    │                   │
    │ ┌───────────────┐ │
    │ │ Alert Rules   │ │
    │ │               │ │
    │ │ Thunderstorm  │ │  WMO 95,96,99 → Warning
    │ │ Hail          │ │  WMO 96,99 → Warning
    │ │ Severe Wind   │ │  Wind > 80 km/h → Severe
    │ │ Extreme Heat  │ │  Apparent temp > 40°C → Warning
    │ │ Blizzard      │ │  WMO 71-77 + Wind > 50 km/h → Warning
    │ │ Hurricane     │ │  Wind > 118 km/h → Extreme
    │ └───────────────┘ │
    │                   │
    │ ┌───────────────┐ │
    │ │ Recommend.    │ │
    │ │ Rules         │ │
    │ │               │ │
    │ │ Precipitation │ │  > 60% → "Bring umbrella" → Caution
    │ │ UV            │ │  > 6 → "Wear sunscreen" → Caution
    │ │ Cold Temp     │ │  Apparent < 5°C → "Dress warmly" → Caution
    │ │ Hot Temp      │ │  Apparent > 35°C → "Stay hydrated" → Warning
    │ │ Wind          │ │  > 40 km/h → "Wind advisory" → Caution
    │ │ Storm         │ │  WMO 95,96,99 → "Seek shelter" → Warning
    │ │ General       │ │  No adverse → "Great day" → Info
    │ └───────────────┘ │
    └───────────────────┘
            │
            ▼
    List<WeatherAlert> + List<Recommendation>
    (sorted by severity desc, then category order)
```

## 4. Integration Architecture

### 4.1 Open-Meteo Integration

```
WeatherFiesta                          Open-Meteo
┌──────────────┐                       ┌──────────────┐
│              │  GET /v1/search       │              │
│  Geocoding   │──────────────────────▶│  Geocoding   │
│  Client      │  ?name=Helsinki       │  API         │
│              │◀──────────────────────│              │
│              │  JSON response        │              │
└──────────────┘                       └──────────────┘

┌──────────────┐                       ┌──────────────┐
│              │  GET /v1/forecast      │              │
│  Forecast    │──────────────────────▶│  Forecast    │
│  Client     │  ?lat=60.17&lon=24.94 │  API         │
│              │  &current=...&daily=..│              │
│              │◀──────────────────────│              │
│              │  JSON response        │              │
└──────────────┘                       └──────────────┘

┌──────────────┐                       ┌──────────────┐
│              │  GET /v1/get           │              │
│  Lookup     │──────────────────────▶│  Geocoding   │
│  Client     │  ?id=658225            │  Lookup API  │
│              │◀──────────────────────│              │
│              │  JSON response        │              │
└──────────────┘                       └──────────────┘
```

### 4.2 Integration Failure Handling

| Failure | Detection | Response |
|---------|-----------|----------|
| Transient HTTP error (5xx, timeout) | HttpClient exception | Retry 3x with 1s backoff |
| Permanent error (4xx) | Status code | Log error, skip city, continue |
| Rate limit (429) | Status code | Log warning, back off, retry after delay |
| Open-Meteo completely down | Connection failure | Serve cached data with staleDataWarning |
| Geocoding no results | Empty results array | Return 400 "No geocoding results found" |
| Malformed response | Deserialization error | Log error, skip city, continue |

### 4.3 Rate Limiting

- Open-Meteo free tier: ~10,000 requests/day
- Expected load: 10 cities × 48 ingestions/day = 480 requests/day
- 100ms delay between sequential API calls
- No per-user rate limiting for v1

## 5. Persistence Model

### 5.1 Entity Relationships

```
┌──────────────┐       ┌──────────────┐       ┌────────────────┐
│  UserProfile │ 1───* │   UserCity   │ *───1 │     City       │
│              │       │              │       │                │
│  Id (PK)     │       │  Id (PK)     │       │  Id (PK)       │
│  Email       │       │  UserId (FK) │       │  Name          │
│  Name        │       │  CityId (FK) │       │  Country       │
│  TempUnit    │       │  IsPrimary   │       │  CountryCode   │
│  WindUnit    │       │  DisplayOrder│       │  TimeZone      │
│  IsDeleted   │       │  IsDeleted   │       │  Latitude      │
└──────────────┘       │  DeleteReason│       │  Longitude     │
                       └──────┬───────┘       │  Elevation     │
                              │               │  ExternalId    │
                              │               └───────┬────────┘
                              │                       │
                              │              ┌────────┴────────┐
                              │              │                 │
                              │    ┌─────────┴──────┐ ┌───────┴──────────┐
                              │    │ CurrentWeather  │ │ WeatherForecast  │
                              │    │                │ │                  │
                              │    │  Id (PK)       │ │  Id (PK)         │
                              │    │  CityId (FK,   │ │  CityId (FK)     │
                              │    │    UNIQUE)     │ │  ForecastDate    │
                              │    │  Temperature   │ │  DayWeatherCode  │
                              │    │  ApparentTemp   │ │  TempMax/Min     │
                              │    │  Humidity      │ │  AppTempMax/Min  │
                              │    │  WeatherCode   │ │  PrecipSum       │
                              │    │  WindSpeed     │ │  PrecipProbMax   │
                              │    │  WindDirection  │ │  WindSpeedMax    │
                              │    │  WindGusts     │ │  WindGustsMax    │
                              │    │  Precipitation │ │  WindDir         │
                              │    │  CloudCover    │ │  UvIndexMax      │
                              │    │  Pressure      │ │  SunDuration     │
                              │    │  RetrievedAt   │ │  DayDuration     │
                              │    └────────────────┘ │  Sunrise/Sunset  │
                              │                       │  HourlyForecasts │
                              │                       │    (JSON column) │
                              │                       │  RetrievedAt     │
                              │                       └──────────────────┘
                              │
                       ┌──────┴───────┐
                       │    Outbox    │
                       │  Messages    │
                       │              │
                       │  Id (PK)     │
                       │  Type        │
                       │  Data (JSON) │
                       │  CreatedAt   │
                       │  ProcessedAt │
                       └──────────────┘
```

### 5.2 Key Constraints

| Table | Constraint | Type |
|-------|-----------|------|
| Cities | ExternalId | UNIQUE (nullable) |
| Cities | (Latitude, Longitude) | Indexed (proximity check in code) |
| UserCities | (UserId, CityId) | UNIQUE (filtered WHERE IsDeleted = 0) |
| UserCities | (UserId, DisplayOrder) | Indexed (filtered WHERE IsDeleted = 0) |
| CurrentWeather | CityId | UNIQUE (one per city) |
| WeatherForecast | (CityId, ForecastDate) | UNIQUE (one per city per day) |
| UserProfiles | Email | UNIQUE (filtered WHERE IsDeleted = 0) |

### 5.3 Soft-Delete Strategy

| Entity | Delete Type | Behavior |
|--------|------------|----------|
| UserCity | Soft-delete | IsDeleted = true, IsPrimary = false, DisplayOrder gap closed |
| UserProfile | Soft-delete | IsDeleted = true, cascade soft-delete all UserCity |
| City | Hard-delete (admin only) | Cascade delete CurrentWeather, WeatherForecast, all UserCity |

EF Core global query filters exclude soft-deleted records by default. Admin endpoints use `IgnoreQueryFilters()`.

### 5.4 Concurrency

All mutable entities implement `IConcurrency` (RowVersion). Optimistic concurrency conflicts return 409 Conflict.

## 6. Security Model

### 6.1 Authentication

```
┌──────────┐     ┌──────────────┐     ┌──────────────────┐
│  Client   │────▶│  Host App    │────▶│  WeatherFiesta   │
│  (Browser)│     │  (Auth MW)   │     │  Endpoints        │
└──────────┘     └──────┬───────┘     └──────────────────┘
                       │
                       ▼
               ┌──────────────┐
               │  Claims      │
               │  Principal  │
               │              │
               │  UserId      │
               │  (NameIdent) │
               │  Roles       │
               └──────────────┘
```

- All user endpoints require authenticated user
- UserId resolved from `ClaimTypes.NameIdentifier`
- Admin endpoints require "CoreAdmin" role

### 6.2 Authorization Matrix

| Category | Endpoints | Required Role | Policy |
|----------|-----------|---------------|--------|
| City subscriptions | GET/POST/DELETE /cities/* | Authenticated | Default |
| Weather data | GET /cities/{id}/weather, /sun, /compare, /export | Authenticated + subscribed | Subscription check |
| Dashboard | GET /dashboard | Authenticated | Default |
| Recommendations | GET /cities/{id}/recommendations | Authenticated + subscribed | Subscription check |
| Alerts | GET /cities/alerts | Authenticated | Default |
| User profile | GET/PUT/DELETE /users/* | Authenticated (own data) | Owner check |
| Admin | /admin/cities/* | Admin | CoreAdmin |

### 6.3 Data Isolation

- Users can only access their own subscriptions (filtered by UserId)
- Users can only view weather data for subscribed cities
- Admin endpoints bypass subscription checks but require admin role
- No cross-user data leakage in any query

## 7. Deployment and Runtime Model

### 7.1 Runtime Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    ASP.NET Core Host                          │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                  Middleware Pipeline                   │   │
│  │  Serilog → Correlation → Exception → Auth → Routing │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Core                                       │   │
│  │                                                        │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌────────────┐  │   │
│  │  │  Endpoints    │  │  Requester   │  │  Notifier   │  │   │
│  │  │  (27 routes)  │──▶│  Pipeline    │──▶│  (Events)  │  │   │
│  │  └──────────────┘  └──────┬───────┘  └────────────┘  │   │
│  │                            │                           │   │
│  │  ┌─────────────────────────┴───────────────────────┐  │   │
│  │  │              Handlers & Pipelines                 │  │   │
│  │  │  CityCreatePipeline · IngestWeatherPipeline      │  │   │
│  │  │  27 Command/Query Handlers                       │  │   │
│  │  └─────────────────────────┬───────────────────────┘  │   │
│  │                            │                           │   │
│  │  ┌─────────────────────────┴───────────────────────┐  │   │
│  │  │              Domain & Infrastructure             │  │   │
│  │  │  Repositories · DbContext · OpenMeteoClient       │  │   │
│  │  │  WeatherRuleEngine · Quartz Job                   │  │   │
│  │  └─────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Quartz Scheduler                         │   │
│  │  Ingestion Job (every 30 min)                         │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              SQL Server Database                       │   │
│  │  Cities · UserCities · CurrentWeather                  │   │
│  │  WeatherForecasts · UserProfiles · OutboxMessages     │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### 7.2 Module Registration

```csharp
builder.Services.AddModules(builder.Configuration, builder.Environment)
    .WithModule<CoreModule>()
    .WithModuleContextAccessors()
    .WithRequestModuleContextAccessors();
```

### 7.3 Scaling

- Single instance deployment for v1
- Stateless API — horizontal scaling possible
- Quartz clustering mode for multi-instance ingestion (prevent duplicate jobs)
- No distributed cache required for v1

## 8. Observability

### 8.1 Structured Logging

All handlers use Serilog with structured message templates:

```
[LogKey] City created {CityId} with external ID {ExternalId}
[LogKey] Ingestion completed for city {CityId}: {CurrentCount} current, {ForecastCount} forecasts
[LogKey] Ingestion failed for city {CityId}: {Error}
[LogKey] Stale data served for city {CityId}: last updated {MinutesSinceUpdate} minutes ago
[LogKey] Dashboard request completed for user {UserId} in {ElapsedMs}ms
```

### 8.2 Metrics

| Metric | Type | Purpose |
|--------|------|---------|
| weatherfiesta_ingestion_total | Counter | Track ingestion success/failure per city |
| weatherfiesta_ingestion_duration_seconds | Histogram | Monitor ingestion latency |
| weatherfiesta_geocoding_requests_total | Counter | Track geocoding API usage |
| weatherfiesta_stale_data_served_total | Counter | Track stale data responses |
| weatherfiesta_dashboard_requests_total | Counter | Track dashboard usage |
| weatherfiesta_alerts_computed_total | Counter | Track alert computation volume |

### 8.3 Health Checks

| Check | Purpose |
|-------|---------|
| Open-Meteo connectivity | Verify external API is reachable |
| SQL Server connectivity | Verify database is accessible |
| Quartz scheduler status | Verify ingestion job is running |
| Stale data count | Alert if > 50% of cities have stale data |

## 9. Failure Handling

### 9.1 Failure Modes and Recovery

| Failure Mode | Detection | Recovery | User Impact |
|-------------|-----------|----------|-------------|
| Open-Meteo API down | HTTP connection failure or timeout | Serve cached data with staleDataWarning | Data may be outdated |
| Open-Meteo API slow | Response time > 10s | Timeout after 10s, serve cached data | Data may be outdated |
| Open-Meteo rate limit | HTTP 429 | Log warning, back off, retry after delay | Delayed ingestion |
| Open-Meteo malformed response | Deserialization error | Log error, skip city, continue | Missing data for one city |
| SQL Server down | Connection failure | Return 503 Service Unavailable | Complete outage |
| Optimistic concurrency conflict | RowVersion mismatch | Return 409 Conflict | User must refresh and retry |
| City dedup collision | Unique constraint violation | Return 409 "City already exists" | User can subscribe to existing city |
| UserCity dedup collision | Unique constraint violation | Return 409 or reactivate soft-deleted | User can re-subscribe |
| Ingestion partial failure | UpsertCurrentWeather succeeds, UpsertForecasts fails | CurrentWeather updated, forecasts stale until next ingestion | Partial data available |

### 9.2 Retry Strategy

```
Request ──▶ RetryPipelineBehavior
                │
                ├── Attempt 1 ──▶ Success ──▶ Return Result
                │
                ├── Attempt 2 (1s delay) ──▶ Success ──▶ Return Result
                │
                ├── Attempt 3 (2s delay) ──▶ Success ──▶ Return Result
                │
                └── All 3 failed ──▶ Log Warning ──▶ Serve Cached Data + Stale Flag
```

### 9.3 Data Integrity Guarantees

| Scenario | Guarantee | Mechanism |
|----------|-----------|-----------|
| Duplicate city creation | No duplicate City records | ExternalId unique constraint + Lat/Lng proximity check |
| Duplicate subscription | No duplicate UserCity records | (UserId, CityId) unique constraint (filtered WHERE IsDeleted = 0) |
| Duplicate weather ingestion | No duplicate weather records | Upsert on (CityId) for CurrentWeather, (CityId, ForecastDate) for WeatherForecast |
| Concurrent reorder | No lost updates | RowVersion optimistic concurrency |
| Admin city deletion | Complete removal | Explicit cascade delete in handler (not EF cascade) |
