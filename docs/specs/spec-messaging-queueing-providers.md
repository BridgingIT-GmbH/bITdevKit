---
status: implemented
---

# Design Specification: Messaging & Queueing Provider Enhancements

> This document outlines the design and implementation details for the new Azure Queue Storage messaging broker and the added pause/resume capabilities with operational endpoints in the messaging subsystem.

[TOC]

## 1. Azure Queue Storage Messaging Broker

New messaging broker backed by Azure Queue Storage, added to `Infrastructure.Azure.Storage`.

**Design**: one queue per message type, single poller per queue, in-process fan-out to all registered handlers for that type. No external pub/sub dependency — fan-out happens inside the broker after dequeue.

**Key types**:

- `AzureQueueStorageMessageBroker` — main broker implementation
- `AzureQueueStorageMessageBrokerOptions` / `AzureQueueStorageMessageBrokerOptionsBuilder` — configuration
- `AzureQueueStorageMessageBrokerConfiguration` — queue naming/settings
- `ServiceCollectionExtensions` — DI registration (`AddAzureQueueStorageMessageBroker`)

**Tests**: 5 integration tests in `tests/Infrastructure.IntegrationTests/Azure.Storage/Messaging/`.

## 2. Messaging Pause/Resume & Operational Endpoints

Added runtime pause/resume control at the **message type level** (not queue level) and new operational query endpoints.

### 2.1 Control State

`MessageBrokerControlState` (singleton in `Application.Messaging`) holds an in-memory `HashSet<string>` of paused type identifiers. Thread-safe via lock.

Dual-format normalization: both `AssemblyQualifiedNameShort` and `PrettyName` are checked when resolving pause state, so workers and store services match regardless of how the type string was stored.

### 2.2 IMessageBrokerService Additions

New methods on `IMessageBrokerService`:

| Method | Purpose |
|---|---|
| `PauseMessageTypeAsync(type)` | Pause processing for a message type |
| `ResumeMessageTypeAsync(type)` | Resume processing for a message type |
| `GetSummaryAsync()` | Broker runtime summary (counts, paused types, capabilities) |
| `GetSubscriptionsAsync()` | Active message type → handler registrations |
| `GetWaitingMessagesAsync(take?)` | Messages published with no handler registered |

### 2.3 REST Endpoints

Mapped in `MessagingEndpoints` under the messaging group:

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/summary` | Broker summary |
| `GET` | `/subscriptions` | Active subscriptions |
| `GET` | `/waiting` | Waiting (unhandled) messages |
| `POST` | `/types/{type}/pause` | Pause message type |
| `POST` | `/types/{type}/resume` | Resume message type |

### 2.4 Worker Integration

EF worker (`EntityFrameworkMessageBrokerWorker`) filters out paused types before claiming leases via `MessageBrokerControlState.IsMessageTypePaused`. Store service `GetSubscriptionsAsync` also checks both name formats for paused state.

## 3. DoFiesta Operations UI Updates

### 3.1 OperationsMessaging.razor

- Added subscriptions panel showing registered message type → handler mappings
- Added flow controls and waiting messages section
- Added pause/resume buttons per message type

### 3.2 OperationsQueueing.razor

- Removed queue-level pause/resume (only type-level pause remains)
- Layout aligned with messaging page

### 3.3 Auto-Refresh

- Default refresh interval changed from **30s → 5s** across all operations pages
- Interval persisted in `localStorage` so it survives page navigation

## 4. Files Added/Modified (Summary)

### Azure Queue Storage Broker (new)

- `src/Infrastructure.Azure.Storage/Messaging/AzureQueueStorageMessageBroker.cs`
- `src/Infrastructure.Azure.Storage/Messaging/AzureQueueStorageMessageBrokerConfiguration.cs`
- `src/Infrastructure.Azure.Storage/Messaging/AzureQueueStorageMessageBrokerOptions.cs`
- `src/Infrastructure.Azure.Storage/Messaging/AzureQueueStorageMessageBrokerOptionsBuilder.cs`
- `src/Infrastructure.Azure.Storage/Messaging/ServiceCollectionExtensions.cs`
- `tests/Infrastructure.IntegrationTests/Azure.Storage/Messaging/AzureQueueStorageMessageBrokerTests.cs`

### Messaging Pause/Resume & Endpoints

- `src/Application.Messaging/IMessageBrokerService.cs` — added 5 new methods
- `src/Application.Messaging/MessageBrokerControlState.cs` — new singleton
- `src/Application.Messaging/Models/BrokerMessageBrokerCapabilities.cs` — new model
- `src/Application.Messaging/Models/BrokerMessageBrokerSummary.cs` — new model
- `src/Application.Messaging/Models/BrokerMessageSubscriptionInfo.cs` — new model
- `src/Presentation.Web.Messaging/MessagingEndpoints.cs` — added 5 new endpoint mappings
- `src/Infrastructure.EntityFramework/Messaging/EntityFrameworkMessageBrokerStoreService{TContext}.cs` — pause-aware subscriptions
- `src/Infrastructure.EntityFramework/Messaging/EntityFrameworkMessageBrokerWorker{TContext}.cs` — pause filtering

### DoFiesta Operations UI

- `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsMessaging.razor` — subscriptions, pause/resume, waiting
- `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsQueueing.razor` — removed queue-level pause
- `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsFiles.razor` — refresh interval
- `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsFileEvents.razor` — refresh interval
- `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsJobScheduling.razor` — refresh interval
- `examples/DoFiesta/DoFiesta.Presentation.Web.Client/Pages/OperationsNotifications.razor` — refresh interval
