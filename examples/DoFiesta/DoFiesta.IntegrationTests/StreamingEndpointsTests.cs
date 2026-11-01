// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using System.Net;
using System.Text.Json;

[Collection("DoFiesta")]
public sealed class StreamingEndpointsTests(DoFiestaApplicationFactory factory)
{
    [Theory]
    [InlineData("/api/core/todoitems/stream", NdjsonMode.Raw)]
    [InlineData("/api/core/todoitems/stream/enveloped", NdjsonMode.Envelope)]
    public async Task TodoStreams_MultipleBatches_PreserveListAndPerItemReadPermissions(string route, NdjsonMode mode)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        var opened = await client.GetNdjsonAsync<TodoItemModel>(route, new() { ExpectedMode = mode });
        opened.IsSuccess.ShouldBeTrue(string.Join(",", opened.Errors.OfType<NdjsonClientError>().Select(e => $"{e.ReasonCode}:{e.StatusCode}")));
        await using var response = opened.Value;
        var received = new List<TodoItemModel>();
        await foreach (var model in mode == NdjsonMode.Raw ? response.ReadAsync() : response.ReadDataAsync()) { received.Add(model); }

        received.Count.ShouldBe(103);
        received.Select(r => r.Id).ToHashSet().SetEquals(factory.ReadableIds).ShouldBeTrue();
        received.Select(r => r.Title).ShouldBe(received.Select(r => r.Title).OrderBy(t => t, StringComparer.Ordinal));
        received.All(r => r.UserId == factory.UserId).ShouldBeTrue();
    }

    [Fact]
    public async Task TodoStream_NoListGrant_RejectsAdmissionWithoutSuccessHeaders()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", "no-grants-" + Guid.NewGuid());
        using var response = await client.GetAsync("/api/core/todoitems/stream");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.Contains("X-Ndjson-Mode").ShouldBeFalse();
    }

    [Fact]
    public async Task TodoStream_Unauthenticated_RequiresAuthentication()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync("/api/core/todoitems/stream");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains("X-Ndjson-Mode").ShouldBeFalse();
    }

    [Fact]
    public async Task Ticks_LiveSource_ConsumesThroughDotnetHelperAndCancels()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        var opened = await client.GetNdjsonAsync<JsonElement>("/api/core/streaming/ticks?intervalMilliseconds=20000", new() { ExpectedMode = NdjsonMode.Envelope });
        opened.IsSuccess.ShouldBeTrue(string.Join(",", opened.Errors.OfType<NdjsonClientError>().Select(e => $"{e.ReasonCode}:{e.StatusCode}")));
        await using var response = opened.Value;
        await using var items = response.ReadDataAsync().GetAsyncEnumerator();
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        items.Current.GetProperty("sequence").GetInt64().ShouldBe(1);
        await response.DisposeAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60001)]
    public async Task Ticks_InvalidInterval_ReturnsNormalHttpValidationFailure(int interval)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        using var response = await client.GetAsync($"/api/core/streaming/ticks?intervalMilliseconds={interval}");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Contains("X-Ndjson-Mode").ShouldBeFalse();
    }
}
