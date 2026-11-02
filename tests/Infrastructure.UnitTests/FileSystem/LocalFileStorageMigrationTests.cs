namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Application.Storage;
using Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;

public sealed class LocalFileStorageMigrationTests
{
    [LocalFileSystemFact]
    public async Task RegisteredProvider_UsesSharedMetricsAndPreservesReaderDuringStagedPublication()
    {
        using var root = new OwnedFileSystemRoot();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFileStorage(factory => factory.RegisterProvider("reports", builder => builder.UseLocal("reports", Path.Combine(root.Path, "files"))));
        await using var container = services.BuildServiceProvider();
        var provider = container.GetRequiredService<IFileStorageProviderFactory>().CreateProvider("reports");
        var readsBefore = Interlocked.Read(ref FileSystemMetrics.FileReadOpens);
        var writesBefore = Interlocked.Read(ref FileSystemMetrics.FileWriteOpens);

        (await provider.WriteFileAsync("daily.bin", new MemoryStream([1, 2, 3]))).IsSuccess.ShouldBeTrue();
        var opened = await provider.ReadFileAsync("daily.bin");
        opened.IsSuccess.ShouldBeTrue();
        await using var oldReader = opened.Value;
        var writable = await provider.OpenWriteFileAsync("daily.bin", useTemporaryWrite: true);
        writable.IsSuccess.ShouldBeTrue();

        await using (var staged = writable.Value)
        {
            await staged.WriteAsync(new byte[] { 4, 5, 6 });
        }

        var original = new byte[3];
        await oldReader.ReadExactlyAsync(original);
        original.ShouldBe(new byte[] { 1, 2, 3 });
        File.ReadAllBytes(Path.Combine(root.Path, "files", "daily.bin")).ShouldBe(new byte[] { 4, 5, 6 });
        Directory.EnumerateFiles(Path.Combine(root.Path, "files"), "*.tmp").ShouldBeEmpty();
        File.Exists(Path.Combine(root.Path, "files", "store.json")).ShouldBeFalse();
        Interlocked.Read(ref FileSystemMetrics.FileReadOpens).ShouldBeGreaterThan(readsBefore);
        Interlocked.Read(ref FileSystemMetrics.FileWriteOpens).ShouldBeGreaterThan(writesBefore);
    }

    [LocalFileSystemFact]
    public async Task FileWriteFailure_ExposesSharedErrorsAlongsideExistingFileErrors()
    {
        using var root = new OwnedFileSystemRoot();
        var filesRoot = Path.Combine(root.Path, "files");
        Directory.CreateDirectory(Path.Combine(filesRoot, "directory"));
        using var provider = new LocalFileStorageProvider("errors", filesRoot);
        using var source = new MemoryStream([1]);

        var result = await provider.WriteFileAsync("directory", source);

        result.IsFailure.ShouldBeTrue();
        result.Errors.Any(error => error is FileSystemError or FileSystemPermissionError).ShouldBeTrue();
        result.Errors.Any(error => error is FileSystemStoragePermissionError or FileSystemStorageUnavailableError).ShouldBeTrue();
    }

    [LocalFileSystemFact]
    public async Task MonitoringRegistration_ResolvesMovedHandlerWithoutAnApplicationInfrastructureReference()
    {
        using var root = new OwnedFileSystemRoot();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFileMonitoring(builder => builder.UseLocal("inbound", root.Path, options => options.UseOnDemandOnly = true));
        await using var container = services.BuildServiceProvider();
        var handler = container.GetServices<ILocationHandler>().Single();
        handler.ShouldBeOfType<LocalLocationHandler>();
        await handler.StopAsync();
        typeof(IFileStorageProvider).Assembly.GetReferencedAssemblies()
            .ShouldNotContain(reference => reference.Name.StartsWith("BridgingIT.DevKit.Infrastructure.", StringComparison.Ordinal));
    }

    [LocalFileSystemFact]
    public void SharedErrorMapping_ClassifiesCapacityAndPermissionsAndPreservesCancellation()
    {
        var capacityCode = OperatingSystem.IsWindows() ? 112 : 28;
        FileSystemResults.FromException(new FileSystemNativeIOException("write", capacityCode)).ShouldBeOfType<FileSystemStorageCapacityError>();
        FileSystemResults.FromException(new UnauthorizedAccessException()).ShouldBeOfType<FileSystemStoragePermissionError>();
        Assert.Throws<OperationCanceledException>(() => FileSystemResults.FromException(new OperationCanceledException()));
    }
}
