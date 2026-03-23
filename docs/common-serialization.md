# Common Serialization

`Common.Serialization` is the shared serialization layer used across the devkit. It provides a small serializer abstraction, several concrete serializers, and the devkit's default JSON conventions for results, filtering, smart enumerations, and metadata objects.

This package matters because many higher-level features depend on consistent serialization behavior:

- messaging and outbox storage
- document and file storage
- filtering payloads
- HTTP payload helpers
- result and error serialization

## Core Abstractions

### `ISerializer`

`ISerializer` is the base contract for stream-based serialization:

- `Serialize(object value, Stream output)`
- `Deserialize(Stream input, Type type)`
- `Deserialize<T>(Stream input)`

### `ITextSerializer`

`ITextSerializer` extends the serializer model for text-based formats. Use it when the transport or persistence format is naturally textual, such as JSON or CSV.

## Built-In Serializers

### `SystemTextJsonSerializer`

The default System.Text.Json-based serializer for most application-facing JSON work. Use this when you want:

- modern .NET JSON support
- integration with the devkit's JSON defaults
- good compatibility with HTTP APIs and internal app models

### `JsonNetSerializer`

A Newtonsoft.Json-based alternative for scenarios that need its contract model or compatibility surface. This is the fallback when you need JSON.NET-specific behavior rather than the standard devkit JSON path.

### `MessagePackSerializer`

A binary serializer intended for compact, fast payloads. This is also the serializer returned by `DefaultSerializer.Create`, which means the package's default serializer choice is optimized for internal transport/storage scenarios rather than human-readable output.

### `CsvSerializer`

A CSV-oriented serializer for tabular data scenarios. This is useful when the consuming feature naturally works with rows and flat data rather than object graphs.

### `CompressionSerializer`

A decorator that wraps another serializer and adds compression. Use this when payload size matters more than raw readability and you want to keep the underlying serialization format unchanged.

## Default JSON Conventions

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

## Important Converters And Resolvers

### Smart Enumerations

`EnumerationJsonConverter` handles the devkit's smart-enum pattern so those types can move through JSON payloads without custom hand-written conversion every time.

### Filtering

`FilterCriteriaJsonConverter` and `FilterSpecificationNodeConverter` support the filtering feature's JSON model. That is one reason the filtering docs and serialization docs are tightly related.

### Results

Result converters make `Result`, `Result<T>`, and paged results serialize in a stable way for APIs and internal workflows.

### `PropertyBag`

`PropertyBagJsonConverter` preserves the flexible metadata bag used across errors, saga data, and other extensibility points.

### Private Constructors And Setters

Resolvers such as `UniversalContractResolver`, `PrivateConstructorContractResolver`, and `PrivateSetterContractResolver` help the serializer work with richer domain models that do not expose public setters or public constructors.

That support is especially useful in a DDD-oriented codebase where encapsulation matters.

## Recommended Usage

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

## Choosing The Right Serializer

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

## Tradeoffs And Caveats

- `DefaultSerializer.Create` returns a `MessagePackSerializer`, which can surprise readers who assume the default is JSON.
- JSON defaults are opinionated, so if a feature needs different naming or converter ordering, create an explicit `JsonSerializerOptions` instance instead of assuming the shared defaults fit every case.
- The serializer abstraction is intentionally small. It does not try to replace ASP.NET Core formatters or model binding.
- Binary serializers are great for internal transport, but they are harder to debug than JSON.

## Related Docs

- [Filtering](./features-filtering.md)
- [Results](./features-results.md)
- [Messaging](./features-messaging.md)
- [DocumentStorage](./features-storage-documents.md)
- [DataPorter](./features-application-dataporter.md)
