---
created: 2026-11-16
status: implemented
---

# Dimensions for Blob Storage and Document Storage

This specification defines the implemented optional, queryable Dimensions for two independent storage features. Dimensions preserve the JSON-based Properties contract established by the [Properties alignment plan](../../plan/pln-refactor-storage-properties-alignment-1.md). EF models, atomic mutations, typed queries, dashboards and HTTP/CLI/MCP adapters are implemented and validated. The original server matrix and full current-model million-owner runs are complete. PostgreSQL passed every strict gate; SQL Server is accepted with the specific release exceptions recorded below. The [implementation plan](../../plan/pln-feature-storage-dimensions-1.md) records completion and accepted boundaries.

The contracts below remain normative for future changes. A representation or contract change requires renewed schema, semantics and performance proof.

## Agreed direction

- `Properties` remain descriptive/content metadata. Align their scalar persistence, null/empty mutation behavior, and private transform-metadata separation in the separate prerequisite plan. Dimensions must preserve that resulting contract, not make Properties queryable, restrict them to the Dimensions type set, or migrate them into Dimensions.
- `Dimensions` are a separate optional collection supplied explicitly by the application. Every accepted dimension is automatically indexed on a capable provider; applications do not predeclare dimension names or select which supplied dimensions to index.
- A property and a dimension may have the same name and unrelated values. Changing either collection does not implicitly update the other. Dimension filters read only Dimensions.
- Blob Storage and Document Storage own separate collections, predicates, validators, capabilities, dictionary/value entities, provider translations, and tests. Neither feature depends on the other, and there is no shared storage dimension engine or catalog.
- Dimensions persistence and querying are initially supported only by the EF provider on SQL Server, PostgreSQL, and SQLite, with engine-specific query capabilities. All other providers report Dimensions unsupported; their existing Properties and key-query behavior remain unchanged.
- Dimensions accept exactly `int`, `decimal`, `bool`, `string`, and UTC `DateTime`, plus explicit null as a value state.
- Each EF feature owns one name dictionary and one dimension-value table. Use three nullable physical search columns: `IntegralValue`, `DecimalValue`, and `StringValue`. Public types remain distinct through `ValueType`.
- Configure all new tables, keys, relationships, and ordinary composite indexes through standard EF annotations. No model-builder extension, custom convention, computed column, raw SQL schema definition, or filtered index is required.
- The name dictionary stores `NameHash`; the value table stores compact `NameId` references. Canonicalize names in memory. Do not persist a normalized-name column or repeat hashes/name strings in every value row.
- Optimize for many records with several dimensions each. Empty collections create no dimension values or name registrations. High-volume qualification must include populated collections, dictionary contention, index footprint, and mixed reads/writes.
- Capabilities distinguish dimension storage from dimension querying. Unsupported operations fail explicitly before provider I/O. No implicit cloud/filesystem scan substitutes for unsupported queries.

See [ADR-0044](../adr/0044-independent-storage-dimensions.md).

## Properties alignment prerequisite

Properties alignment establishes the contract below. Dimensions must preserve it. Do not reintroduce old blob-specific defaults, natural-JSON type loss, or transform keys in application Properties.

### Blob Storage

`BlobUpload.Properties`, `BlobInfo.Properties`, and `BlobPropertiesUpdate.Properties` use `PropertyBag`. The EF provider persists these through `StorageBlob.PropertiesJson` using scalar-codec JSON. Omitted/null replacement input preserves the existing bag; explicit empty input clears it.

`BlobQuery` currently contains a container, optional name prefix, paging, and full-scan approval. Its existing no-dimension behavior remains unchanged. Stored blob models separate compression/encryption behavior metadata from application Properties. Neither metadata collection becomes Dimensions.

### Document Storage

`DocumentInfo.Properties`, `DocumentWriteOptions.Properties`, and `DocumentPropertiesUpdate.Properties` use `PropertyBag`. `StorageDocument.PropertiesJson` encodes scalar values with `PropertyBagScalarCodec`. Null properties on replacement/update preserve the bag; an explicit empty bag clears it. These document semantics form the alignment baseline. The supported Properties scalar domain remains broader than the new Dimensions contract.

`DocumentQuery` and `DocumentCountQuery` currently filter partition/row keys, not arbitrary payload fields. `DocumentStoreProviderCapabilities` already describes key queries, counts, concurrency, expiration, and projection. Extend this feature's own capability model for Dimensions.

Source references:

- [Blob query](../../src/Application.Storage/Blobs/Models/BlobQuery.cs), [blob entity](../../src/Infrastructure.EntityFramework/Storage/Blobs/Entities/StorageBlob.cs), and [EF blob provider](../../src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs).
- [Document models](../../src/Application.Storage/Documents/Models/DocumentModels.cs), [document query](../../src/Application.Storage/Documents/DocumentQuery.cs), [document capabilities](../../src/Application.Storage/Documents/DocumentStoreProviderCapabilities.cs), [document entity](../../src/Infrastructure.EntityFramework/Storage/Documents/Entities/StorageDocument.cs), and [EF document provider](../../src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs).

## ETag conditional-update baseline

[PR #124](https://bridgingitgruppe.ghe.com/bit-intern/bITdevKit/pull/124), merged on `main` as `6a22d2a8003c185f0d9391587e3fe07bbc95f145`, is part of the implementation baseline. It adds conditional blob content replacement and completes whole-item version checks for both storage features. Dimensions extend these existing guarantees; they do not introduce a separate concurrency mechanism.

- Blob content writes already accept `BlobUpload.IfMatchETag`; byte/text/object/file upload options and destination copy/move options forward that condition. Documents use `DocumentWriteOptions.IfMatchETag`. Properties/expiration updates and conditional deletes use their existing feature-owned conditions.
- One item ETag covers content, Properties, and expiration. Dimensions join that same item version. A Dimensions change invalidates an earlier content, Properties, expiration, Dimensions, or delete condition, and those mutations invalidate an earlier Dimensions condition. Do not introduce a per-dimension ETag or accept a stale version because only another field changed.
- Null/omitted conditions allow unconditional writes. A supplied condition requires the exact existing item version. Conditional content replacement returns the feature's conflict error for a stale or deleted target and cannot recreate that target. Metadata-only updates and deletes retain their existing operation-specific missing-item results; adding Dimensions must not turn a missing owner into an insert. Create-only remains atomic and cannot be combined with an ETag. Wildcard ETags are not supported.
- Metadata preservation and transform-assisted read-modify-write may require an internal version check even when the caller omits a condition. That internal check must not replace or refresh a caller-supplied ETag. Bounded internal preservation attempts are permitted only for an otherwise unconditional operation. Conflicts and partial-update failures must not be automatically retried as unconditional writes.
- Return the ETag and metadata of the acknowledged committed version, even if a competing writer updates the item immediately afterward. Compensation may restore only the mutation's own version and must never recreate a deleted owner or overwrite a competing writer.
- The existing independently enabled read/maintenance HTTP groups accept optional `If-Match`, return HTTP 409 for version conflicts, and expose reusable `ETag` headers through `HttpETag`. Dimensions endpoints reuse that transport mapping, including rejection of conflicting header/body conditions and ETag lists. Forward decoded opaque conditions unchanged to the application/provider boundary. Preserve dashboard drafts and the originally read ETag on conflict. Dashboard Dimension queries are included; Dimension mutation editing remains outside scope.

The original Properties alignment plan excluded adding public conditional-upload APIs because that work belonged to PR #124. That exclusion does not remove the APIs now present in the merged baseline. Existing scalar persistence, null-preserve/empty-clear semantics, and private transform metadata remain requirements.

## Scope and ownership

| Area | Blob implementation | Document implementation |
| --- | --- | --- |
| `Application.Storage` | Blob Dimensions collection/value types, predicates, validation, query hashing, errors, capabilities, and behavior forwarding | Independent document equivalents, including payload/key pages and counts |
| `Infrastructure.EntityFramework` | `StorageBlobDimensionName`, `StorageBlobDimension`, dictionary resolution, transactions, and SQL translation | `StorageDocumentDimensionName`, `StorageDocumentDimension`, dictionary resolution, transactions, and SQL translation |
| Other provider packages | Advertise Dimensions unsupported and reject explicit dimension inputs/queries; no new dimension persistence | Equivalent document capability/rejection behavior; no new dimension persistence |
| Presentation and consumers | New Dimensions contracts where already exposing storage operations; preserve Properties | Equivalent document changes; preserve Properties |

Existing Common results, hashing, scalar codecs, and general utilities may be reused. Do not add a shared Dimensions base entity, collection, query AST, normalizer, dictionary service, or translator. Domain dependencies and module boundaries remain unchanged.

Excluded: querying Properties, payload-field search, full-text search, array/object dimensions, property-based sorting, an external search service, a cross-feature dictionary, and encryption refactoring. Dimensions are searchable metadata outside payload encryption; do not log their values.

## Public contracts and optionality

The implementation uses these independent APIs:

- Blob Storage owns `BlobDimensions`, `BlobDimensionValue`, `BlobDimensionFilter`, `BlobDimensionPredicate`, `BlobDimensionOperator`, `BlobDimensionValueType`, and `BlobDimensionCapabilities` under `Blobs/`.
- Document Storage owns independent `DocumentDimensions`, `DocumentDimensionValue`, `DocumentDimensionFilter`, `DocumentDimensionPredicate`, `DocumentDimensionOperator`, `DocumentDimensionValueType`, and `DocumentDimensionCapabilities` under `Documents/`.

These typed collections validate names and scalar operands. They are separate from the existing `PropertyBag` properties. Add optional `Dimensions` inputs to `BlobUpload` and `DocumentWriteOptions`, and Dimensions snapshots to `BlobInfo` and `DocumentInfo`. Copies returned to callers cannot mutate persisted state. Empty results expose an empty collection.

Provide independent `BlobDimensionsUpdate` / `DocumentDimensionsUpdate` requests and `UpdateDimensionsAsync` operations. They carry an exact owner key, replacement Dimensions, and optional `IfMatchETag` condition using the existing whole-item ETag. A dimensions-only update changes no content bytes, existing Properties, transform metadata, or expiration. It advances the owning record's existing ETag/modified timestamp under the same concurrency rules as other mutations.

Dimensions mutation semantics:

| Operation | Null/omitted Dimensions | Empty Dimensions | Non-empty Dimensions |
| --- | --- | --- | --- |
| New blob/document insertion | None | None | Store and index supplied values |
| Existing content replacement | Preserve current Dimensions | Clear | Replace |
| `UpdateDimensionsAsync` | Invalid input | Clear | Replace |
| `UpdatePropertiesAsync` or expiration-only update | Preserve, with no dimension lookup | Not a Dimensions input | Not a Dimensions input |

These rules apply only to the new Dimensions argument; existing Properties rules stay as they are. A write may intentionally update Properties and Dimensions together, but each is independently supplied and validated. An owner deletion removes its dimension values. A dimensionless record creates no dictionary entries or child rows.

The initial public ceilings are 64 dimensions per owner, including explicit-null entries, 16 predicates per filter, and four nested AND/OR group levels. A root group is level one; a standalone predicate uses no group level. Limit each filter to 16 groups as well, and reject empty groups. Apply validation to writes, updates, queries, and adapters before provider I/O. Expose these limits in each feature's capability descriptor; they are independent of page size and do not bound database candidates or matches.

### Logical types and three physical search columns

| Public dimension type | `ValueType` | Populated search column | Representation |
| --- | --- | --- | --- |
| `int` / `System.Int32` | `Integer` | `long? IntegralValue` | Exact signed 32-bit value widened internally |
| `bool` / `System.Boolean` | `Boolean` | `long? IntegralValue` | `0` or `1` |
| `DateTime` / `System.DateTime` | `DateTime` | `long? IntegralValue` | UTC `DateTime.Ticks` |
| `decimal` / `System.Decimal` | `Decimal` | `decimal? DecimalValue` | Exact `decimal(18,6)` value |
| `string` / `System.String` | `String` | Bounded `string StringValue` | Uppercase hexadecimal encoding of the exact UTF-8 value |
| Explicit null | `Null` | None | All three search columns null |

`IntegralValue` is a private persistence representation, not an expanded public `long` type. Decode through `ValueType`: an integer must fit `Int32`, a Boolean must be `0`/`1`, and a timestamp must fit `DateTime` and return with `DateTimeKind.Utc`. Every predicate constrains `ValueType`, so Boolean `1`, integer `1`, and timestamp tick `1` are distinct.

Require UTC `DateTime` in this contract. Reject local/unspecified input rather than depending on server time zone. UTC ticks preserve 100-nanosecond precision without native timestamp precision differences or provider converters. Record creation/expiration timestamps keep their existing contracts.

Decimal Dimensions use precision 18 and scale 6, with at most 12 whole digits and six fractional digits. Accept values from `-999999999999.999999` through `999999999999.999999`. Validate writes and query operands before I/O, rejecting values that require rounding or exceed this range. Numerically equal values such as `1.0m` and `1.000000m` have the same query identity; redundant trailing fractional zeros do not make a value invalid. Declare `[Precision(18, 6)]` on both independent `DecimalValue` members.

SQL Server and PostgreSQL target indexed decimal equality and range predicates. SQLite initially targets indexed decimal equality only, subject to real-engine semantics and plan verification; other decimal value comparisons are unsupported and fail before query execution. Existence/null predicates remain available. SQLite still round-trips accepted decimal values exactly. Do not convert decimals to floating point or evaluate unsupported predicates on the client.

String values use exact, case-sensitive ordinal equality. Case, leading/trailing spaces, and Unicode normalization forms remain significant: `"EU"`, `"eu"`, and `"EU "` are distinct values. Empty string is a present string, distinct from explicit null and a missing dimension. Do not trim, case-fold, or normalize string values. Initial profiles support string equality/inequality, not string ordering. The indexed representation must preserve these semantics without depending on the database's default linguistic comparison rules.

Accept at most 256 UTF-8 bytes per string value or query operand, using strict UTF-8 encoding that rejects unpaired UTF-16 surrogates. Keep the exact original string in the owner snapshot. Populate `StringValue` with `Convert.ToHexString` of those bytes, producing only uppercase ASCII `0`-`9`/`A`-`F`, up to 512 characters. Empty string encodes as empty string. This is a reversible search representation, not a digest; it introduces no value-hash collision semantics. Encode parameters before SQL construction and compare the indexed column directly, without SQL conversion or collation functions. Use `[MaxLength(512)]` and `[Unicode(false)]` on both independent `StringValue` members. Qualify equality and index use on each supported engine/collation profile, including case, accents, normalization forms, embedded nulls, spaces, empty strings, and maximum-length values.

Reject unsupported Dimensions types before mutation, including public `long`, floating-point values, GUIDs, `DateTimeOffset`, date-only/time-only values, durations, byte arrays, collections, and nested objects. Do not round, truncate, stringify, or silently omit a dimension to fit storage. These restrictions do not apply to existing Properties.

Dimension names match `[A-Za-z_][A-Za-z0-9_.:-]{0,127}`: one to 128 ASCII characters, starting with a letter or underscore. Reject invalid names without trimming or replacing characters. Names are case-insensitive; canonicalize ASCII `a`-`z` to `A`-`Z` before hashing, identity checks, query hashing, and cache lookup. Reject duplicate names under that identity within one collection rather than selecting a winner. This rule needs no Unicode normalization or runtime-dependent Unicode case table. It restricts dimension names only; string values remain Unicode and existing Properties names are unchanged. A name is not tied to one global type: different records may use different kinds under the same name. Preserve original spelling in the owner snapshot; do not use the first writer's value kind as a dictionary schema.

Validate that exactly one search column is populated for a non-null value and that the discriminator/representation match. Null rows have no populated search column. Missing dimensions have no row. This is explicit provider validation; annotations do not enforce this cross-column rule against direct SQL writes.

## Name dictionaries

Each feature owns a separate dictionary scoped by its actual persisted namespace: blob container or document type within its physical store. Do not substitute a named-client boundary that disagrees with the provider's current record identity. Document partitions remain parent query constraints; a name can be reused across partitions of the same type.

For each dictionary entry persist:

- `Id`: generated `long`, unique within that feature's dictionary table.
- `ScopeHash`: full SHA-256 digest of the persisted namespace, stored as a 32-byte `byte[]`.
- `Scope`: original persisted namespace, maximum 256 UTF-16 code units, matching existing EF container/type bounds; retain its existing identity/normalization rules.
- `NameHash`: full SHA-256 of the UTF-8 canonical dimension name, stored as a 32-byte `byte[]`.
- `Name`: original spelling, maximum 128 ASCII characters, for identity verification and diagnostics.

Use a unique index on `(ScopeHash, NameHash)`. Compute hashes in application code and verify scope/name identity after the bounded dictionary lookup. Hash collisions between distinct logical identities must return typed failures, never resolve to the wrong name or overwrite an entry. No normalized-name column is persisted.

Use separate proposed `BlobDimensionHashCollisionError` and `DocumentDimensionHashCollisionError` results for verified scope/name digest collisions, identifying the collision kind without logging dimension values. A collision aborts the owning operation atomically; it is not a registration race or a recoverable owner-version conflict. Invalid scalar/name/limit inputs use the feature's validation result conventions; unsupported operators use its existing query-not-supported result, and stale owner versions retain the existing store conflict result.

The provider registers missing names automatically, resolving concurrent insert attempts through a bounded insert-or-read protocol appropriate to the backend transaction semantics. Register and attach new names within the owning mutation's atomic unit. Publish name-cache entries only after commit. A failed owner write must not leave a partially attached Dimensions collection.

### Automatic NameId management

The database generates dictionary IDs; the EF provider automatically creates, resolves, and reuses dictionary entries. Applications supply logical dimension names and typed values only. They never assign a `NameId`, pre-register names, seed dictionary rows, or configure an ID generator.

Declare this independently on each feature's name-dictionary entity:

```csharp
[Key]
[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
public long Id { get; set; }
```

Leave new dictionary IDs unset so the active EF database provider uses its generated numeric-key mapping. `NameId` on a dimension-value entity is an ordinary foreign key, not another generated column. The database's primary key, scoped-name unique index, and foreign keys protect stored identities and references; the EF provider supplies the name-resolution and lifecycle logic. No database trigger, stored procedure, custom EF convention, or model-builder configuration is required.

For a write containing `region = "EU"`, the owning EF provider performs this flow automatically:

1. Validate/canonicalize `region`, compute its scoped hashes, and resolve names in a bounded batch or positive cache.
2. If the scoped name is new, insert a dictionary entity. The database generates its `Id`; concurrent registrations converge through the existing unique-index protocol.
3. Attach the dimension value to that name entity. EF propagates the generated ID into the value's `NameId`; callers never handle temporary or persisted IDs.
4. Commit name registration, owner snapshot, and value changes together, then publish any positive cache mapping.
5. Later writes and queries resolve/reuse the same scoped name without a schema migration or manual setup.

The only host database setup is the normal EF schema migration/update for the annotated entities. Dictionary tables and indexes are included through ordinary model discovery. Adding a new logical dimension after that initial migration changes data, not schema. Clearing values, deleting owners, and retaining unused names require no application-maintained registry or recurring repair job. IDs are internal database-local references; applications must not persist or depend on them.

Resolve distinct query names in one bounded batch, or from a bounded positive cache keyed by physical store and logical scope. Do not issue one lookup per predicate or per result row. Resolve to `NameId` before constructing value-table predicates, keeping strings and name hashes out of the hot value indexes. Cache misses must not hide concurrently registered names; do not cache negative results by default. Invalidate positive caches on resource recreation.

Dictionary entries are immutable and retained when owners clear dimensions or are deleted. Do not reuse IDs, rename entries in place, or update shared reference counters on each write. This avoids turning popular names into write hotspots. Empty collections never register names. There is no distinct-name quota per scope or physical store. Applications control name cardinality and should reuse names rather than generate a name per record. Retained dictionary storage can therefore grow without a fixed ceiling; expose entry counts and new-name registration rates in diagnostics without imposing a hidden quota or running a dictionary count on normal writes.

Bound each feature's positive cache to 4,096 entries per physical-store provider instance, across its scopes, using eviction rather than indefinite retention. Include physical-store identity, exact logical scope, and canonical name in cache identity. Resource recreation invalidates the cache. Independently bound new-name registration to four in-flight mutations per provider instance; waiting is asynchronous and cancellable. Existing-name writes do not acquire a registration permit. These are initial internal defaults, not database-wide locks or dictionary-size limits.

Resolve/register all distinct names for one owner mutation in a batch of at most 64. Allow at most three attempts to resolve a concurrent first-registration race, using backend-safe transaction/savepoint handling. Keep the original caller ETag and existing stream/lease rules; do not replay a consumed content stream, weaken a condition, or retry a returned version conflict. Exhaustion returns a feature-owned provider failure with a registration-contention reason and no partial owner/name/value commit. Do not add global locks, persistent scope counters, or a round trip per name.

An unknown name returns no matches for equality/existence and matches missing within the allowed owner scope. Simplify these cases without registering a name during a read. Public queries and continuation hashes use logical names/values, never dictionary IDs. Blob/document dictionaries, IDs, caches, and failure handling remain independent.

## EF database layout

The logical diagrams omit existing payload/chunk and unrelated metadata columns. CLR types describe the model; each provider selects its SQL representation. Fixed schema constants are `ScopeLength = 256`, `NameLength = 128`, `StringLength = 512`, and decimal precision/scale `18,6`. The public string limit is 256 UTF-8 bytes before hexadecimal encoding.

### Blob tables

```text
__Storage_Blobs                    __Storage_BlobDimensions
------------------------------    ------------------------------
Id                       PK ------ BlobId                 PK, FK
Container/ContainerHash             NameId                 PK, FK ----+
Name/NameHash                       ValueType                       |
Properties       unchanged JSON    IntegralValue         nullable   |
Dimensions        new snapshot     DecimalValue          nullable   |
...                                StringValue           nullable   |
                                                                    |
                                  __Storage_BlobDimensionNames      |
                                  ------------------------------    |
                                  Id                       PK <-----+
                                  ScopeHash
                                  Scope
                                  NameHash
                                  Name
```

| Blob dimension column | CLR type / bound | Role |
| --- | --- | --- |
| `BlobId` | Required `string`, maximum 64 | FK to `__Storage_Blobs.Id`; first PK component |
| `NameId` | `long` | FK to blob name dictionary; second PK component |
| `ValueType` | Blob-owned enum backed by `byte` | Logical type or explicit null |
| `IntegralValue` | `long?` | Integer, Boolean, or UTC ticks |
| `DecimalValue` | `decimal?`, `[Precision(18, 6)]` | Decimal only |
| `StringValue` | Nullable `string`, `[MaxLength(512)]`, `[Unicode(false)]` | ASCII hexadecimal string search representation |

`StorageBlobDimension` maps to `__Storage_BlobDimensions` with primary key `(BlobId, NameId)`. `StorageBlobDimensionName` maps to `__Storage_BlobDimensionNames` with generated `long Id`, required bounded `Scope`/`Name`, required 32-byte `ScopeHash`/`NameHash`, and unique `(ScopeHash, NameHash)`.

### Document tables

```text
__Storage_Documents                __Storage_DocumentDimensions
------------------------------    ------------------------------
Id                       PK ------ DocumentId             PK, FK
Type/TypeHash                       NameId                 PK, FK ----+
PartitionKey/RowKey                 ValueType                       |
Properties       unchanged JSON    IntegralValue         nullable   |
Dimensions        new snapshot     DecimalValue          nullable   |
...                                StringValue           nullable   |
                                                                    |
                                  __Storage_DocumentDimensionNames  |
                                  ------------------------------    |
                                  Id                       PK <-----+
                                  ScopeHash
                                  Scope
                                  NameHash
                                  Name
```

| Document dimension column | CLR type / bound | Role |
| --- | --- | --- |
| `DocumentId` | `Guid` | FK to `__Storage_Documents.Id`; first PK component |
| `NameId` | `long` | FK to document name dictionary; second PK component |
| `ValueType` | Independent document enum backed by `byte` | Logical type or explicit null |
| `IntegralValue` | `long?` | Integer, Boolean, or UTC ticks |
| `DecimalValue` | `decimal?`, `[Precision(18, 6)]` | Decimal only |
| `StringValue` | Nullable `string`, `[MaxLength(512)]`, `[Unicode(false)]` | ASCII hexadecimal string search representation |

`StorageDocumentDimension` maps to `__Storage_DocumentDimensions` with primary key `(DocumentId, NameId)`. Its dictionary uses `StorageDocumentDimensionName` and `__Storage_DocumentDimensionNames`, with its own generated `long Id` and the same conceptual fields/unique index. There is no shared CLR entity or cross-feature FK.

### Keys, indexes, and annotations

Each value table has exactly three baseline non-unique search indexes:

| Search slot | Blob index key | Document index key |
| --- | --- | --- |
| Integral | `(NameId, ValueType, IntegralValue, BlobId)` | `(NameId, ValueType, IntegralValue, DocumentId)` |
| Decimal | `(NameId, ValueType, DecimalValue, BlobId)` | `(NameId, ValueType, DecimalValue, DocumentId)` |
| String | `(NameId, ValueType, StringValue, BlobId)` | `(NameId, ValueType, StringValue, DocumentId)` |

`NameId` already identifies a verified, scoped dictionary entry, so do not repeat scope/name hashes or text in value rows or search keys. Parent joins still enforce complete logical scope, optional partition/key constraints, expiration, and authorization. A provider validates that an owner is attached only to names from its scope; foreign keys alone do not establish that invariant.

Each value primary key provides owner-leading replacement/deletion and uniqueness; do not add a duplicate owner/name index. The name dictionary adds its own primary key and scoped hash unique index. Three search indexes does not mean three indexes total. Inspect migrations for redundant convention-created FK indexes already covered by these access paths.

The document parent also declares an ordinary `[Index]` on `(TypeHash, Type, Id, ExpiresAtUnixMilliseconds)`. It covers scoped candidate membership and logical expiration without fetching document content rows during counts. Keep it unfiltered, including documents with no expiration. Retain the existing expiration index for retention; optional expiration-index filtering must not filter this visibility index. Include this additional parent index in storage and write-cost measurements.

Use only standard `[Table]`, `[Key]`/`[PrimaryKey]`, `[DatabaseGenerated]`, `[Required]`, `[MaxLength]`, `[Unicode]`, `[Precision]`, `[Index]`, and relationship annotations plus normal EF conventions. A non-composite `long` dictionary key uses database-generated values through the active EF provider. Enforce exact digest length and scalar limits in provider validation as well as model facets. Do not hard-code SQL types, use `INCLUDE`, or require filtered indexes.

For example, these are proposed annotations on the independent blob value entity:

```csharp
[Table("__Storage_BlobDimensions")]
[PrimaryKey(nameof(BlobId), nameof(NameId))]
[Index(nameof(NameId), nameof(ValueType), nameof(IntegralValue), nameof(BlobId))]
[Index(nameof(NameId), nameof(ValueType), nameof(DecimalValue), nameof(BlobId))]
[Index(nameof(NameId), nameof(ValueType), nameof(StringValue), nameof(BlobId))]
```

Declare document equivalents independently. Required owner/name navigations allow model discovery without a new host model-builder call; verify annotations-only contexts for each feature alone and both together. Preserve existing context contracts where possible. Owner deletion removes value rows but never cascades into shared dictionary names. Inspect and test the generated FK delete behavior; use explicit owner-scoped transactional deletes where required.

### Separate Dimensions snapshots

Add a nullable, feature-owned `DimensionsJson` persistence member mapped to a new parent `Dimensions` column. It contains the original dimension names and typed logical values, not internal `NameId` values. Keep existing `PropertiesJson` getters/setters and physical `Properties` columns untouched.

Persist a compact feature-owned JSON snapshot independently of the public typed-envelope transport format. Each original name maps to a two-element `[kind, value]` array. The fixed kind tags match `ValueType`: null 0, integer 1, decimal 2, boolean 3, string 4, and UTC DateTime 5. Store exact decimals as invariant strings and UTC dates as ticks; strings remain their original logical text, with UTF-8 characters preserved. For example, `{"Year":[1,2026],"Amount":[2,"125.5"],"Approved":[3,true],"Region":[4,"EU"],"MissingValue":[0,null]}`. No NameIds or encoded search strings appear in this snapshot.

The smaller snapshot limits parent-row width, column materialization and allocation costs on ordinary metadata paths. Validate count, names, duplicate identities, kind/value shapes and scalar bounds when decoding it. Empty collections still use database null. Public JSON continues to use typed envelopes; the existing Properties scalar-codec format remains unchanged. No old snapshot reader, backfill or upgrade workflow is required for this new feature.

The Dimensions snapshot is authoritative for metadata reads and mutation comparison. Maintain it and searchable value rows in the same commit. This deliberate duplication preserves existing optimized point-read/paged metadata paths without a dimension query per owner. A null snapshot represents an empty collection and requires no dictionary/value-table access on ordinary reads or writes that preserve emptiness. New properties-only writes create no Dimensions snapshot or child rows.

Do not put a serialized copy of Dimensions into every value row. Properties-only changes perform no dictionary lookup or dimension DML. New/changed Dimensions normalize once, resolve required names in a bounded batch, and maintain only the changed value rows. Identical Dimensions cause no dimension-table DML. Whole-owner deletion and retention remove values with indexed, bounded cleanup; dictionaries remain reusable.

Blob staged uploads must publish the final content/metadata/snapshot/value delta under their existing lease/concurrency transaction. Do not buffer the entire payload to achieve metadata atomicity. Document batches retain their existing item outcome and concurrency semantics, including bulk paths that bypass ordinary tracking.

## Sample data

These fictional invoice examples show decoded parent snapshots and persisted value slots, not final encoded snapshot bytes. `B1`/`B2` abbreviate string blob IDs and `D1`/`D2` abbreviate GUID document IDs. `HC`/`HT` and `H_...` abbreviate full 32-byte digests, not literal stored strings. Dictionary IDs are illustrative generated integers. `NULL` denotes database null. String slot `432D3432` is the UTF-8 hexadecimal representation of logical value `C-42`.

### Blob example

`__Storage_Blobs`, selected columns:

| Id | Container | Name | Properties, decoded | Dimensions, decoded |
| --- | --- | --- | --- | --- |
| `B1` | `archive` | `invoices/INV-0007.pdf` | `source="email", year=2020` | Six typed entries shown below |
| `B2` | `archive` | `attachments/scan-0008.pdf` | `year=2026` | `NULL` |

`__Storage_BlobDimensionNames`:

| Id | ScopeHash | Scope | NameHash | Name |
| --- | --- | --- | --- | --- |
| `101` | `HC` | `archive` | `H_CUSTOMER` | `customerId` |
| `102` | `HC` | `archive` | `H_YEAR` | `year` |
| `103` | `HC` | `archive` | `H_AMOUNT` | `amount` |
| `104` | `HC` | `archive` | `H_APPROVED` | `approved` |
| `105` | `HC` | `archive` | `H_ISSUED` | `issuedAt` |
| `106` | `HC` | `archive` | `H_EXTERNAL` | `externalRef` |

`__Storage_BlobDimensions`:

| BlobId | NameId | ValueType | IntegralValue | DecimalValue | StringValue |
| --- | --- | --- | --- | --- | --- |
| `B1` | `101` | `String` | `NULL` | `NULL` | `432D3432` |
| `B1` | `102` | `Integer` | `2026` | `NULL` | `NULL` |
| `B1` | `103` | `Decimal` | `NULL` | `1250.50` | `NULL` |
| `B1` | `104` | `Boolean` | `1` | `NULL` | `NULL` |
| `B1` | `105` | `DateTime` | `639304218000000000` | `NULL` | `NULL` |
| `B1` | `106` | `Null` | `NULL` | `NULL` | `NULL` |

The ticks represent `2026-11-16T10:30:00Z`. A dimension filter `year == 2026` matches `B1`, despite its unrelated property `year=2020`. It does not match `B2`, which has a property `year=2026` but no Dimensions. An explicit-null dimension filter for `externalRef` matches `B1`; a missing-dimension filter matches `B2`. Neither reads PDF bytes or filters Properties.

### Document example

`__Storage_Documents`, selected columns for a fictional invoice type:

| Id | PartitionKey | RowKey | Properties, decoded | Dimensions, decoded |
| --- | --- | --- | --- | --- |
| `D1` | `customer-42` | `INV-0012` | `source="api", year=2024` | Six typed entries shown below |
| `D2` | `customer-42` | `INV-0013` | `year=2026` | `NULL` |

`__Storage_DocumentDimensionNames` uses `HT` for the persisted invoice type identity, abbreviated as `InvoiceType` for display:

| Id | ScopeHash | Scope | NameHash | Name |
| --- | --- | --- | --- | --- |
| `701` | `HT` | `InvoiceType` | `H_CUSTOMER` | `customerId` |
| `702` | `HT` | `InvoiceType` | `H_YEAR` | `year` |
| `703` | `HT` | `InvoiceType` | `H_AMOUNT` | `amount` |
| `704` | `HT` | `InvoiceType` | `H_APPROVED` | `approved` |
| `705` | `HT` | `InvoiceType` | `H_ISSUED` | `issuedAt` |
| `706` | `HT` | `InvoiceType` | `H_EXTERNAL` | `externalRef` |

`__Storage_DocumentDimensions`:

| DocumentId | NameId | ValueType | IntegralValue | DecimalValue | StringValue |
| --- | --- | --- | --- | --- | --- |
| `D1` | `701` | `String` | `NULL` | `NULL` | `432D3432` |
| `D1` | `702` | `Integer` | `2026` | `NULL` | `NULL` |
| `D1` | `703` | `Decimal` | `NULL` | `995.25` | `NULL` |
| `D1` | `704` | `Boolean` | `0` | `NULL` | `NULL` |
| `D1` | `705` | `DateTime` | `639303300000000000` | `NULL` | `NULL` |
| `D1` | `706` | `Null` | `NULL` | `NULL` | `NULL` |

The ticks represent `2026-11-15T09:00:00Z`. `year == 2026 AND approved == false` matches `D1` within the allowed type/partition, subject to expiration. Boolean `0` is a populated value, not null. `D2` has no dimension rows. The same logical names have independent IDs in blob/document dictionaries; the tables never participate in a combined catalog query.

## Dimension queries

Add optional feature-owned `DimensionFilter` inputs to `BlobQuery`, `DocumentQuery`, and `DocumentCountQuery`, with corresponding feature-specific builders. Existing `Properties` do not participate. No dimension filter preserves existing key-query behavior.

Each predicate carries a logical name, explicit scalar kind, operator, and typed operand where needed. Initial operators are equality/inequality, ordered comparisons for integer/DateTime and supported decimal profiles, existence, missing, explicit null, and non-null. SQLite initially supports decimal equality only among value comparisons. String ordering is unsupported in all initial profiles; a future implementation requires a separate advertised capability and specification. Support bounded AND/OR groups; defer general NOT, substring search, arbitrary SQL/delegates, and payload field access. Do not use `FilterModel.ApplyFilter` reflection over a JSON bag as the Dimensions translator.

Resolve names and canonical operands before building value predicates. A non-null value comparison must constrain `NameId` and `ValueType` and use the appropriate typed slot. Equality and inequality require a present, non-null value of that kind; missing, explicit null, or other kinds match neither. Existence/null predicates express those states directly. An unknown dictionary name must preserve these rules without a read-side registration.

Filter and combine predicates before page limiting and counts. Use duplicate-free semijoins/EXISTS or equivalent database plans; do not transfer unbounded owner-ID lists into application memory. Preserve existing logical-key result order and keyset continuation. The user accepted the three-column/dictionary changes, not a new result-order contract. A value index does not automatically eliminate sorting by the parent's logical key; qualify that cost explicitly.

Document `FindPageAsync`, `ListPageAsync`, and `CountAsync` use identical dimension semantics. Key pages/counts do not deserialize payloads; payload pages read only selected documents. Blob `ListPageAsync` returns matching metadata; no unrelated blob count API is introduced here.

A positive indexed dimension predicate may anchor a query without a name prefix/exact key. Empty filters do not bypass full-scan consent. Missing-only/negative predicates remain broad unless narrowed by an accepted positive anchor. Every OR branch must be allowed. Preserve mandatory container/type/partition boundaries, document logical expiration and fixed page-sequence cutoff, and existing live-pagination semantics.

Continuation hashes bind logical names, kinds, canonical values, operators, groups, scope, operation, and existing page settings. A change to Dimensions predicates invalidates the token. Do not bind tokens to internal `NameId` values or promise a snapshot across pages/counts.

## Provider capabilities

Extend each feature's existing capabilities with its own dimension descriptor. Separate `SupportsDimensions` for persistence/round trips from dimension-query support. Defaults are unsupported. Document Storage can reuse its own `DocumentQuerySupport`; Blob Storage must not depend on the document enum.

Descriptors communicate support per public kind/operator, indexed/backend execution, bounded groups, name/value limits, snapshot/key projection, and string semantics. Public types are five even though physical search slots are three. Property persistence capabilities remain unchanged and do not imply Dimensions support. Calls that omit new Dimensions inputs remain valid under their existing contracts.

| Provider | Dimension persistence target | Query target |
| --- | --- | --- |
| EF SQL Server / PostgreSQL | Separate snapshot plus feature-owned dictionary/value tables | Indexed verified operators over three search columns |
| EF SQLite | Same logical model; exact persisted values | Indexed only for verified native predicates; decimal operator limits explicit |
| InMemory storage providers | Unsupported | Unsupported |
| Azure Blob / Azure Table document providers | Unsupported | Unsupported |
| Filesystem | Unsupported | Unsupported |
| Cosmos / EF Cosmos document providers | Unsupported | Unsupported |
| Other EF database engines | Unsupported until separately qualified | Unsupported until separately qualified |

The table is initial implementation scope, not a claim that other backends cannot ever support Dimensions. Non-EF providers do not store Dimensions snapshots, name dictionaries, or values in this change. Reject any explicit Dimensions write input, dimension-update operation, or dimension filter before I/O, including explicit empty input on an unsupported write path. New write inputs default to null/omitted so existing calls remain valid. Preserve Properties operations and existing key-query behavior, and return an empty Dimensions collection on ordinary metadata reads where none is stored.

The initial EF operator targets are:

| Operation | SQL Server | PostgreSQL | SQLite |
| --- | --- | --- | --- |
| Integer equality/inequality and ranges | Indexed | Indexed | Indexed |
| Boolean equality/inequality | Indexed | Indexed | Indexed |
| UTC DateTime equality/inequality and ranges | Indexed | Indexed | Indexed |
| Decimal equality | Indexed | Indexed | Indexed, exact-value and plan proof required |
| Decimal inequality and ranges | Indexed | Indexed | Unsupported |
| Exact string equality/inequality | Indexed encoded representation | Indexed encoded representation | Indexed encoded representation |
| Exists, missing, null, non-null | Backend execution | Backend execution | Backend execution |
| Bounded AND/OR | Backend execution | Backend execution | Backend execution |
| String ordering | Unsupported | Unsupported | Unsupported |

Here, indexed means the corresponding slot is eligible for the dimension search index, not that every predicate is selective. Inequality, missing, common values, broad ranges, and counts may examine many rows and retain the existing broad-query consent rules. Advertise each target only after its implementation proof passes. Validate unsupported operators before executing a query, even when another branch could match without them.

No cloud shadow catalog, filesystem index, or InMemory dimension evaluator is introduced. Allowing full scans or disabling existing client-side key-query restrictions cannot override unsupported Dimensions capabilities. Dimension-query document counts additionally require count support. EF scalar/operator differences remain explicit, particularly SQLite decimal comparisons.

### Dimensions mutation concurrency

Commit the owner version/modified timestamp, Dimensions snapshot, name registrations, and value-row delta in one atomic unit under the existing provider transaction/lease/concurrency mechanism. Validate the condition before changing any of them. Missing/stale owners, failed create-only checks, and cancellation must not leave a partially changed snapshot/value set or publish uncommitted dictionary-cache entries. Dimensionless writes retain their current fast paths and ETag behavior.

Copies apply the existing destination condition to the destination owner's complete version, including Dimensions. Moves delete the source only if the source's originally read ETag still matches. Bulk operations retain their current ordered per-item/prefix-commit failure contract; a condition conflict must not retry the failed item without its original condition. Dimension-filtered reads and continuation tokens remain live queries; an item ETag does not create a snapshot across result pages.

Implementation tests must exercise both directions of the cross-field conflicts: read version V, change Dimensions, then attempt content/Properties/expiration/delete with V; and read V, change one of those fields, then attempt Dimensions with V. Include matching/unconditional success, create-only plus ETag rejection, deletion before save, competing writers during dictionary registration, rollback/compensation races, helper/transform/batch condition forwarding, committed-version response metadata, and HTTP header/body mapping. Preserve the aligned Properties and private transform state in every case. Unsupported Dimensions providers still reject explicit Dimensions before I/O; their supported ETag updates remain available for content and Properties.

## High-volume requirements

If `N` records have an average of `D` Dimensions, the value table contains approximately `N * D` rows. Dictionary size follows distinct names per persisted scope, not the number of values. Reusing a name adds value rows without updating dictionary reference counters. Each of the three ordinary search indexes contains every value row, including rows whose corresponding slot is null. These indexes do not eliminate all write/storage amplification.

- Keep hot keys to `NameId`, `ValueType`, the selected value, and owner ID. Do not repeat name strings, name hashes, scope text, payloads, or JSON snapshots in value rows. Measure owner/primary-key locator bytes too.
- Resolve/register names in bounded batches and use a bounded positive cache. Existing names do not cause repeated dictionary writes. Avoid global locks, per-dimension owner checks, per-result name lookups, and `SaveChanges`/round trips per dimension.
- Let selective dimension indexes drive candidate retrieval. Do not force a full parent scan with one child probe per parent. Inspect plans for AND/OR combinations, common Boolean values, broad ranges, missing dimensions, and late pages.
- SQL remains parameterized. Normalize names/operands before querying; do not invoke JSON extraction, hashing, or casts on indexed value columns. Keep duplicate elimination, predicate intersections, and ordering in the database, with bounded result materialization.
- Preserve Properties-only and dimensionless fast paths. Identical Dimensions cause no dimension DML; a one-value change updates its delta without clearing/reinserting all values or rewriting content.
- Batch value mutations under the parent's existing atomic/concurrency boundary. Bound tracker size, transactions, cleanup batches, allocations, and cache size. Reads/counts may still be expensive for large matches; page size alone is not a database work bound.

Technical basis: [SQL Server index design](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-index-design-guide?view=sql-server-ver17), [EF efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying), and [EF efficient updating](https://learn.microsoft.com/en-us/ef/core/performance/efficient-updating). The proposed benefits are structural expectations, not benchmark results.

## Portability and adoption

The three physical slots use `long`, `decimal`, and bounded `string` properties with ordinary annotations. UTC ticks avoid native timestamp mapping/precision differences without a value converter. A provider must still validate public integer/Boolean/tick ranges during encoding and decoding.

Decimal precision/scale is fixed at `18,6`. SQL Server/PostgreSQL target indexed decimal equality and ranges; SQLite initially targets indexed equality only. SQLite EF translations can use functions/collation rather than native numeric storage. Do not advertise an indexed operator without plan/semantics proof or silently convert decimal to `double`. String equality is case-sensitive and ordinal, including significant trailing spaces and distinct Unicode normalization forms. The 512-character ASCII hexadecimal search representation preserves exact UTF-8 identity while using ordinary `[MaxLength]`/`[Unicode]` annotations. Account for its expansion in storage budgets and qualify the actual index plans/collation profiles. Name canonicalization is verified on bounded dictionary candidates before value querying; it is not a database collation assumption.

Use one annotated model per feature and separate migrations for SQL Server, PostgreSQL, and SQLite. Verify generated numeric dictionary IDs, composite keys, foreign keys, digest comparisons, index widths, and delete behavior on each engine. Creating tables alone does not establish query efficiency. References: [EF index annotations](https://learn.microsoft.com/en-us/ef/core/modeling/indexes), [generated keys](https://learn.microsoft.com/en-us/ef/core/modeling/generated-properties), [multiple-provider migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers), and [SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).

Add the new Dimensions schema/APIs and migrate affected repository consumers and examples together. Breaking Dimensions/context changes are allowed; no existing Dimensions data or clients require compatibility or backfill. Do not rewrite, reinterpret, migrate, or backfill existing Properties. Old records simply have no Dimensions. Resource recreation is permitted for development adoption, not a prerequisite to changing an existing JSON property bag.

No property-to-dimension copy worker, legacy queryable-properties format, staged dual-format rollout, or compatibility reader is needed. Keep application-specific execution evidence outside `docs/`. Existing content integrity, encryption, expiration, and metadata behavior remain outside this design change.

## User stories and acceptance criteria

### Keep simple metadata separate

As an application developer, I want Properties and Dimensions to remain independent so that descriptive metadata does not automatically become indexed query data.

1. The aligned Properties contract passes its scalar round-trip and preserve/replace/clear regression tests without adopting Dimensions. Dimensions introduces no further Properties format or behavior change.
2. Writing/updating Properties does not register names, create dimension values, or affect a dimension query.
3. The same name can have different property/dimension values; filters return only dimension matches, including the sample where a property alone must not match.
4. Invalid Dimensions fail independently before mutation and do not impose their scalar restrictions on Properties.

### Query runtime dimension names

As an application developer, I want supplied dimension names indexed automatically so that adding a dimension does not require a schema change or application-side registration.

1. A successful write makes every accepted dimension queryable on capable providers in the same atomic commit.
2. Concurrent registration of a new name converges on one scoped dictionary identity; name/value kinds remain independent.
3. Predicates, payload/key pages, and document counts agree before/after updates, clear, type changes, deletion, and retention; unsupported shapes fail before I/O.
4. Querying an unknown name never registers it. Dictionary cache misses/collisions do not produce false matches or hide committed data.

### Keep Dimensions optional and scalable

As an application developer, I want dimensionless and populated records to retain predictable storage costs so that Dimensions do not limit the existing high-volume storage paths.

1. Omitted/empty Dimensions on insertion create no names or values, and ordinary dimensionless/Properties-only paths access no dimension tables.
2. Existing record replacement preserves omitted Dimensions; an explicit empty collection clears value rows but preserves Properties and dictionary names.
3. Selective predicates can use the typed indexes before bounded materialization; there are no per-item/per-dimension round trips or unbounded application-side candidate lists.
4. Unchanged Dimensions cause no value DML; changed values maintain only their delta and retain ETag/cancellation atomicity.

## Verification and performance gates

Documentation-only work does not implement or qualify these requirements. Implementation must provide:

1. Separate blob/document suites for the five public kinds, explicit null/missing, shared integral-slot discrimination, UTC tick round trips, scalar limits, capabilities, query hashes, logical scope, and Properties independence.
2. Annotation-only model/migration tests for each feature separately and both together. Verify two added tables per feature, three value search indexes, dictionary uniqueness, no redundant indexes, database-generated IDs propagated into value FKs, and no custom model configuration, supplied IDs, dictionary seeds, or per-name migrations.
3. Real SQL Server, PostgreSQL, and SQLite tests for dictionaries, hash collisions, concurrent first registration, failed/cancelled commits, positive cache invalidation, property-only writes, bulk updates, delete/retention, duplicate-free AND/OR, keyset paging, document counts, and logical expiration.
4. Unsupported-provider contracts for InMemory, Azure, filesystem, Cosmos, and unqualified EF engines. Verify storage and query capabilities are false, explicit dimension inputs/queries fail before I/O, and aligned Properties/key operations remain unchanged by Dimensions. No non-EF dimension storage, evaluator, filesystem/cloud scan, or implicit property promotion appears.
5. Updated feature guides, public XML examples, diagnostics, and existing API/CLI adapters. Provide capability-aware Dimension query controls on each dashboard, preserving filters through paging and applying the same document filter to rows and counts. Do not log dimension values.

### Reference workload and acceptance targets

On SQL Server and PostgreSQL, qualify each feature at 10,000, 100,000, and 1,000,000 owners, with eight Dimensions per populated owner. The largest populated case has eight million value rows per feature. Run sparse/dimensionless mixes separately, then a combined deployment to verify that the independent features coexist. Ten-million-owner qualification is outside the initial acceptance scope; do not extrapolate an unmeasured capacity claim.

SQLite remains functionally supported with its advertised operator restrictions. Its performance is informational: degraded latency/throughput relative to server databases is accepted. Keep correctness, exact type semantics, atomicity, ETags, bounded work and native index-use proofs. Retain short representative diagnostics; long SQLite performance qualification and further SQLite-specific tuning for small gains are outside the current release gate. The latency/overhead targets below apply to SQL Server and PostgreSQL.

Prioritize measured scaling over small constant-factor gains. For each server engine, compare 10,000, 100,000 and 1,000,000 owners with eight Dimensions per populated owner, reaching eight million value rows per feature. Use comparable predicate kinds, fixed page sizes and documented match counts/selectivity. Distinguish increased output work from lookup growth. Report median run-p95, throughput, actual rows/reads, sorts/spills, plan/index access, snapshot/table/index bytes, and single-value update cost at each size. Inspect latency ratios for tenfold dataset growth and investigate disproportionate increases; three observations alone cannot prove an asymptotic bound or exclude exponential growth. Bounded selective plans must not deteriorate into full parent scans. Report broad/common-value/count queries separately because their cost can grow with matches.

A focused scaling run may skip ordinary-operation saturation calibration/comparison and allocation-attribution loops while retaining three full two-minute mixed query/update measurements, correctness preflight, plans and storage evidence. In focused scaling mode, measure selective pages with concurrent dimension writers separately from broad predicates/counts with concurrent writers. Keep three two-minute intervals for each group, record every failure, and report each workload explicitly. This isolates lookup growth from broad-query saturation. Such a run establishes only its measured scaling checks; the original combined read/count workload and skipped overhead/allocation gates remain incomplete. Preserve earlier mixed-load failures, and do not present isolated timings as proof that the original mixed-load target passed. Begin with combined-feature server runs for early feedback, then complete any remaining server qualification scope. No timing or capacity result is inferred for an unmeasured size.

Record a reproducible reference machine with eight vCPUs, 32 GiB RAM, local NVMe storage, database/runtime versions, database settings/collation, dataset seed, and warm-up. Share that machine budget between the benchmark application and its database; report the actual CPU/storage specifications and cache residency. Run engines sequentially. For server databases exercise 16 concurrent readers with four writers; for SQLite exercise 16 readers with one writer and report the configured journal/busy policy. Report cold-dictionary and warm-cache runs separately, including registrations from more than one provider instance. Use at least three measured runs per target scenario, each with two minutes of steady load after warm-up; report every run and compare the medians of the run-level p95 values.

Resource-capped local runs are accepted for this reference budget. Enforce the shared application/database CPU and memory limits, report the larger physical host separately, and retain evidence of the effective caps. An uncapped run on a larger host does not satisfy this requirement.

Use a documented representative distribution covering all five kinds, explicit null, shared names, ten logical scopes, 32 reused names per scope, and 32-byte UTF-8 strings. Add skew, many distinct names with no quota, hot scopes, maximum-length strings/names, maximum collections/filters, registration bursts, and mixed reads/writes as separate stress cases. State exact kind/null proportions and predicate selectivity in each result. Compare unchanged operations against the optimized parent-only baseline with the same payloads and concurrency.

Ordinary-operation overhead comparisons use the same fixed offered request rate for both models, with the same reader/writer limits. Measure separate parent-only and Dimensions saturation capacity in three alternating two-minute intervals after warm-up. Freeze separate read/write request rates at 70% of the median parent-only saturation throughput before comparisons; do not tune rates from Dimensions results. Report those rates and capacity measurements. Use deterministic per-worker arrivals and include scheduler/queue delay in end-to-end latency from the scheduled arrival. A request not started within 250 ms is missed, not silently discarded; any missed or failed request fails the comparison. Report queue-delay percentiles, offered/completed/missed counts and actual interval/drain duration. Keep selective-query/dimension-delta mixed-load qualification and the 10% latency/allocation limits unchanged. Saturation capacity is separate evidence and does not supply the ordinary-operation overhead percentile.

Release-specific acceptance decision, 2026-11-19: the current-model SQL Server combined million-owner case may be accepted with its recorded 65 missed writer-start deadlines out of 956,400 scheduled requests in one comparison window, together with the three explicitly accepted ordinary-write overheads below. All storage operations in that window succeeded, and the other five comparison windows had no misses. Keep the strict comparison failure in the evidence. This exception applies only to that recorded window; future comparisons retain the zero-miss rule. The [implementation plan](../../plan/pln-feature-storage-dimensions-1.md#finalization-and-release-acceptance) records completed finalization and accepted boundaries.

The user also accepted these exact SQL Server ordinary-write p95 results for this release: blob Properties 16.53→19.00 ms (+14.94%), blob dimensionless content 17.21→19.11 ms (+11.04%), and document Properties 10.47→11.76 ms (+12.32%). All other checks passed. Preserve the original failed 10-percent comparisons; this decision changes no future target and establishes no deployment SLA.

Initial acceptance targets on that reference workload are:

- Warm selective equality/range pages of 100 owners, with 200 to 1,000 matching owners within the allowed scope in the million-owner dataset, have p95 provider-operation latency at or below 250 ms. Measure first and late continuation pages; include both payload/key projections where applicable. Apply this target only to advertised indexed operators, using fixed 1 KiB payloads and reporting payload cost separately from metadata cost.
- Established-name single-value changes have p95 at or below 250 ms under the stated mixed workload. Identical Dimensions produce no value DML. Properties-only/dimensionless operations access no dimension tables and increase warm p95 latency and per-operation allocations by no more than 10% versus the parent-only baseline.
- In the representative mixed-type dataset, incremental dictionary, value-table, all index, and parent-snapshot storage averages at most 1 KiB per dimension value. Measure the parent-snapshot increment against identical owners without Dimensions, excluding payload and unchanged parent storage. Report maximum-string stress storage separately; the representative average is not a per-row storage guarantee.
- No selective plan scans all parents or materializes unbounded candidates in the application. Cache entries, tracker size, mutation/cleanup batches, and registration attempts remain bounded as specified. Report broad predicates/counts and registration bursts separately; they have no 250 ms promise.

These are design acceptance targets, not measurements or deployment service-level guarantees. If they fail, record the measured bottleneck and revise the implementation or seek an explicit specification change. Do not conceal an unsupported/index-defeating representation with client evaluation.

Required scenarios include selective equality, integral/decimal/date ranges, Boolean/common-value predicates, several AND/OR terms, first/late pages, counts, insert, identical-Dimensions overwrite, one-value/type change, clear, bulk deletion, and retention. Compare unchanged operations against the optimized parent-only baseline.

Report latency distributions, rows examined/logical reads where available, commands/round trips, allocations/working set, sorting/spills, transaction/log work, dictionary and value-table bytes, each index's bytes, and throughput at stated concurrency. Include snapshot duplication, all three ordinary indexes' null entries, and dictionary-cache bounds. Never infer speed or capacity from schema counts alone.

Acceptance requires selective indexed plans without parent-wide scans, bounded materialization, no dimension work on unchanged simple-property paths, batched delta writes, safe name registration, and storage/write costs within agreed budgets. Revise the annotation-compatible representation if it fails; do not silently add filtered indexes or disable automatic indexing.

## Final decisions and implementation order

The decision pass closes the draft questions:

| Decision | Accepted contract |
| --- | --- |
| Decimal | `decimal(18,6)`; exact validation; SQLite initially queries decimal equality only |
| String values | Exact case-sensitive equality, spaces significant; 256 UTF-8 bytes; uppercase ASCII hexadecimal search representation |
| Dimension names | ASCII identifier grammar, maximum 128 characters; ASCII-uppercase identity and `NameHash`; preserve supplied spelling |
| Dictionary scope | Existing persisted container/type identity, maximum 256 UTF-16 code units |
| Content replacement | Omitted/null preserves; empty clears; non-empty replaces |
| Public ceilings | 64 Dimensions, 16 predicates, 16 groups, four group levels |
| Dictionary growth | No name quota; applications manage cardinality; unused names retained |
| Internal bounds | 4,096 positive cache entries, four concurrent new-name registrations per provider instance, three race-resolution attempts |
| Qualification | SQL Server/PostgreSQL: one million owners and eight Dimensions per populated owner per feature, with the reference targets above. SQLite: correctness and short diagnostics; performance informational. |

The completed implementation follows these proof stages. Future changes must preserve the annotation-only schema, verified search semantics, independent feature ownership, ETag-safe atomic publication and the documented provider envelope.
