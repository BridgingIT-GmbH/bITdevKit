# WeatherFiesta — Documentation Index

WeatherFiesta is a weather dashboard application for tracking weather across multiple cities, backed by Open-Meteo data.

## Documents

| Document | Description |
|----------|-------------|
| [Architecture](./architecture.md) | System boundaries, components, data flow, integrations, persistence, security, failure handling |
| [Technical Design](./technical-design.md) | Domain model, database schema, endpoint mapping, pipelines, cross-cutting concerns, testing |

## PRDs

| ID | Slice | Title | File |
|----|-------|-------|------|
| PRD-0000 | CITIES | City Subscriptions | [prd-0000-cities-city-subscriptions.md](./prd-0000-cities-city-subscriptions.md) |
| PRD-0001 | CITIES | City Reorder | [prd-0001-CITIES-city-reorder.md](./prd-0001-CITIES-city-reorder.md) |
| PRD-0100 | WEATHER | Weather Data Viewing | [prd-0100-weather-weather-data-viewing.md](./prd-0100-weather-weather-data-viewing.md) |
| PRD-0101 | WEATHER | Sunrise and Sunset | [prd-0101-WEATHER-sunrise-sunset.md](./prd-0101-WEATHER-sunrise-sunset.md) |
| PRD-0102 | WEATHER | Weather Comparison | [prd-0102-WEATHER-weather-comparison.md](./prd-0102-WEATHER-weather-comparison.md) |
| PRD-0103 | WEATHER | Data Export | [prd-0103-WEATHER-data-export.md](./prd-0103-WEATHER-data-export.md) |
| PRD-0200 | INGESTION | Data Ingestion | [prd-0200-ingestion-data-ingestion.md](./prd-0200-ingestion-data-ingestion.md) |
| PRD-0300 | ADMIN | Admin City Management | [prd-0300-admin-city-management.md](./prd-0300-admin-city-management.md) |
| PRD-0400 | USER | User Profile | [prd-0400-USER-user-profile.md](./prd-0400-USER-user-profile.md) |
| PRD-0500 | DASHBOARD | Dashboard Summary | [prd-0500-DASHBOARD-dashboard-summary.md](./prd-0500-DASHBOARD-dashboard-summary.md) |
| PRD-0600 | RECOMMENDATIONS | Daily Summary and Recommendations | [prd-0600-RECOMMENDATIONS-daily-summary.md](./prd-0600-RECOMMENDATIONS-daily-summary.md) |
| PRD-0700 | SUBSCRIPTION | Subscription Plans | [prd-0700-SUBSCRIPTION-subscription-plans.md](./prd-0700-SUBSCRIPTION-subscription-plans.md) |

## ADRs

| ADR | Title | File |
|-----|-------|------|
| ADR-0001 | Module Boundaries | [adr-0001-weatherfiesta-module-boundaries.md](./adr-0001-weatherfiesta-module-boundaries.md) |
| ADR-0002 | Staleness Strategy | [adr-0002-weatherfiesta-staleness-strategy.md](./adr-0002-weatherfiesta-staleness-strategy.md) |
| ADR-0003 | Unit Preferences | [adr-0003-weatherfiesta-unit-preferences-strategy.md](./adr-0003-weatherfiesta-unit-preferences-strategy.md) |
| ADR-0004 | Ingestion Pipeline | [adr-0004-weatherfiesta-ingestion-pipeline.md](./adr-0004-weatherfiesta-ingestion-pipeline.md) |
| ADR-0005 | Alert & Recommendation Computation | [adr-0005-weatherfiesta-alert-recommendation-computation.md](./adr-0005-weatherfiesta-alert-recommendation-computation.md) |
| ADR-0006 | City Deduplication & Soft-Delete | [adr-0006-weatherfiesta-city-deduplication-and-soft-delete.md](./adr-0006-weatherfiesta-city-deduplication-and-soft-delete.md) |