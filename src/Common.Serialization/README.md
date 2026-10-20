# Common Serialization

`BridgingIT.DevKit.Common.Serialization` provides a shared `ISerializer` contract for JSON, MessagePack, CBOR, and CSV, plus a compression decorator and persistence codecs.

## Serialize with CBOR

```csharp
using BridgingIT.DevKit.Common;

ISerializer serializer = new CborSerializer();
var bytes = serializer.SerializeToBytes(new[] { 1, 2, 3 });
var restored = serializer.Deserialize<int[]>(bytes);
```

`bytes` contains native CBOR. `SerializeToString` represents the complete binary payload as Base64. Use bytes or streams for binary transport and storage.

CBOR maps object instances directly with Microsoft's `System.Formats.Cbor` package. Public properties and fields are included by default. Records, matching constructors, inherited properties, private setters, init setters, collections, dictionaries, and native byte strings are supported.

Object contracts can use the BCL attributes from `System.Runtime.Serialization` and the DevKit constructor attribute:

- `[DataMember(Name = "wire_name")]` for explicit member names and opt-in nonpublic members.
- `[IgnoreDataMember]` to exclude a member from reads and writes.
- `[DataContract]` to include only members marked `[DataMember]`.
- `[CborConstructor]` to select a public or nonpublic constructor. Parameters match CLR member names and types independently of wire names.

CBOR recognizes member selection and the `DataMember.Name` option. `Order`, `IsRequired`, and `EmitDefaultValue` are not interpreted. JSON attributes and converters do not apply.

The byte-array adapters avoid intermediate streams. Writer reuse is bounded, and returned arrays remain independently owned. The default nesting limit is 64. Cyclic graphs, multidimensional arrays, and unsupported tags are rejected. Members declared as `object` decode to scalar values, lists, or dictionaries without restoring arbitrary CLR classes.

## Choose a serializer

Use `SystemTextJsonSerializer` for the DevKit JSON conventions, or `JsonNetSerializer` for JSON.NET-specific behavior. Use `CsvSerializer` for tabular data. `CompressionSerializer` wraps another serializer when compression is required.

`DefaultSerializer.Create` remains a shared `MessagePackSerializer`. The current benchmarks favor MessagePack for small and nested DTO throughput. Choose `CborSerializer` explicitly when the protocol requires CBOR. CBOR and MessagePack payloads require their matching decoder.

See the [serialization guide](../../docs/common-serialization.md) for supported types, mapping rules, stream behavior, and limitations. The [final benchmark comparison](../../docs/benchmarks/serialization-2026-10-20-allocations/README.md) records measured timings, allocations, pool tradeoffs, and reproduction commands.
