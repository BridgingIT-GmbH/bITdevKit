---
title: Decisions
---

# Decisions

Use these guides to choose a `bITdevKit` feature or decide where code belongs. Each guide explains the tradeoffs with a concrete example and links to the feature documentation.

## Available guides

| Decision | Question |
| --- | --- |
| [Messaging or Queueing](decisions-messaging-vs-queueing.md) | Should an event reach several consumers, or should one consumer own a work item? |
| [Repository or ActiveEntity](decisions-repository-vs-activeentity.md) | Should persistence use an injected repository or entity-scoped methods? |
| [Pipeline or Orchestration](decisions-pipeline-vs-orchestration.md) | Is this an in-process sequence, or a stateful workflow that waits and resumes? |
| [Blob Storage or Document Storage](decisions-blob-vs-document-storage.md) | Does the application store streams or typed documents? |
| [Jobs or Startup Tasks](decisions-jobs-vs-startup-tasks.md) | Does work belong to a host startup or need scheduling and execution history? |
| [Where to Put Business Logic](decisions-business-logic.md) | Does the code enforce a domain rule or coordinate an application use case? |
| [When to Use Domain Events](decisions-domain-events.md) | Should a recorded business change trigger separate reactions after persistence? |
| [Requester or Notifier](decisions-requester-vs-notifier.md) | Does the caller need one handler's result or want to notify all matching handlers? |
| [Queueing or Jobs](decisions-queueing-vs-jobs.md) | Does work need consumer dispatch, scheduling, or both? |
| [Domain Events or Application Events](decisions-domain-vs-application-events.md) | Does the event originate in an aggregate or an application workflow? |
| [Change History or Event Sourcing](decisions-change-history-vs-event-sourcing.md) | Do you need an audit trail or events that reconstruct aggregate state? |
| [Repository or Document Storage](decisions-repository-vs-document-storage.md) | Are you persisting domain entities or typed documents? |
| [Direct Call or Requester](decisions-direct-call-vs-requester.md) | Does this operation need a service call or a request handler with shared behaviors? |
| [Retry or Compensation](decisions-retry-vs-compensation.md) | Should you repeat a failed action or counteract work that already succeeded? |
| [`Result<T>` or plain return type](decisions-results.md) | Does the caller need an explicit expected-failure outcome or a plain value? |
