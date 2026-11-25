# Common benchmarks

## Serialization

`ValueCodecBenchmarks` measures cache-independent codec work. It compares direct JSON byte-array materialization, forward-only asynchronous encoding, complete nonseekable decoding, and the adapter for the existing seek-dependent serializer. A separate pre-cancelled case verifies rejection before any encode work.

```powershell
dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*ValueCodecBenchmarks*'
```

The logical ASCII payload parameters are 256 bytes, 4 KiB and 64 KiB. JSON framing adds its own encoded bytes. Setup verifies a full round trip. Measured decodes validate their sentinel and payload length, and failures abort the benchmark rather than count as completed work. Both encode paths include their output allocation; the forward codec avoids a second `ToArray` copy.

The serializer adapter receives a seekable MemoryStream because its existing implementation accesses `Length` and rewinds `Position`. The forward-only decoder receives a stream that rejects seeking, `Length` and `Position`. These access requirements form part of each result; they must not be hidden by an automatic full-payload buffer.

Keep BenchmarkDotNet's raw reports and runtime/hardware metadata. Use at least five measured iterations after warm-up, repeat noisy runs, and state whether the measurement is CPU-, allocation- or I/O-limited. Pre-cancelled rejection is not successful serialization throughput. Compare cache overhead separately in the [application workloads](../Application.Benchmarks/Caching/README.md).

## UTF-8 truncation

`Utf8TruncationBenchmarks` compares the original full-input byte count with the length guard in `TruncateUtf8`. Setup checks that both paths return the same text. It measures one million ASCII characters with a 32-byte budget, fitting ASCII and multibyte strings, and a short multibyte string that needs truncation.

```powershell
dotnet run -c Release --project benchmarks/Common.Benchmarks -- --filter '*Utf8TruncationBenchmarks*'
```
