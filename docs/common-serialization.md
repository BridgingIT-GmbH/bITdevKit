<img class="bdk-doc-icon" src="assets/features/serialization.svg" width="32" height="32" alt="" align="right">

# Common Serialization

> Serialize object instances as JSON, MessagePack, CBOR, or CSV through one shared contract.

[TOC]

`Common.Serialization` is the shared serialization layer used across the devkit. It provides a small serializer abstraction, several concrete serializers, Base64Url and scalar codecs, continuation-token encoding, converters for common devkit types, and the default JSON conventions for results, filtering, and metadata objects.

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

### `Base64UrlHelper`

`Base64UrlHelper.Encode` produces canonical unpadded Base64Url text. `Base64UrlHelper.Decode` restores the bytes and rejects malformed, padded, or non-canonical values.

```csharp
var encoded = Base64UrlHelper.Encode("payload"u8);
var decoded = Base64UrlHelper.Decode(encoded);
```

Base64Url is appropriate for opaque URL segments, continuation tokens, and string-only metadata formats. It is distinct from regular padded Base64; use `Convert.ToBase64String` when a protocol requires the standard Base64 alphabet.

### `PropertyBagScalarCodec`

`PropertyBagScalarCodec` uses a versioned Base64Url envelope to preserve supported scalar types in string-only persistence systems. Legacy unprefixed values remain strings.

### `OpaqueContinuationTokenCodec`

`OpaqueContinuationTokenCodec` serializes purpose-bound continuation-token payloads using Base64Url. It supports unsigned values and optional HMAC-SHA256 protection through `IContinuationTokenProtector`.

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

`ContentTransformEnvelopeCodec` encodes versioned `bdk_` metadata for ordered payload transforms. `PropertyBagScalarCodec` preserves supported scalar property types in a `bdk_v1_` Base64Url envelope, while legacy unprefixed values remain strings. `OpaqueContinuationTokenCodec` creates purpose-bound JSON token envelopes and can use `IContinuationTokenProtector` for HMAC protection.

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

The [recorded serializer comparison](../docs/benchmarks/serialization-2026-10-20/README.md) preserves the original five-serializer baseline. The [CBOR allocation follow-up](../docs/benchmarks/serialization-2026-10-20-allocations/README.md) contains the final comparison, allocation reductions, raw reports, and reproduction command.
