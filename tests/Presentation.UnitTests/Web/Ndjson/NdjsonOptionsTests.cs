// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class NdjsonOptionsTests
{
    [Fact]
    public async Task WithNdjson_ClearInheritedDuration_CapturesIndependentDescriptor()
    {
        var continueSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await LoopbackStreamingFixture.StartAsync(app =>
                app.MapGet("/stream", () => NdjsonHttpResult.From(Source()))
                    .WithNdjson<string>(o => o.ClearMaxDuration = true),
            services => services.Configure<NdjsonStreamingOptions>(o => { o.MaxDuration = TimeSpan.FromMinutes(1); o.MaxRecordBytes = 128; }));
        var endpoint = ((IEndpointRouteBuilder)server.App).DataSources.SelectMany(s => s.Endpoints).Single(e => e.Metadata.GetMetadata<NdjsonEndpointDescriptor>() is not null);
        var descriptor = endpoint.Metadata.GetMetadata<NdjsonEndpointDescriptor>();
        descriptor.Settings.MaxDuration.ShouldBeNull();
        descriptor.Settings.MaxRecordBytes.ShouldBe(128);
        using var client = new HttpClient { BaseAddress = server.Address };
        var opened = await client.GetNdjsonAsync<string>("/stream");
        opened.IsSuccess.ShouldBeTrue();
        await using var response = opened.Value;
        await using var values = response.ReadAsync().GetAsyncEnumerator();
        (await values.MoveNextAsync()).ShouldBeTrue();
        values.Current.ShouldBe("first");
        server.App.Services.GetRequiredService<IOptions<NdjsonStreamingOptions>>().Value.MaxRecordBytes = 8;
        continueSource.TrySetResult();
        (await values.MoveNextAsync()).ShouldBeTrue();
        values.Current.ShouldBe("longer than eight bytes");
        (await values.MoveNextAsync()).ShouldBeFalse();

        async IAsyncEnumerable<string> Source([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "first";
            await continueSource.Task.WaitAsync(cancellationToken);
            yield return "longer than eight bytes";
        }
    }

    [Fact]
    public void Validate_InvalidPresentationOrDeadlines_RejectsBeforeOutput()
    {
        Should.Throw<ArgumentException>(() => new NdjsonStreamingOptions { DownloadFileName = "../secret" }.Validate());
        Should.Throw<ArgumentException>(() => new NdjsonStreamingOptions { DownloadFileName = "bad\r\nname" }.Validate());
        Should.Throw<ArgumentOutOfRangeException>(() => new NdjsonStreamingOptions { WriteTimeout = TimeSpan.Zero }.Validate());
        Should.Throw<ArgumentOutOfRangeException>(() => new NdjsonStreamingOptions { CleanupTimeout = Timeout.InfiniteTimeSpan }.Validate());
        typeof(NdjsonStreamingOptions).GetProperties().Any(p => typeof(INdjsonStreamObserver).IsAssignableFrom(p.PropertyType)).ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ContradictoryRuntimeMode_RejectsBeforeStartingResponse()
    {
        using var output = new BridgingIT.DevKit.Testing.Ndjson.NdjsonTestStreams.RecordingStream();
        using var services = NdjsonHttpResultTests.Services();
        var (context, feature, _) = NdjsonHttpResultTests.Context(services, output);
        context.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(null, new Microsoft.AspNetCore.Http.EndpointMetadataCollection(new NdjsonEndpointDescriptor(typeof(int), new NdjsonStreamingOptions())), "raw"));
        var result = NdjsonHttpResult.From(new[] { 1 }, new() { Mode = NdjsonMode.Envelope });
        await Should.ThrowAsync<InvalidOperationException>(() => result.ExecuteAsync(context));
        feature.Started.Task.IsCompleted.ShouldBeFalse();
        output.Bytes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ExecuteAsync_ContradictoryRuntimeCompression_RejectsBeforeStartingResponse(bool declared, bool runtime)
    {
        using var output = new BridgingIT.DevKit.Testing.Ndjson.NdjsonTestStreams.RecordingStream();
        using var services = NdjsonHttpResultTests.Services();
        var (context, feature, _) = NdjsonHttpResultTests.Context(services, output);
        context.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(null, new Microsoft.AspNetCore.Http.EndpointMetadataCollection(
            new NdjsonEndpointDescriptor(typeof(int), new NdjsonStreamingOptions { EnableCompression = declared })), "stream"));
        var result = NdjsonHttpResult.From(new[] { 1 }, new() { EnableCompression = runtime });
        await Should.ThrowAsync<InvalidOperationException>(() => result.ExecuteAsync(context));
        feature.Started.Task.IsCompleted.ShouldBeFalse();
        output.Bytes.ShouldBeEmpty();
    }
}
