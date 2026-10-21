---
title: Where to Put Business Logic
---

<img class="bdk-doc-icon" src="assets/features/domain.svg" width="32" height="32" alt="" align="right">

# Where to Put Business Logic

Business logic includes domain rules and application workflows. Put a rule in the Domain layer when it defines valid business state. Put coordination in the Application layer when it executes a use case.

## Put domain rules in Domain

- aggregate methods protect state transitions and invariants
- value objects validate business concepts such as an email address or money amount
- domain policies express reusable decisions that need several domain facts
- domain events record meaningful changes without executing external side effects

The domain model remains independent of HTTP endpoints, database providers, and concrete storage clients. Pass the facts a rule needs instead of making an aggregate call an external service.

## Put use-case coordination in Application

- load the required aggregates or documents
- check caller permissions and validate the use-case input
- invoke aggregate methods or domain policies and handle their results
- persist changes and coordinate jobs, pipelines, storage, or external integrations
- map results into a response for the caller

Application handlers depend on abstractions. Infrastructure implements those abstractions, while Presentation handles transport-specific input and output.

## Comparison

| Question | Domain | Application |
| --- | --- | --- |
| What does the code decide? | Which business states and transitions are valid | How a use case obtains data and completes its work |
| What inputs does it need? | Domain values and relevant business facts | Requests, caller context, and service results |
| What does it coordinate? | Aggregate behavior and domain decisions | Persistence, calls, workflows, and side effects |
| Where must the rule hold? | Across every use case that changes the model | In the particular application workflow |

## Example

An order's cancellation method rejects cancellation after shipment. That rule belongs in the aggregate so every caller receives the same result.

A cancellation handler loads the order, checks the caller's access, invokes the method, and saves the change. A later notification or refund is a separate application reaction.

Keep the domain rule in one place. HTTP validation can reject malformed input early, but it does not replace an aggregate's invariant checks.

## Related features

- [Architecture](architecture.md#layer-responsibilities)
- [Domain Model](reference/features-domain.md)
- [Domain Policies](reference/features-domain-policies.md)
- [Application Commands and Queries](reference/features-application-commands-queries.md)
- [Domain Events](reference/features-domain-events.md)
