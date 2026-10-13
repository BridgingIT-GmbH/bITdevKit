---
title: Retry or Compensation
---

# Retry or Compensation

Retry attempts the same action again. Compensation performs another business action to counteract work that already succeeded. A workflow can need both.

## Choose retry when

- the failure is transient and a later attempt can succeed
- another attempt is safe for the operation's side effects
- attempts and delays have an explicit bound
- the component's retry policy can distinguish retryable failures from terminal outcomes

A timeout does not prove that an external operation failed to complete. Use a stable operation identifier or inspect the external state before repeating an action that must not happen twice.

Do not treat every failed `Result` as retryable. A rejected business rule needs a decision from the caller, rather than repeated identical attempts.

## Choose compensation when

- an earlier activity completed but the wider workflow cannot finish
- the earlier effect cannot be covered by one database transaction
- the domain has an explicit counteraction, such as releasing a reservation
- the process must retain history of both the original action and its counteraction

Compensation follows business rules. It may release, cancel, or reverse an effect without restoring every prior value. It can also fail and need operational recovery.

## Comparison

| Concern | Retry | Compensation |
| --- | --- | --- |
| Action | Repeat an operation | Execute a counteraction |
| Work addressed | Failed or uncertain attempt | Earlier completed work |
| Intended result | Complete the original action | Resolve effects of a workflow that cannot continue |
| Main requirement | Safe repetition and bounded attempts | Explicit business reversal and recoverable failures |
| Example | Retry a temporary carrier outage | Release stock after fulfillment becomes impossible |

## Example

A workflow reserves stock, then creates a shipment. Retry shipment creation when the carrier is temporarily unavailable.

If the order can no longer be fulfilled, release the completed stock reservation through compensation. Sending the same rejected shipment request forever does not resolve the reservation.

## Orchestration behavior

Activities support `Retry(...)` and `CompensateWith(...)`. The runtime registers compensation only after the forward activity completes successfully. It executes registered compensations in reverse order when the workflow enters its compensation path.

A failed activity may have partial external effects before it reports failure. Its implementation must account for that uncertainty; registration of compensation for successful activities does not solve it automatically.

An operator's `CancelAsync(...)` call is a direct lifecycle change and does not automatically run custom compensation logic. Model the required cleanup in the orchestration.

## Related features

- [Orchestrations](reference/features-orchestrations.md#activity-configuration)
- [Requester retry options](reference/features-requester-notifier.md#retryoptions-configuration)
- [Jobs](reference/features-jobs.md)
- [Pipeline or Orchestration](decisions-pipeline-vs-orchestration.md)
