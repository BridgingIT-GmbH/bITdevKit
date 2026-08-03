// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage.Documents;

using System.Security.Cryptography;
using Application.Storage;

public sealed class DocumentPayloadTransformTests
{
    [Fact]
    public async Task CompressionAndEncryption_RoundTripThroughVersionedEnvelope()
    {
        var provider = new InMemoryDocumentStoreProvider();
        var keys = new DictionaryEncryptionKeyProvider("key-1", new Dictionary<string, byte[]>
        {
            ["key-1"] = RandomNumberGenerator.GetBytes(32)
        });
        var client = new DocumentStoreClient<PersonStub>(provider, transforms:
        [
            new CompressionDocumentPayloadTransform(),
            new EncryptionDocumentPayloadTransform(keys)
        ]);
        var key = new DocumentKey("people", Guid.NewGuid().ToString("N"));

        var write = await client.UpsertAsync(key, new() { Name = new string('A', 1000) });
        var stored = await provider.GetAsync(DocumentTypeIdentity.For<PersonStub>(), key, DateTimeOffset.UtcNow);
        var read = await client.GetAsync(key);

        write.IsSuccess.ShouldBeTrue();
        stored.IsSuccess.ShouldBeTrue();
        stored.Value.TransformMetadata.Get<string>("bdk_transform_envelope").ShouldStartWith(ContentTransformEnvelopeCodec.Prefix);
        stored.Value.Content.AsSpan().IndexOf(new byte[] { (byte)'A', (byte)'A', (byte)'A', (byte)'A' }).ShouldBe(-1);
        read.Value.Value.Name.ShouldBe(new string('A', 1000));
    }

    [Fact]
    public async Task Encryption_WhenHistoricalKeyIsMissing_ReturnsSerializationFailure()
    {
        var provider = new InMemoryDocumentStoreProvider();
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var writer = new DocumentStoreClient<PersonStub>(provider, transforms:
        [
            new EncryptionDocumentPayloadTransform(new DictionaryEncryptionKeyProvider("old", new Dictionary<string, byte[]> { ["old"] = keyBytes }))
        ]);
        var key = new DocumentKey("people", Guid.NewGuid().ToString("N"));
        await writer.UpsertAsync(key, new() { Name = "Ada" });
        var reader = new DocumentStoreClient<PersonStub>(provider, transforms:
        [
            new EncryptionDocumentPayloadTransform(new DictionaryEncryptionKeyProvider("new", new Dictionary<string, byte[]> { ["new"] = RandomNumberGenerator.GetBytes(32) }))
        ]);

        var result = await reader.GetAsync(key);

        result.Errors.ShouldContain(error => error is DocumentStoreSerializationError);
    }

    [Fact]
    public async Task GetAsync_WhenReverseTransformIsCanceled_PreservesCancellation()
    {
        var provider = new InMemoryDocumentStoreProvider();
        var transform = new CancelingReadTransform();
        var client = new DocumentStoreClient<PersonStub>(provider, transforms: [transform]);
        var key = new DocumentKey("people", Guid.NewGuid().ToString("N"));
        await client.UpsertAsync(key, new() { Name = "Ada" });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => client.GetAsync(key, cancellation.Token));
    }

    public sealed class PersonStub
    {
        public string Name { get; set; }
    }

    private sealed class CancelingReadTransform : IDocumentPayloadTransform
    {
        public string Identifier => "cancel-test";
        public ValueTask<byte[]> WriteAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default) => ValueTask.FromResult(content.ToArray());
        public ValueTask<byte[]> ReadAsync(byte[] content, PropertyBag metadata, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(content.ToArray());
        }
    }
}
