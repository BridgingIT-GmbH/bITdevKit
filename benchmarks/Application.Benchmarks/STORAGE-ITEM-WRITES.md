# Item add and content-update scaling

This study measures EF provider calls for blobs and documents on SQL Server and PostgreSQL. It is separate from Dimension-only mutations and read-saturation qualification.

```powershell
$env:DOTNET_gcServer = '1'
./benchmarks/Application.Benchmarks/Run-StorageItemWrites.ps1 -Smoke -Output artifacts/storage-dimensions/my-write-smoke
./benchmarks/Application.Benchmarks/Run-StorageItemWrites.ps1 -Output artifacts/storage-dimensions/my-write-matrix
```

`-Engines` selects servers. `-Owners 1000000` selects one population for an independent repeat; omit it for the growing matrix. `-BulkUploadsOnly` runs only the public blob upload groups, allowing a completed sequential baseline to be retained.

Run Release builds first and keep binaries unchanged during the matrix. Engines run sequentially. `-Resume` skips successful engines and restarts an interrupted engine in a new owned database; earlier attempts remain recorded. A `STOP-AFTER-CASE` file in the output root stops after the current engine. Each owned container is disposed after its case.

## Measurement contract

- Collections grow through 10,000, 100,000 and 1,000,000 records per feature, in one container/type per feature. Both features share the database and one coordinator executes operation groups. Single/sequential calls use one writer; UploadManyAsync uses four workers.
- Payloads are exactly 1 KiB of valid JSON. No compression, encryption, HTTP, typed serialization, caching behaviors or conditional ETag inputs are included. Providers' internal concurrency and commit handling remain active.
- Every record has either zero Dimensions or three: `Number` is an integer, `Active` is a Boolean and `Label` is a string. The three dictionary names are registered through ordinary provider mutations before timing.
- Adds use fresh keys and create-only conditions. Each starts with exactly the stated number of existing records. Its added rows are removed after persistent verification, outside timing. A 100-item add temporarily grows the collection by 100.
- Content updates select distinct, uniformly random existing keys within each operation. Every selected payload changes. In the three-Dimension variant, all three logical values also change; the Boolean toggles per key.
- Blob groups measure both 100 sequential `UploadAsync` calls and public `UploadManyAsync` with four bounded workers. The latter includes client coordination and per-item facade validation. A document group calls `UpsertManyAsync` once with 100 writes. Group latency ends after every item is acknowledged. These are different API execution methods, not equal transaction counts.
- Five repetitions rotate operation order. Each repetition collects 100 single calls and ten 100-item groups after warm-up. Reported p95 uses all 500 single-call samples or all 50 group samples. The smaller group sample has a less precise tail and is indicative, not an SLA. Retain the per-repetition samples and ranges.
- Effective items/second divides committed items by measured call time. Preparation, verification, fixture work and cleanup are excluded, so these are call-only rates, not whole-application capacity.

Fixtures are created outside timing. EF generates keys and fixes relationships; SQL Server copies immutable seed rows with constraints enabled. PostgreSQL uses EF inserts. A transaction prepares all parent snapshots and three typed value rows per owner for each three-Dimension point. Dictionary rows remain retained when returning to zero Dimensions. These setup operations are not insert benchmarks.

After each measured call, grouped reads verify every payload byte, content hashes, snapshot values and typed search rows. Returned ETags must be nonempty and change for revisited keys. Owner/chunk/value/snapshot counts are checked after each feature point. Verification and cleanup use indexed exact-key queries, outside the measured provider calls. Full-population snapshot preparation, population verification and statistics refresh have a 600-second command allowance. This does not change provider-call timeouts or measured code.

## Environment and limits

The approved local budget is four application CPU threads and 8 GiB through a Windows job, plus four database CPUs and 24 GiB in an owned container. SQL Server uses MAXDOP 1 and `MinBatchSize(1)`; its log is provisioned to 8 GiB before work. PostgreSQL has `fsync`, `synchronous_commit` and `full_page_writes` enabled. Effective budgets and engine versions are recorded; this is not a separate-host or power-loss proof. Ordinary engine/host caches remain active. Other storage qualification runs must not overlap this matrix.

The study asks whether ordinary writes remain practical as the collection and its indexes grow, and what three indexed values add to the write cost. It does not measure concurrent readers, write saturation, arbitrary payload sizes, many distinct Dimension names, or more than three Dimensions per record. SQLite is outside this study.

## Results

The completed study contains 120 summaries with persistent correctness checks passing. Each single-call summary pools 500 observations; each 100-item summary pools 50 whole-group observations. Feature appendixes contain the client-facing tables and Mermaid charts.

| Million-record point, three Dimensions | SQL Server p95 | PostgreSQL p95 |
|---|---:|---:|
| Blob add, single | 10.70 ms | 8.99 ms |
| Blob content update, single | 12.97 ms | 8.88 ms |
| Blob add, UploadMany 100 / four workers | 306.50 ms | 223.20 ms |
| Blob content update, UploadMany 100 / four workers | 342.61 ms | 225.07 ms |
| Document add, single | 6.66 ms | 4.68 ms |
| Document content update, single | 5.73 ms | 4.69 ms |
| Document add, native 100-item group | 58.21 ms | 35.95 ms |
| Document content update, native 100-item group | 50.03 ms | 57.03 ms |

Single calls stayed in a similar band over the 100-fold population growth. Blob UploadMany groups completed about three to four times faster than sequential groups at one million records. PostgreSQL document bulk-update p95 increased about 1.5 to 1.9 times from 10k to one million, depending on Dimension count. SQL Server's 100k three-Dimension document point was slower than either endpoint, so the results do not form a monotonic latency curve. These measurements support practical ordinary-write scaling over the tested range. They do not resolve the separate mixed-reader Dimension-only update qualification.

The primary study combines the complete SQL Server baseline and public UploadMany repeat, the completed PostgreSQL 10k/100k points, and the verified million-record retry. The original PostgreSQL attempt failed during untimed snapshot preparation and remains preserved. Only fixture command allowances changed for the retry; measured provider code did not. The primary source data and provenance are retained under `artifacts/storage-dimensions/retained-evidence/`.

### Independent SQL Server 100k repeat

The original document three-Dimension bulk spike is retained. All three highest observations in each original 50-sample series came from the fifth repetition. Original add-group times included 135.29, 174.23 and 778.17 ms; update groups included 117.94, 136.32 and 151.66 ms. That repetition's medians were also elevated, so a single extreme sample does not explain the entire slow phase.

A fresh current-harness repeat at 100,000 records per feature passed all 20 operation summaries. Document three-Dimension group p95 was 60.64 ms for adds and 47.52 ms for updates, with maxima of 305.03 and 81.07 ms. It uses the same payload, bounded resource budget and sample counts, but fresh population history and the current blob upload groups. It is independent repeat evidence, not an interleaved causal experiment. The primary tables/curves remain unchanged. The independent repeat is included in the retained evidence archive.

No root cause was established for the original slow phase. Do not label it a proven index cliff, a GC issue, statistics maintenance or host noise from timings alone.
