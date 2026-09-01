# Common Serialization

> Share consistent serializer abstractions and JSON conventions across the devkit.

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

### `SystemTextJsonSerializer`

The System.Text.Json-based serializer for application-facing JSON work. Use this when you want:

- modern .NET JSON support
- integration with the devkit's JSON defaults
- good compatibility with HTTP APIs and internal app models

### `JsonNetSerializer`

A Newtonsoft.Json-based alternative for scenarios that need its contract model or compatibility surface. This is the fallback when you need JSON.NET-specific behavior rather than the standard devkit JSON path.

### `MessagePackSerializer`

A binary serializer for internal payloads. `DefaultSerializer.Create` is a shared `MessagePackSerializer` instance, so the package's static default is binary rather than human-readable.

### `CsvSerializer`

A CSV-oriented serializer for tabular data scenarios. This is useful when the consuming feature naturally works with rows and flat data rather than object graphs.

### `CompressionSerializer`

A decorator that wraps another serializer and adds compression. Use this when payload size matters more than raw readability and you want to keep the underlying serialization format unchanged.

## Shared codecs

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
- size and speed matter more than readability

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
