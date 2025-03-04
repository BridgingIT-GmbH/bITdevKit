// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

[IntegrationTest("Storage")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class InMemoryFileStorageProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : FileStorageTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    protected override IFileStorageProvider CreateProvider()
    {
        return new LoggingFileStorageBehavior(
            new InMemoryFileStorageProvider("InMemoryTest"),
            this.fixture.ServiceProvider.GetRequiredService<ILoggerFactory>());
        //return new InMemoryFileStorageProvider("InMemoryTest");
    }

    protected override FileStorageBuilder CreateBuilder(IServiceProvider serviceProvider = null)
    {
        throw new NotImplementedException();
    }

    protected override FileStorageFactory CreateFactory(IServiceProvider serviceProvider = null)
    {
        throw new NotImplementedException();
    }

    [Fact]
    public override async Task ExistsAsync_ExistingFile_FileFound()
    {
        await base.ExistsAsync_ExistingFile_FileFound();
    }

    [Fact]
    public override async Task ExistsAsync_NonExistingFile_FileNotFound()
    {
        await base.ExistsAsync_NonExistingFile_FileNotFound();
    }

    [Fact]
    public override async Task ReadFileAsync_ExistingFile_ReturnsStream()
    {
        await base.ReadFileAsync_ExistingFile_ReturnsStream();
    }

    [Fact]
    public override async Task ReadFileAsync_NonExistingFile_Fails()
    {
        await base.ReadFileAsync_NonExistingFile_Fails();
    }

    [Fact]
    public override async Task WriteFileAsync_ValidInput_Succeeds()
    {
        await base.WriteFileAsync_ValidInput_Succeeds();
    }

    [Fact]
    public override async Task DeleteFileAsync_ExistingFile_Succeeds()
    {
        await base.DeleteFileAsync_ExistingFile_Succeeds();
    }

    [Fact]
    public override async Task DeleteFileAsync_NonExistingFile_Fails()
    {
        await base.DeleteFileAsync_NonExistingFile_Fails();
    }

    [Fact]
    public override async Task GetChecksumAsync_ExistingFile_ReturnsChecksum()
    {
        await base.GetChecksumAsync_ExistingFile_ReturnsChecksum();
    }

    [Fact]
    public override async Task GetChecksumAsync_NonExistingFile_Fails()
    {
        await base.GetChecksumAsync_NonExistingFile_Fails();
    }

    [Fact]
    public override async Task GetFileInfoAsync_ExistingFile_ReturnsMetadata()
    {
        await base.GetFileInfoAsync_ExistingFile_ReturnsMetadata();
    }

    [Fact]
    public override async Task GetFileInfoAsync_NonExistingFile_Fails()
    {
        await base.GetFileInfoAsync_NonExistingFile_Fails();
    }

    [Fact]
    public override async Task SetFileMetadataAsync_ExistingFile_Succeeds()
    {
        await base.SetFileMetadataAsync_ExistingFile_Succeeds();
    }

    [Fact]
    public override async Task SetFileMetadataAsync_NonExistingFile_Fails()
    {
        await base.SetFileMetadataAsync_NonExistingFile_Fails();
    }

    [Fact]
    public override async Task UpdateFileMetadataAsync_ExistingFile_Succeeds()
    {
        await base.UpdateFileMetadataAsync_ExistingFile_Succeeds();
    }

    [Fact]
    public override async Task UpdateFileMetadataAsync_NonExistingFile_Fails()
    {
        await base.UpdateFileMetadataAsync_NonExistingFile_Fails();
    }

    [Fact]
    public override async Task ListFilesAsync_ValidPath_ReturnsFiles()
    {
        await base.ListFilesAsync_ValidPath_ReturnsFiles();
    }

    [Fact]
    public override async Task CopyFileAsync_ValidPaths_Succeeds()
    {
        await base.CopyFileAsync_ValidPaths_Succeeds();
    }

    [Fact]
    public override async Task RenameFileAsync_ValidPaths_Succeeds()
    {
        await base.RenameFileAsync_ValidPaths_Succeeds();
    }

    [Fact]
    public override async Task MoveFileAsync_ValidPaths_Succeeds()
    {
        await base.MoveFileAsync_ValidPaths_Succeeds();
    }

    [Fact]
    public override async Task CopyFilesAsync_ValidPairs_Succeeds()
    {
        await base.CopyFilesAsync_ValidPairs_Succeeds();
    }

    [Fact]
    public override async Task MoveFilesAsync_ValidPairs_Succeeds()
    {
        await base.MoveFilesAsync_ValidPairs_Succeeds();
    }

    [Fact]
    public override async Task DeleteFilesAsync_ExistingFiles_Succeeds()
    {
        await base.DeleteFilesAsync_ExistingFiles_Succeeds();
    }

    [Fact]
    public override async Task IsDirectoryAsync_ExistingDirectory_ReturnsTrue()
    {
        await base.IsDirectoryAsync_ExistingDirectory_ReturnsTrue();
    }

    [Fact]
    public override async Task CreateDirectoryAsync_ValidPath_Succeeds()
    {
        await base.CreateDirectoryAsync_ValidPath_Succeeds();
    }

    [Fact]
    public override async Task DeleteDirectoryAsync_ExistingDirectory_Succeeds()
    {
        await base.DeleteDirectoryAsync_ExistingDirectory_Succeeds();
    }

    [Fact]
    public override async Task ListDirectoriesAsync_ValidPath_ReturnsDirectories()
    {
        await base.ListDirectoriesAsync_ValidPath_ReturnsDirectories();
    }

    [Fact]
    public async Task CheckHealthAsync_HealthyStorage_ReturnsSuccess()
    {
        // Arrange
        var provider = this.CreateProvider();

        // Act
        var result = await provider.CheckHealthAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"In-memory storage at '{provider.LocationName}' is healthy");
    }

    [Fact]
    public override async Task WriteCompressedFileAsync_Stream_Success()
    {
        await base.WriteCompressedFileAsync_Stream_Success();
    }

    [Fact]
    public override async Task ReadCompressedFileAsync_Success()
    {
        await base.ReadCompressedFileAsync_Success();
    }

    [Fact]
    public override async Task WriteEncryptedFileAsync_Success()
    {
        await base.WriteEncryptedFileAsync_Success();
    }

    [Fact]
    public override async Task WriteBytesAsync_Success()
    {
        await base.WriteBytesAsync_Success();
    }

    [Fact]
    public override async Task WriteReadObjectAsync_Success()
    {
        await base.WriteReadObjectAsync_Success();
    }

    // TODO: compression still has issues reading the zipfiles
    //[Fact]
    //public override async Task WriteCompressedFileAsync_Directory_Success()
    //{
    //    await base.WriteCompressedFileAsync_Directory_Success();
    //}

    //[Fact]
    //public override async Task UncompressFileAsync_Success()
    //{
    //    await base.UncompressFileAsync_Success();
    //}

    //[Fact]
    //public override async Task WriteCompressedFileAsync_Directory_NonExistentDirectory_Fails()
    //{
    //    await base.WriteCompressedFileAsync_Directory_NonExistentDirectory_Fails();
    //}

    //[Fact]
    //public override async Task UncompressFileAsync_NonExistentZip_Fails()
    //{
    //    await base.UncompressFileAsync_NonExistentZip_Fails();
    //}
}