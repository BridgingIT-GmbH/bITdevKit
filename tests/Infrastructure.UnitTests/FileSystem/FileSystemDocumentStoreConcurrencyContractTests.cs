namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;

public sealed class FileSystemDocumentStoreConcurrencyContractTests : Shared.Storage.DocumentStoreConcurrencyContractTests, IDisposable
{
    private readonly OwnedFileSystemRoot root = new();

    protected override IDocumentStoreProvider CreateProvider() => new FileSystemDocumentStoreProvider(this.root.Options);

    public void Dispose() => this.root.Dispose();
}
