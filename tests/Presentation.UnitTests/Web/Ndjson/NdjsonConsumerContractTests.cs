// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Testing.Ndjson;
using Microsoft.AspNetCore.Builder;

public class NdjsonConsumerContractTests
{
    [Fact]
    public async Task PreencodedExport_ExactBudgetAndSelectiveDigest_PartialBusinessResultHasSuccessfulTransport()
    {
        var writer = new NdjsonWriter();
        // Fixed fixture capacity includes metadata, two data frames, and completion.
        // Each actual record is encoded exactly once; admission uses its returned bytes.
        var completionReserve = Encoding.UTF8.GetByteCount("{\"kind\":\"completion\",\"status\":\"Partial\",\"count\":2}\n");
        var byteBudget = Encoding.UTF8.GetByteCount("{\"kind\":\"metadata\",\"format\":1}\n")
            + (2 * Encoding.UTF8.GetByteCount("{\"kind\":\"data\",\"value\":\"fictional\"}\n")) + completionReserve;
        long spent = 0;
        var observer = new NdjsonHttpResultTests.OutcomeObserver();
        string producerDigest = null;
        long emitted = 0;
        await using var server = await LoopbackStreamingFixture.StartAsync(app =>
            app.MapGet("/export", () => NdjsonHttpResult.FromEncodedRecords<JsonElement>(Source()).WithObserver(observer)).WithNdjson<JsonElement>());
        using var client = new HttpClient { BaseAddress = server.Address };
        var opened = await client.GetNdjsonAsync<JsonElement>("/export");
        opened.IsSuccess.ShouldBeTrue();
        await using var response = opened.Value;
        using var received = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var records = 0;
        await foreach (var frame in response.ReadRecordsAsync())
        {
            using var json = JsonDocument.Parse(frame.JsonBytes);
            var kind = json.RootElement.GetProperty("kind").GetString();
            if (kind == "data") { received.AppendData(frame.FramedBytes.Span); }

            if (kind == "completion")
            {
                json.RootElement.GetProperty("status").GetString().ShouldBe("Partial");
                json.RootElement.GetProperty("count").GetInt64().ShouldBe(2);
            }

            records++;
        }

        records.ShouldBe(4); // metadata, two data records, business completion
        emitted.ShouldBe(2);
        spent.ShouldBe(byteBudget);
        Convert.ToHexString(received.GetHashAndReset()).ShouldBe(producerDigest);
        await observer.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Succeeded);
        observer.Outcome.CleanupCompleted.ShouldBeTrue();
        observer.Outcome.FlushedRecordCount.ShouldBe(4);
        observer.Count.ShouldBe(1);

        async IAsyncEnumerable<NdjsonEncodedRecord> Source([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var metadata = await writer.EncodeRecordAsync(new { kind = "metadata", format = 1 }, cancellationToken);
            var remaining = byteBudget - metadata.FramedBytes.Length;
            spent += metadata.FramedBytes.Length;
            yield return metadata;
            for (var i = 0; i < 3; i++)
            {
                var frame = await writer.EncodeRecordAsync(new { kind = "data", value = "fictional" }, cancellationToken);
                if (frame.FramedBytes.Length > remaining - completionReserve) { frame.Dispose(); break; }

                remaining -= frame.FramedBytes.Length;
                spent += frame.FramedBytes.Length;
                digest.AppendData(frame.FramedBytes.Span);
                emitted++;
                yield return frame; // executor now owns this lease
            }

            producerDigest = Convert.ToHexString(digest.GetHashAndReset());
            var completion = await writer.EncodeRecordAsync(new { kind = "completion", status = "Partial", count = emitted }, cancellationToken);
            completion.FramedBytes.Length.ShouldBe(remaining);
            spent += completion.FramedBytes.Length;
            yield return completion;
        }
    }

    [Fact]
    public async Task OriginalFrames_CrlfWhitespaceAndEquivalentJson_HaveDistinctDigests()
    {
        using var source = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(" {\"value\":\"a\"} \r\n{\"value\":\"\\u0061\"}\n"), 1);
        var digests = new List<string>();
        var values = new List<string>();
        var reader = new NdjsonReader();
        await foreach (var frame in reader.ReadRecordsAsync(source))
        {
            digests.Add(Convert.ToHexString(SHA256.HashData(frame.FramedBytes.Span)));
            using var document = JsonDocument.Parse(frame.JsonBytes);
            values.Add(document.RootElement.GetProperty("value").GetString());
            if (digests.Count == 1)
            {
                frame.DelimiterBytes.Span.SequenceEqual("\r\n"u8).ShouldBeTrue();
                Encoding.UTF8.GetString(frame.FramedBytes.Span).ShouldBe(" {\"value\":\"a\"} \r\n");
            }
        }

        values.ShouldBe(["a", "a"]);
        digests[0].ShouldNotBe(digests[1]);
    }

    [Fact]
    public async Task FinalFlushFailure_DoesNotPublishSuccessfulBusinessOutcome()
    {
        using var output = new NdjsonTestStreams.RecordingStream { FailFlushAt = 2 };
        using var services = NdjsonHttpResultTests.Services();
        var (context, _, _) = NdjsonHttpResultTests.Context(services, output);
        var observer = new NdjsonHttpResultTests.OutcomeObserver();
        await NdjsonHttpResult.From(new[] { new { kind = "completion", status = "Complete" } }).WithObserver(observer).ExecuteAsync(context);
        observer.Outcome.SourceCompleted.ShouldBeTrue();
        observer.Outcome.Status.ShouldBe(NdjsonStreamStatus.Failed);
        observer.Outcome.ReasonCode.ShouldBe("stream_flush_failed");
        observer.Count.ShouldBe(1);
    }
}
