<img class="bdk-doc-icon" src="assets/features/serialization.svg" width="32" height="32" alt="" align="right">

# Common Serialization

> Serialize object instances as JSON, MessagePack, CBOR, or CSV through one shared contract.

[TOC]

`Common.Serialization` is the shared serialization layer used across the devkit. It provides a small serializer abstraction, several concrete serializers, versioned value codecs, Base64Url and scalar codecs, continuation-token encoding, converters for common devkit types, and the default JSON conventions for results, filtering, and metadata objects.

This package matters because many higher-level features depend on consistent serialization behavior:

- messaging and outbox storage
- document and file storage
- filtering payloads
- HTTP payload helpers
- result and error serialization

## Core abstractions

### `ISerializer`

`ISerializer` is the base contract for stream-based serialization:

- `Serialize(object value, Stream output)`
- `Deserialize(Stream input, Type type)`
- `Deserialize<T>(Stream input)`

### `ITextSerializer`

`ITextSerializer` extends the serializer model for text-based formats. Use it when the transport or persistence format is naturally textual, such as JSON or CSV.

## Built-in serializers

### <img class="bdk-doc-icon" src="assets/features/format-json.svg" width="24" height="24" alt=""> `SystemTextJsonSerializer`

The System.Text.Json-based serializer for application-facing JSON work. Use this when you want:

- modern .NET JSON support
- integration with the devkit's JSON defaults
- good compatibility with HTTP APIs and internal app models

### <img class="bdk-doc-icon" src="assets/features/format-json.svg" width="24" height="24" alt=""> `JsonNetSerializer`

A Newtonsoft.Json-based alternative for scenarios that need its contract model or compatibility surface. This is the fallback when you need JSON.NET-specific behavior rather than the standard devkit JSON path.

### <img class="bdk-doc-icon" src="assets/features/format-messagepack.svg" width="24" height="24" alt=""> `MessagePackSerializer`

A binary serializer for internal payloads. `DefaultSerializer.Create` is a shared `MessagePackSerializer` instance, so the package's static default is binary rather than human-readable.

### <img class="bdk-doc-icon" src="assets/features/format-cbor.svg" width="24" height="24" alt=""> `CborSerializer`

A binary serializer for [Concise Binary Object Representation (CBOR)](https://www.rfc-editor.org/rfc/rfc8949), implemented directly with Microsoft's `System.Formats.Cbor` package. Object instances map to CBOR without an intermediate JSON document or Base64 conversion. Reflection discovers each type's members and constructors, and compiled accessors and collection codecs are cached for subsequent operations.

The object mapping supports:

- Public instance properties and fields using their CLR names, including inherited properties, private setters, and init setters. `[DataMember(Name = "wire_name")]` changes the encoded name, and `[IgnoreDataMember]` excludes a member from writes and reads.
- Public and private parameterless constructors, including explicit struct constructors and their field initializers. Types without a parameterless constructor use a constructor whose parameter names and types match their members, including immutable records. `[CborConstructor]` selects a specific public or nonpublic constructor, even when a parameterless constructor exists.
- Nested objects, arrays, generic collections, sets, stacks, and dictionaries. Collection interfaces use standard concrete implementations on reads.
- Native CBOR byte strings for `byte[]`, `Memory<byte>`, `ReadOnlyMemory<byte>`, and `ArraySegment<byte>`, including nested members, dictionary values, and object-typed values. An array segment encodes its selected bytes; deserialization creates a new segment with offset zero. Binary payloads have only CBOR framing overhead.
- Signed and unsigned integers, 128-bit integers, `BigInteger`, `Half`, `float`, `double`, `decimal`, booleans, characters, strings, nullable values, and enums. Floating-point values use native CBOR floats. Decimals use decimal-fraction tag 4, large integers use bignum tags, and enums use their underlying integer type.
- `Uri` values as their original text, preserving relative and absolute forms, and `System.Version` values as component-preserving text.
- GUIDs as tagged UUID byte strings, dates with date-time tag 0 or round-trip text for unspecified dates, and invariant text for `TimeSpan`, `DateOnly`, and `TimeOnly`.

`CborSerializer` ignores null values and null output streams. Null or empty input streams return null from runtime-type deserialization and `default(T)` from generic deserialization. Reads rewind seekable streams and consume non-seekable streams from their current position. Writes start at the output stream's current position. Streams remain open.

The byte-array adapters use an internal buffer capability for `CborSerializer`. Serialization returns a directly encoded, independently owned array, avoiding an intermediate `MemoryStream` and its backing-array copy. Deserialization reads the supplied bytes directly without constructing a stream or copying the encoded input into a rented buffer. Decoded byte strings remain independently owned arrays. The public `ISerializer` API and the stream fallback for other serializers are unchanged. Subclasses retain the stream fallback so their own interface implementations are respected.

Writer reuse is bounded to six writers whose largest completed payload was at most 64 KiB and two writers whose largest completed payload was at most 1 MiB. The largest completed size stays with each writer across operations, so a subsequent small write cannot move a large writer into the small pool. Larger writers and failed writes are discarded. Reuse reduces repeated allocations at the cost of retaining larger buffers; these payload-size thresholds are not exact limits on the writer's internal capacity or bookkeeping memory. Readers retain their existing bounded pool and 64 KiB input threshold, and reset without holding the caller's input.

Stream writes still use a rented temporary byte buffer, and reads use exposed memory-stream buffers where available. `CborWriter` buffers the complete output before writing to the destination. Non-seekable input streams require buffering because `CborReader` requires the complete encoded value.

CBOR recognizes a subset of the BCL data-contract attributes from `System.Runtime.Serialization`. `[DataContract]` on the target type opts in only members marked `[DataMember]`. Without `[DataContract]`, public members retain the default convention and `[DataMember]` can opt in nonpublic fields or properties. Annotated private members declared on base classes are included. `[IgnoreDataMember]` takes precedence over `[DataMember]`. Other data-member options, including `Order`, `IsRequired`, and `EmitDefaultValue`, are not interpreted.

Hidden CLR member names resolve to the most-derived eligible member before exclusions are applied. Distinct members cannot share a wire name. Constructor parameters always bind to CLR member names and types, independently of wire-name aliases. Only one constructor can have `[CborConstructor]`; an ambiguous or unbindable selection throws `NotSupportedException`.

```csharp
using System.Runtime.Serialization;
using BridgingIT.DevKit.Common;

[DataContract]
public sealed class Customer
{
    [CborConstructor]
    public Customer(string name) => this.Name = name;

    [DataMember(Name = "customer_name")]
    public string Name { get; }

    [DataMember]
    private string InternalCode { get; set; }
}
```

JSON settings, attributes, and converters do not apply to this binary serializer. Unknown object members are skipped, including nested values. Getter-only members are populated through matching constructor parameters when available. Members without setters or matching constructor parameters are ignored on reads.

CBOR carries no CLR type names. Reads targeting `object` produce natural scalar values, byte arrays, lists, and dictionaries. Polymorphic members therefore require a concrete target type to reconstruct a particular CLR class. Cyclic graphs, multidimensional arrays, unsupported constructors or tags, and excessive nesting are rejected. The default depth limit is 64. Duplicate map keys and trailing data are rejected.

`SerializeToBytes` returns CBOR bytes. `SerializeToString` returns Base64 of the complete CBOR payload because the extension returns text. Use streams or byte arrays to keep transport and storage binary.

### <img class="bdk-doc-icon" src="assets/features/format-csv.svg" width="24" height="24" alt=""> `CsvSerializer`

A CSV-oriented serializer for tabular data scenarios. This is useful when the consuming feature naturally works with rows and flat data rather than object graphs.

### <img class="bdk-doc-icon" src="assets/features/compression.svg" width="24" height="24" alt=""> `CompressionSerializer`

A decorator that wraps another serializer and adds compression. Use this when payload size matters more than raw readability and you want to keep the underlying serialization format unchanged.

## <img class="bdk-doc-icon" src="assets/features/codecs.svg" width="24" height="24" alt=""> Shared codecs

### Versioned value codecs

`IValueCodec<T>` encodes and decodes one declared value contract through `ValueTask<Result>` and `ValueTask<Result<T>>`. Its stable `FormatId` and positive `FormatVersion` identify persisted bytes independently of CLR type names. Both methods leave caller-owned streams open.

`ValueCodecCapabilities` declares forward-only access, seek requirements, graph materialization, native asynchronous I/O, finite scratch estimates, and raw-byte seeking and ranges. Stream-shaped methods alone do not establish these properties. `SystemTextJsonValueCodec<T>` uses forward-only native asynchronous stream I/O and materializes the decoded graph. `SerializerValueCodec<T>` adapts an existing synchronous `ISerializer` with explicitly declared seek and scratch requirements. Returning a `ValueTask` does not make the underlying serializer asynchronous or interruptible.

```csharp
using BridgingIT.DevKit.Common;

IValueCodec<ReportSummary> codec = new SystemTextJsonValueCodec<ReportSummary>();

await using var bytes = new MemoryStream();
var encoded = await codec.EncodeAsync(new("Quarterly report", 120), bytes);
if (encoded.IsSuccess)
{
	bytes.Position = 0; // Read the bytes just written to this caller-owned stream.
	var decoded = await codec.DecodeAsync(bytes);
	if (decoded.IsSuccess)
	{
		Console.WriteLine(decoded.Value.Title); // Quarterly report
	}
}

public sealed record ReportSummary(string Title, int Rows);
```

`IValueCodecValidator<T>` is an explicit exact-format streaming-validation contract. A validator consumes the complete input, including trailing content, without materializing an arbitrary graph. It is registered separately; ordinary decoding does not imply that capability. `ValueCodecRegistry` resolves only code-approved reader registrations by declared contract and persisted format/version.

These abstractions belong to Common. They supply no provider, expiration, source ownership, or hosting policy. [Application Caching](features-application-caching.md#codecs-compression-and-hot-entries) uses them for its bounded persisted representations and owns its cache compression and envelopes. Use [Common Caching](common-caching.md) for basic in-process cache access.

### Register versioned codec readers

Use `AddValueCodecs` to register trusted codec implementations and the shared `ValueCodecRegistry`:

```csharp
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

services.AddValueCodecs(codecs => codecs
	.WithCodec<ReportSummary, ReportCodecV1>()
	.WithCodec<ReportSummary, ReportCodecV2>(isDefault: true));
```

`ReportCodecV1` and `ReportCodecV2` are application-provided implementations of `IValueCodec<ReportSummary>`. Both declare `FormatId = "report"`, with `FormatVersion` values of `1` and `2`. `WithCodec<T, TCodec>()` registers the concrete codec as a singleton and resolves constructor dependencies through DI. An existing concrete codec registration is preserved. Codec instances must support concurrent calls.

For an already configured instance, use the instance overload:

```csharp
services.AddValueCodecs(codecs => codecs
	.WithCodec(new SystemTextJsonValueCodec<ReportSummary>(jsonOptions), isDefault: true));
```

Resolve a reader using the format ID and version stored with the payload:

```csharp
var registry = serviceProvider.GetRequiredService<ValueCodecRegistry>();
var format = new ValueCodecFormat("report", 1);
if (registry.TryGet<ReportSummary>(format, out var reader))
{
	var decoded = await reader.DecodeAsync(content, cancellationToken);
}
```

The registry matches the exact value type, format ID, and format version. An unregistered combination returns `false`; it does not select a newer reader. Duplicate registrations for the same combination throw when the registry is resolved.

Set `isDefault: true` to select the writer resolved as `IValueCodec<T>`. The writer uses the same instance as its registered reader. A later explicit default selection replaces the previous writer for that value type; other reader registrations leave it unchanged. Without an explicit default, these methods register readers only. They do not replace `ISerializer`. Retain older readers while their payloads exist.

The [Blob Storage codec example](features-storage-blobs.md#store-with-a-versioned-codec-and-compression) combines versioned readers, blob properties, and transparent GZip compression.

### Streaming payload transforms

`IStreamingPayloadTransform` describes a reversible transformation over streams without choosing a storage provider, value model, or persisted envelope. `FormatId` and `FormatVersion` identify its bytes. `MaximumStateBytes` supplies a conservative per-stream workspace estimate for resource admission; input buffers, output buffers, retained data, and transfer limits are separate responsibilities.

Implementations support forward-only access, create independent per-transfer streams, and do not buffer the complete value. Encoding streams must be disposed before their final output is consumed. The implementation defines whether disposal closes the supplied stream and validates its format parameters before returning logical bytes.

`GZipPayloadTransform` implements version-one gzip with the BCL compression streams. It accepts no parameters, uses `CompressionLevel.Optimal` by default, and leaves the supplied stream open by default:

```csharp
using BridgingIT.DevKit.Common;
using System.IO.Compression;

IStreamingPayloadTransform transform = new GZipPayloadTransform(CompressionLevel.Fastest);
using var source = new MemoryStream("Quarterly report"u8.ToArray());
using var stored = new MemoryStream();
await using (var encoder = transform.CreateEncodingStream(stored, ReadOnlyMemory<byte>.Empty))
{
    await source.CopyToAsync(encoder, cancellationToken);
}

stored.Position = 0;
await using var decoder = transform.CreateDecodingStream(stored, ReadOnlyMemory<byte>.Empty);
using var text = new StreamReader(decoder);
Console.WriteLine(await text.ReadToEndAsync(cancellationToken)); // Quarterly report
```

Pass `leaveOpen: false` when the gzip wrapper owns the supplied stream. Its one-MiB workspace estimate is an accounting estimate, not a hard memory limit. Callers also enforce their own logical-size, stored-size, integrity, and deadline contracts.

`StreamingPayloadTransformDescriptor` records a format/version and opaque encoded parameters. A consuming envelope defines parameter encoding, finite bounds, and encode order; decoding reverses that order. The descriptor does not resolve types or approve an implementation. Consumers select trusted readers for the exact persisted format/version before decoding.

[Document Storage](features-storage-documents.md) uses the shared gzip factory behind its byte-array transform API. [Application Caching](features-application-caching.md) accepts shared implementations through `WithPayloadTransform` and supplies its own trusted registry, parameter bounds, resource admission, and checked envelope. `ICachePayloadTransform` implements the shared contract, and `CachePayloadTransformAdapter` exposes an existing shared implementation to code that uses the cache interface directly.

### `Base64UrlHelper`

`Base64UrlHelper.Encode` produces canonical unpadded Base64Url text. `Base64UrlHelper.Decode` restores the bytes and rejects malformed, padded, or non-canonical values.

```csharp
var encoded = Base64UrlHelper.Encode("payload"u8);
var decoded = Base64UrlHelper.Decode(encoded);
```

Base64Url is appropriate for opaque URL segments, continuation tokens, and string-only metadata formats. It is distinct from regular padded Base64; use `Convert.ToBase64String` when a protocol requires the standard Base64 alphabet.

### `PropertyBagScalarCodec`

`PropertyBagScalarCodec` uses a versioned Base64Url envelope to preserve supported scalar types in string-only persistence systems. Legacy unprefixed values remain strings.

### `PropertyBagSerialization`

Use `PropertyBagSerialization` to store a whole scalar `PropertyBag` as a JSON dictionary. Each value is a string written by `PropertyBagScalarCodec`. Deserialization restores the original scalar types and preserves the spelling of case-insensitive keys.

```csharp
using System;
using BridgingIT.DevKit.Common;

var inspection = new PropertyBag
{
	["SampleCount"] = (ushort)42,
	["InspectionDate"] = new DateOnly(2026, 7, 15),
	["Signature"] = new byte[] { 1, 2, 3 }
};

var snapshot = PropertyBagSerialization.Clone(inspection);
var json = PropertyBagSerialization.Serialize(snapshot);
var restored = PropertyBagSerialization.Deserialize(json);

Console.WriteLine(restored.Get("sampleCount").GetType().Name); // UInt16
Console.WriteLine(restored.Get("inspectionDate").GetType().Name); // DateOnly
```

`Clone` returns `null` for a null bag and a new bag for an empty bag. For a populated bag, the clone copies byte-array contents and keeps the other values as supplied. Writing to an array in the clone leaves the source array unchanged. Deserialization also produces independent byte-array contents on each read.

`Serialize` returns `null` for both null and empty bags. `Deserialize` returns a fresh empty bag for null input, the empty string, or `{}`. A present entry with a null value survives a round trip. Legacy unprefixed values such as `"00123"` stay strings.

The JSON literal `null` raises `ArgumentNullException`. Malformed JSON raises `JsonException`, an invalid scalar encoding raises `FormatException`, and keys that differ only by case raise `ArgumentException`.

`PropertyBagJsonConverter` reads ordinary JSON tokens. An ISO date in a JSON string is read as a string, and the converter infers numeric types from JSON numbers. `PropertyBagSerialization` reads the codec's explicit type tags, so an encoded `DateOnly` or `ushort` comes back with the original .NET type.

### Plain scalar JSON

Use `ScalarPropertyBagJson.Parse` from `BridgingIT.DevKit.Common.Serialization` to read a JSON object supplied as metadata. Strings stay strings, including dates and GUIDs. Boolean and null values become `bool` and `null`. The parser tries `long` first for a number, then `decimal` if it agrees with the `double` conversion, and otherwise uses a finite `double`. A nonzero number that rounds to zero as a `double` fails validation.

```csharp
using BridgingIT.DevKit.Common;

var properties = ScalarPropertyBagJson.Parse(
    """{"sampleCount":42,"reviewed":true,"inspectionDate":"2026-07-15"}""");
if (properties.IsSuccess && properties.Value is { } metadata)
{
    Console.WriteLine(metadata.Get("sampleCount")); // 42 as Int64
}
```

Keys compare without regard to case. If the same key appears more than once, the first spelling and last value survive. Blank names, nested values, and JSON syntax errors return a `ValidationError`. Decoding a property name or string value with an unpaired surrogate escape, such as `"\uD800"`, throws `InvalidOperationException`.

Missing text, whitespace, and JSON `null` return a fresh empty bag by default. Pass `preserveMissing: true` to receive `null` for those inputs. An explicit `{}` always returns a fresh empty bag.

For stored metadata with explicit CLR type tags, use [`PropertyBagSerialization`](#propertybagserialization). For ordinary JSON with nested values, see [`PropertyBagJsonConverter`](#propertybag).

### `OpaqueContinuationTokenCodec`

`OpaqueContinuationTokenCodec` serializes purpose-bound continuation-token payloads using Base64Url. It supports unsigned values and optional protection through `IContinuationTokenProtector`.

### Continuation token protection

`HmacContinuationTokenProtector` authenticates payloads with HMAC-SHA256. `AesGcmContinuationTokenProtector` encrypts and authenticates payloads with AES-256-GCM when cursor contents also need confidentiality. Both implement the same protection contract; the reader must use the same algorithm, key, and purpose as the writer. The `p1.` prefix identifies a protected token and does not select an algorithm.

```csharp
// Load a stable 32-byte secret from host configuration or a secret provider.
IContinuationTokenProtector protector = new AesGcmContinuationTokenProtector(secret);
var cursor = new CatalogCursor("catalog:42");
var token = OpaqueContinuationTokenCodec.Serialize(cursor, "catalog.cursor.v1", protector);
var restored = OpaqueContinuationTokenCodec.Deserialize<CatalogCursor>(token, "catalog.cursor.v1", protector);

Console.WriteLine(restored.LastId); // catalog:42

public sealed record CatalogCursor(string LastId);
```

The AES protector accepts at most 16 KiB of plaintext and adds 29 bytes for the format, nonce, and authentication tag. Purposes are nonempty and bounded to 128 UTF-8 bytes. The constructor copies the key. `TryUnprotect` returns `false` for malformed or unauthenticated tokens; invalid purpose configuration throws an argument exception.

When used with `OpaqueContinuationTokenCodec`, the plaintext limit applies to the complete serialized JSON payload. The codec reports malformed tokens, failed authentication, and protection-mode mismatches through `FormatException`. A reader configured with a protector rejects unsigned tokens.

For protocols that already encode cursor bytes, static `AesGcmContinuationTokenProtector.Protect(payload, secret, purpose)` and `Unprotect(token, secret, purpose)` operate directly on the same bounded binary format. Participating nodes supply the same stable secret and purpose. Cursor structure, expiry, query binding, and secret rotation remain the consuming feature's responsibility.

## Default JSON conventions

`DefaultJsonSerializerOptions.Create()` defines the devkit's baseline System.Text.Json behavior.

Key defaults include:

- indented output
- case-insensitive property matching
- camelCase property naming
- ignoring null values when writing
- `UniversalContractResolver` as the type-info resolver
- converters for filtering models
- converters for `PropertyBag`
- converters for `Result`, `Result<T>`, and paged results
- enum serialization support

Those defaults are important because they make common devkit types work consistently without each feature having to register custom converters on its own.

## Important converters and resolvers

### Smart enumerations

The generic `EnumerationJsonConverter` types support the devkit's smart-enum pattern. `DefaultJsonSerializerOptions.Create()` does not register them because each converter requires the concrete enumeration type. Add the required converter through a `Create(...)` overload.

### Filtering

`FilterCriteriaJsonConverter` and `FilterSpecificationNodeConverter` support the filtering feature's JSON model. That is one reason the filtering docs and serialization docs are tightly related.

### Results

Result converters make `Result`, `Result<T>`, and paged results serialize in a stable way for APIs and internal workflows.

### `PropertyBag`

`PropertyBagJsonConverter` preserves the flexible metadata bag used across errors, saga data, and other extensibility points.

### Private constructors and setters

Resolvers such as `UniversalContractResolver`, `PrivateConstructorContractResolver`, and `PrivateSetterContractResolver` help the serializer work with richer domain models that do not expose public setters or public constructors.

That support is especially useful in a DDD-oriented codebase where encapsulation matters.

## Recommended usage

Use explicit serializers for application-facing code rather than relying on the static default unless the binary-first default is exactly what you want.

System.Text.Json example:

```csharp
var serializer = new SystemTextJsonSerializer(
    DefaultJsonSerializerOptions.Create());

serializer.Serialize(model, stream);
var copy = serializer.Deserialize<MyModel>(stream);
```

MessagePack example:

```csharp
var serializer = new MessagePackSerializer();
serializer.Serialize(message, stream);
```

CBOR example:

```csharp
ISerializer serializer = new CborSerializer();
var bytes = serializer.SerializeToBytes(new[] { 1, 2, 3 });
var copy = serializer.Deserialize<int[]>(bytes);

var text = serializer.SerializeToString(copy); // Base64: gwECAw==
var restored = serializer.Deserialize<int[]>(text);
```

Custom nesting limit:

```csharp
var serializer = new CborSerializer(maxDepth: 32);
```

Compression example:

```csharp
var serializer = new CompressionSerializer(
    new SystemTextJsonSerializer(DefaultJsonSerializerOptions.Create()));
```

## Choosing the right serializer

Use `SystemTextJsonSerializer` when:

- the payload is part of an HTTP API
- the data should be human-readable
- you want the standard devkit JSON conventions

Use `JsonNetSerializer` when:

- you need Newtonsoft.Json-specific behavior
- you are integrating with older code that already depends on JSON.NET settings

Use `MessagePackSerializer` when:

- the payload is internal
- throughput for small or nested DTOs matters
- existing payloads or consumers already use MessagePack

Use `CborSerializer` when:

- another system expects CBOR
- you want object instances and native binary values encoded in a standardized binary format
- you want binary payloads to use the direct byte-array path and bounded writer reuse described above

MessagePack remains `DefaultSerializer.Create`. Select CBOR explicitly for CBOR payloads. The two binary formats are not interchangeable, so stored data and consumers must use the same format when a serializer changes.

Use `CsvSerializer` when:

- the data is row-oriented
- the target system expects CSV

Use `CompressionSerializer` when:

- payload size matters
- you want to wrap an existing serialization strategy rather than change it

## Tradeoffs and caveats

- `DefaultSerializer.Create` is a shared `MessagePackSerializer` instance, not a JSON serializer.
- JSON defaults are opinionated, so if a feature needs different naming or converter ordering, create an explicit `JsonSerializerOptions` instance instead of assuming the shared defaults fit every case.
- The serializer abstraction is intentionally small. It does not try to replace ASP.NET Core formatters or model binding.
- Binary serializers reduce readability during diagnostics compared with JSON.

## Storage envelopes

`ContentTransformEnvelopeCodec` encodes versioned `bdk_` metadata for ordered payload transforms. `PropertyBagScalarCodec` preserves supported scalar property types in a `bdk_v1_` Base64Url envelope, while legacy unprefixed values remain strings. `OpaqueContinuationTokenCodec` creates purpose-bound JSON token envelopes and can use `IContinuationTokenProtector` for HMAC authentication or AES-GCM encryption and authentication.

Blob and Document Storage both use `PropertyBagScalarCodec` and `OpaqueContinuationTokenCodec`. Document Storage also uses `ContentTransformEnvelopeCodec` for transform metadata. `DocumentStoreClient<T>` applies payload transforms in registration order on writes and in reverse order on reads, then verifies the stored and logical SHA-256 hashes before deserialization.

## Related docs

- [Filtering](./features-filtering.md)
- [Results](./features-results.md)
- [Messaging](./features-messaging.md)
- [DocumentStorage](./features-storage-documents.md)
- [DataPorter](./features-application-dataporter.md)

## Serializer benchmarks

`CanRoundTripStream_Benchmark` remains a lightweight timing check in the serializer unit tests. The standalone BenchmarkDotNet comparison lives in `benchmarks/Common.Benchmarks/SerializationBenchmarks.cs` and has no xUnit dependency.

Run the comparison in Release mode:

```sh
dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*SerializationBenchmarks*' --job short --iterationTime 500 --iterationCount 8 --warmupCount 8
```

The comparison measures serialization, typed deserialization, and round trips through the public byte-array adapters. It includes CBOR, MessagePack, System.Text.Json, JSON.NET, and JSON.NET with Deflate compression using their DevKit defaults. Each serializer processes the same small DTO, nested DTO with collections, and DTO containing 64 KiB of deterministic random binary data. Setup validates the reconstructed values before timing. Reports include elapsed time, managed allocations, garbage collections, and encoded payload size.

Mappings are warmed during setup, so these measurements describe repeated operations rather than first-use reflection or startup cost. Payload creation and validation are outside the measured methods. Short runs are useful for comparisons on one machine; use longer runs and representative application payloads before making production performance decisions. CSV, Markdown, and HTML reports are written to `BenchmarkDotNet.Artifacts/results` by default.

### CBOR and MessagePack results

The latest run compares the optimized CBOR serializer with the current DevKit MessagePack wrapper. Mean warm round-trip times and managed allocations were:

- Small DTO: CBOR took 3.01 microseconds and allocated 912 bytes. MessagePack took 1.12 microseconds and allocated 960 bytes. Encoded sizes were 118 and 105 bytes, respectively.
- Nested DTO: CBOR took 11.73 microseconds and allocated 5,760 bytes. MessagePack took 3.62 microseconds and allocated 3,456 bytes. Encoded sizes were 476 and 442 bytes, respectively.
- DTO containing 64 KiB of binary data: CBOR took 12.95 microseconds and allocated 133,280 bytes. MessagePack took 14.98 microseconds and allocated 197,704 bytes. Encoded sizes were 65,658 and 65,644 bytes, respectively.

CBOR allocated less for the small and binary DTOs. MessagePack remained faster for small and nested DTOs. The direct buffer path currently applies to CBOR; these results compare the DevKit wrappers as configured. They describe one workstation and these payloads, rather than a universal ranking of the formats.

The current optimization work stops at the buffer adapters and bounded writer reuse. The object mapper still boxes scalar values and uses intermediate collections for some target types. Typed member codecs and direct collection construction remain candidates for a future change if application profiling shows serialization is significant. The benchmarks do not measure cold startup or first-use mapping cost.

The [benchmark source](https://github.com/bridgingIT/bITdevKit/blob/main/benchmarks/Common.Benchmarks/SerializationBenchmarks.cs) defines the payloads and validation. Reports generated by the command above contain measured timings, allocations, and encoded sizes.

## NDJSON record streaming

`NdjsonWriter` and `NdjsonReader` support bounded asynchronous UTF-8 JSON record streams independently of `ISerializer`. They leave caller-owned streams open. Owned encoded records and borrowed received records expose the JSON, delimiter, and complete framed bytes for exact budgets and digests. See [Presentation Streaming](features-presentation-streaming.md) for APIs, strict framing, ownership, and limits.