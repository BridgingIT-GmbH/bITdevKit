// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

public class CacheCompositionTests
{
    [Fact]
    public async Task RegisteredPublicHandles_StoreLoadAndKeepTypedBinaryContractsSeparate()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("labels")
            .WithBinaryContract("raw").WithBinaryCollection("files");
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>();

        var labels = sut.GetCollection<string>("labels");
        (await labels.SetAsync(new("a"), "label")).IsSuccess.ShouldBeTrue();
        (await labels.GetAsync("A")).Value.Value.ShouldBe("label");
        var files = sut.GetBinaryCollection("files");
        using var input = new MemoryStream([1, 2, 3]);
        (await files.SetAsync(new("a"), input)).IsSuccess.ShouldBeTrue();
        var acquired = await files.OpenReadAsync("A");
        await using var handle = acquired.Value.Value;
        handle.Stream.ReadByte().ShouldBe(1);
        Should.Throw<ArgumentException>(() => sut.GetCollection<int>("labels"));
        Should.Throw<ArgumentException>(() => sut.GetBinaryCollection("labels"));
    }

    [Fact]
    public async Task DefaultJsonRawAccess_IsForwardOnlyAndDoesNotPromisePartialObjectRanges()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("labels");
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("labels");
        await sut.SetAsync(new("a"), "value");

        var normal = await sut.OpenReadAsync("a");
        await using (var handle = normal.Value.Value)
        {
            handle.Stream.CanSeek.ShouldBeFalse();
        }

        var range = await sut.OpenReadAsync("a", new(Offset: 1, Length: 2));
        range.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
        var seek = await sut.OpenReadAsync("a", new(RequireSeek: true));
        seek.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Unsupported);
    }

    [Fact]
    public async Task SeekRequiringSerializerAdapter_UsesTheAcquiredSnapshotWithoutWholePayloadBuffering()
    {
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithValueContract<string>("text", c => c.WithCodec("legacy", _ =>
                new SerializerValueCodec<string>(new SystemTextJsonSerializer(), new("legacy-json", 1))))
            .WithCollection<string>("labels", c => c.UseCodec("legacy"));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("labels");

        (await sut.SetAsync(new("a"), "value")).IsSuccess.ShouldBeTrue();
        var result = await sut.GetAsync("a");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("value");
        provider.GetRequiredService<CacheRuntimeResources>().Transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task DisabledPublicHandle_DoesNotConstructItsNativeProvider()
    {
        var services = new ServiceCollection();
        var calls = 0;
        services.AddApplicationCaching().WithProviderKind("never", new CacheProviderCapabilities(false, false, true, true, false, true), (_, _, _) =>
            {
                calls++;
                throw new InvalidOperationException("Disabled collection must not invoke this factory.");
            }).WithProvider("offline", p => p.With(x => x.Kind, "never"))
            .WithValueContract<string>("text").WithCollection<string>("labels", c => c.UseProvider("offline").Enabled(false)
                .WithCoordination(CacheCoordinationScope.SharedStore));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("labels");

        var result = await sut.GetOrLoadAsync(new("a"), (_, _) => ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("direct"))));

        result.Value.Value.ShouldBe("direct");
        (await sut.GetAsync("a")).Value.Found.ShouldBeFalse();
        calls.ShouldBe(0);
    }
}