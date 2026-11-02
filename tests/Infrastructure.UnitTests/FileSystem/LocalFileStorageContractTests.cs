namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using System.Reflection;
using Application.IntegrationTests.Storage;
using Application.Storage;
using Infrastructure.FileSystem;

public sealed class LocalFileStorageContractTests : FileStorageTestsBase, IDisposable
{
    private readonly OwnedFileSystemRoot root = new();
    private readonly List<LocalFileStorageProvider> providers = [];

    protected override bool SupportsTemporaryOpenWrite => true;

    public static IEnumerable<object[]> ContractCases()
    {
        return typeof(FileStorageTestsBase)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.ReturnType == typeof(Task) && method.GetParameters().Length == 0)
            .Select(method => new object[] { method.Name });
    }

    [LocalFileSystemTheory]
    [MemberData(nameof(ContractCases))]
    public async Task ExistingFileStorageContract_PreservesOrdinaryFileBehavior(string scenario)
    {
        var method = typeof(FileStorageTestsBase).GetMethod(scenario);
        await (Task)method.Invoke(this, []);
    }

    protected override IFileStorageProvider CreateProvider()
    {
        var provider = new LocalFileStorageProvider("contract", Path.Combine(this.root.Path, "files"));
        this.providers.Add(provider);

        return provider;
    }

    public void Dispose()
    {
        foreach (var provider in this.providers)
        {
            provider.Dispose();
        }

        this.root.Dispose();
    }
}
