// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.Text.Json;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;

public class NdjsonMetadataTests
{
    [Theory]
    [InlineData(NdjsonMode.Raw)]
    [InlineData(NdjsonMode.Envelope)]
    public async Task OpenApi_DeclaredMode_DescribesTextAndSeparateRecordSchema(NdjsonMode mode)
    {
        await using var server = await LoopbackStreamingFixture.StartAsync(app => app.MapGet("/stream", () => NdjsonHttpResult.From(new[] { new Sample(1, "fictional") })).WithNdjson<Sample>(o => o.Mode = mode));
        using var client = new HttpClient { BaseAddress = server.Address };
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/stream").GetProperty("get");
        operation.GetProperty("x-ndjson-mode").GetString().ShouldBe(mode == NdjsonMode.Raw ? "raw" : "envelope");
        var response = operation.GetProperty("responses").GetProperty("200");
        var content = response.GetProperty("content").GetProperty(NdjsonDefaults.CONTENT_TYPE);
        content.GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
        content.TryGetProperty("x-ndjson-record-schema", out var recordSchema).ShouldBeTrue();
        if (mode == NdjsonMode.Envelope)
        {
            recordSchema.GetProperty("oneOf").GetArrayLength().ShouldBe(4);
            response.GetProperty("headers").TryGetProperty("X-Ndjson-Envelope-Version", out _).ShouldBeTrue();
        }
    }

    private sealed record Sample(int Id, string Title);
}
