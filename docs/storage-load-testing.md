# Storage dashboard throughput tests

Open **Diagnostics** on the Blobs, Documents, or Files dashboard. Choose a configured storage target, payload size, duration, worker count, and read percentage, then select **Start test**. Use 0% reads for a write test, 100% for a read test, or a value between them for mixed traffic. Blob tests require an existing container.

The charts show transfer speed, operation latency, throughput in MiB/s, and completed items per second. Application traffic and throughput tests share one view of the current node. The Files page remembers Explorer/Diagnostics selection in the same way as the other storage pages. Empty charts remain visible before measurements arrive.

## What is measured

Load tests call the configured provider contracts. They work with local, database, and cloud implementations without selecting provider-specific code. Document tests use a dedicated document type namespace, so they do not need to construct application models. Client serialization, transforms, and admission behaviors are excluded. File tests unwrap `IFileStorageBehavior` decorators so caching, retry, permalink, audit, and profiling behaviors do not turn temporary work into application records. Load measurements still use `IMetricsService`.

Reads consume the complete payload before recording bytes. Failed, incomplete, and cancelled transfers do not contribute successful bytes or items. The result shows successful read and write bytes divided by elapsed workload time, where one MiB is 1,048,576 bytes. Preparation and cleanup are excluded. The per-second throughput charts show completed bytes within each one-second interval; the speed chart averages individual successful transfer speeds.

The test emits standard DevKit metrics through `IMetricsService` on the `bdk` meter:

| Instrument | Meaning |
| --- | --- |
| `storage_load_bytes` | Successfully transferred payload bytes |
| `storage_load_items` | Successfully completed reads and writes |
| `storage_load_duration` | Successful full-transfer latency in milliseconds |
| `storage_load_bytes_per_second` | Speed of individual successful transfers |
| `storage_load_errors` | Workload failures |
| `storage_load_cleanup_failures` | Failed cleanup attempts |

Transfer metrics use only `feature` and `operation` tags. Keys, run identifiers, and payloads are never metric dimensions. Test metrics are combined with normal `blobstorage_*`, `document.*`, and `filestorage_*` traffic in the charts. Passive file metrics include read/write item counts and `filestorage_bytes_per_second`; passive stream-open timings can precede actual stream consumption.

This is an instrumented diagnostic workload, not a cold-cache or provider performance qualification. Network latency, cache state, other application traffic and metrics collection affect the result. Dashboard history is bounded to 15 minutes and resets when the host restarts.

## Temporary data and cleanup

Each worker reuses one randomly named temporary item. Limits are 1–16 workers, 4–4096 KiB per item, 1–60 seconds, and one active test per storage feature on a node. Payload data is random rather than compressible zero-filled content. Tests do not scan or modify application records. They never create blob containers or file directories.

Blob/document creation uses create-only conditions. Cleanup checks the diagnostic ownership property and uses the current ETag when deleting. File tests use flat random names, check that a name is absent before writing, and verify the payload ownership header before deleting. The ordinary file contract has no atomic create-only or conditional-delete operation; external writers must not modify diagnostic files while a test runs.

When the configured duration expires, the test stops issuing operations and lets in-flight transfers finish before cleanup. Elapsed time includes this drain. **Stop**, request cancellation, and graceful host shutdown cancel the workload and run cleanup with a separate cancellation budget. Cleanup verifies that the live test items are absent. A cleanup failure displays the remaining keys, offers **Retry cleanup**, and blocks another test on that feature until cleanup succeeds. Retrying cleanup targets only the retained run's keys.

Deleting an item does not erase provider-managed logs, history, versions, filesystem tombstones, or audit/monitoring events. A process crash, power loss, unavailable storage, or an operation that ignores cancellation can prevent cleanup. Pending cleanup state lives on the current node and is lost on restart; reconcile any remaining `bdk-diag-` items with the recorded run before removing them. The dashboard does not claim physical secure erasure or crash-proof cleanup.

## Access

Load and cleanup actions use the dashboard's configured authorization and group path. They require same-origin requests with the dashboard's load-action header. There are no provider-specific credentials or paths in the controls. The selected provider must permit temporary reads, writes, metadata inspection where applicable, and deletion.

See the [dashboard guide](features-presentation-dashboard.md), [Blob Storage](features-storage-blobs.md), [Document Storage](features-storage-documents.md), and [File Storage](features-storage-files.md).
