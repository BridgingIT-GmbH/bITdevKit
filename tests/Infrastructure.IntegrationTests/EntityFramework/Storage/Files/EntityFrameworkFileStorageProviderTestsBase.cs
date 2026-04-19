// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using System.Text;
using Application.IntegrationTests.Storage;
using Application.Storage;
using Common;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public abstract class EntityFrameworkFileStorageProviderTestsBase : FileStorageTestsBase
{
    private readonly string locationName = $"ef-files-{Guid.NewGuid():N}";
    private IFileStorageProvider sut;

    protected abstract EntityFrameworkFileStorageOptions DefaultOptions { get; }

    protected abstract ServiceProvider ServiceProvider { get; }

    protected override bool SupportsTemporaryOpenWrite => true;

    protected abstract IFileStorageProvider CreateInMemoryProvider(string locationName);

    protected abstract EntityFrameworkFileStorageProvider<StubDbContext> CreateProvider(
        string locationName,
        EntityFrameworkFileStorageOptions options = null);

    [Fact]
    public override async Task ExistsAsync_ExistingFile_FileFound()
        => await base.ExistsAsync_ExistingFile_FileFound();

    [Fact]
    public override async Task ExistsAsync_NonExistingFile_FileNotFound()
        => await base.ExistsAsync_NonExistingFile_FileNotFound();

    [Fact]
    public override async Task ReadFileAsync_ExistingFile_ReturnsStream()
        => await base.ReadFileAsync_ExistingFile_ReturnsStream();

    [Fact]
    public override async Task ReadFileAsync_NonExistingFile_Fails()
        => await base.ReadFileAsync_NonExistingFile_Fails();

    [Fact]
    public override async Task WriteFileAsync_ValidInput_Succeeds()
        => await base.WriteFileAsync_ValidInput_Succeeds();

    [Fact]
    public override async Task OpenWriteFileAsync_DirectWrite_Succeeds()
        => await base.OpenWriteFileAsync_DirectWrite_Succeeds();

    [Fact]
    public override async Task OpenWriteFileAsync_CreatesParentDirectories()
        => await base.OpenWriteFileAsync_CreatesParentDirectories();

    [Fact]
    public override async Task OpenWriteFileAsync_Cancelled_Fails()
        => await base.OpenWriteFileAsync_Cancelled_Fails();

    [Fact]
    public override async Task OpenWriteFileAsync_ReportsProgress()
        => await base.OpenWriteFileAsync_ReportsProgress();

    [Fact]
    public override async Task OpenWriteFileAsync_TemporaryWrite_BehaviorMatchesSupport()
        => await base.OpenWriteFileAsync_TemporaryWrite_BehaviorMatchesSupport();

    [Fact]
    public override async Task DeleteFileAsync_ExistingFile_Succeeds()
        => await base.DeleteFileAsync_ExistingFile_Succeeds();

    [Fact]
    public override async Task DeleteFileAsync_NonExistingFile_Fails()
        => await base.DeleteFileAsync_NonExistingFile_Fails();

    [Fact]
    public override async Task GetChecksumAsync_ExistingFile_ReturnsChecksum()
        => await base.GetChecksumAsync_ExistingFile_ReturnsChecksum();

    [Fact]
    public override async Task GetChecksumAsync_NonExistingFile_Fails()
        => await base.GetChecksumAsync_NonExistingFile_Fails();

    [Fact]
    public override async Task GetFileInfoAsync_ExistingFile_ReturnsMetadata()
        => await base.GetFileInfoAsync_ExistingFile_ReturnsMetadata();

    [Fact]
    public override async Task GetFileInfoAsync_NonExistingFile_Fails()
        => await base.GetFileInfoAsync_NonExistingFile_Fails();

    [Fact]
    public override async Task ListFilesAsync_ValidPath_ReturnsFiles()
        => await base.ListFilesAsync_ValidPath_ReturnsFiles();

    [Fact]
    public override async Task CopyFileAsync_ValidPaths_Succeeds()
        => await base.CopyFileAsync_ValidPaths_Succeeds();

    [Fact]
    public override async Task RenameFileAsync_ValidPaths_Succeeds()
        => await base.RenameFileAsync_ValidPaths_Succeeds();

    [Fact]
    public override async Task MoveFileAsync_ValidPaths_Succeeds()
        => await base.MoveFileAsync_ValidPaths_Succeeds();

    [Fact]
    public override async Task IsDirectoryAsync_ExistingDirectory_ReturnsTrue()
        => await base.IsDirectoryAsync_ExistingDirectory_ReturnsTrue();

    [Fact]
    public override async Task CreateDirectoryAsync_ValidPath_Succeeds()
        => await base.CreateDirectoryAsync_ValidPath_Succeeds();

    [Fact]
    public override async Task DeleteDirectoryAsync_ExistingDirectory_Succeeds()
        => await base.DeleteDirectoryAsync_ExistingDirectory_Succeeds();

    [Fact]
    public override async Task ListDirectoriesAsync_ValidPath_ReturnsDirectories()
        => await base.ListDirectoriesAsync_ValidPath_ReturnsDirectories();

    [Fact]
    public override async Task DeepCopyAsync_SingleFile_Success()
        => await base.DeepCopyAsync_SingleFile_Success();

    [Fact]
    public virtual async Task FileExistsAsync_OnEmptyStore_DoesNotCreateRootDirectoryRow()
    {
        var provider = this.CreateIsolatedProvider("empty-read-store");

        var existsResult = await provider.FileExistsAsync("missing/file.txt", null, CancellationToken.None);
        var directoryCount = await this.ExecuteDbContextAsync(dbContext =>
            dbContext.StorageDirectories
                .AsNoTracking()
                .CountAsync(d => d.LocationName == provider.LocationName));

        existsResult.ShouldBeFailure();
        existsResult.ShouldContainError<NotFoundError>("File not found");
        directoryCount.ShouldBe(0);
    }

    [Fact]
    public virtual async Task EmptyExplicitDirectory_QueryApis_ReturnExpectedDirectoryState()
    {
        var provider = this.CreateIsolatedProvider("empty-dir");
        const string directoryPath = "queries/empty-dir";

        var createResult = await provider.CreateDirectoryAsync(directoryPath, CancellationToken.None);
        var existsResult = await provider.DirectoryExistsAsync(directoryPath, CancellationToken.None);
        var metadataResult = await provider.GetFileMetadataAsync(directoryPath, CancellationToken.None);
        var fileListResult = await provider.ListFilesAsync(directoryPath, "*", true, null, CancellationToken.None);
        var directoryListResult = await provider.ListDirectoriesAsync("queries", "*", false, CancellationToken.None);

        createResult.ShouldBeSuccess();
        existsResult.ShouldBeSuccess();
        metadataResult.ShouldBeSuccess();
        metadataResult.Value.Path.ShouldBe(directoryPath);
        metadataResult.Value.Length.ShouldBe(0);
        fileListResult.ShouldBeSuccess();
        fileListResult.Value.Files.ShouldBeEmpty();
        directoryListResult.ShouldBeSuccess();
        directoryListResult.Value.ShouldContain(directoryPath);
    }

    [Fact]
    public virtual async Task ListFilesAsync_OrdersPagesAndMatchesFinalNameSegment()
    {
        var provider = this.CreateIsolatedProvider("ordered-files");
        await provider.WriteFileAsync("docs/zeta/final.txt", new MemoryStream(Encoding.UTF8.GetBytes("zeta")), null, CancellationToken.None);
        await provider.WriteFileAsync("docs/alpha/final.txt", new MemoryStream(Encoding.UTF8.GetBytes("alpha")), null, CancellationToken.None);
        await provider.WriteFileAsync("docs/root-final.txt", new MemoryStream(Encoding.UTF8.GetBytes("root")), null, CancellationToken.None);
        await provider.WriteFileAsync("docs/archive.txt/child.bin", new MemoryStream([0x01, 0x02, 0x03]), null, CancellationToken.None);

        var firstPage = await provider.ListFilesAsync("docs", "*.txt", true, null, CancellationToken.None);
        var secondPage = await provider.ListFilesAsync("docs", "*.txt", true, firstPage.Value.NextContinuationToken, CancellationToken.None);

        firstPage.ShouldBeSuccess();
        firstPage.Value.Files.ShouldBe(["docs/alpha/final.txt", "docs/root-final.txt"]);
        firstPage.Value.NextContinuationToken.ShouldNotBeNullOrEmpty();

        secondPage.ShouldBeSuccess();
        secondPage.Value.Files.ShouldBe(["docs/zeta/final.txt"]);
        secondPage.Value.NextContinuationToken.ShouldBeNull();
    }

    [Fact]
    public virtual async Task ListFilesAsync_ManyFilesAcrossPages_ContinuationTokensAdvanceWithoutDuplicates()
    {
        var provider = this.CreateIsolatedProvider("paged");
        var expectedFiles = new List<string>
        {
            "paged/alpha/file-01.txt",
            "paged/alpha/file-02.txt",
            "paged/alpha/file-03.txt",
            "paged/beta/file-04.txt",
            "paged/beta/file-05.txt",
            "paged/gamma/file-06.txt",
            "paged/root-07.txt"
        };

        foreach (var path in expectedFiles)
        {
            await provider.WriteFileAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(path)), null, CancellationToken.None);
        }

        await provider.WriteFileAsync("paged/alpha/ignore.bin", new MemoryStream([0x01, 0x02, 0x03]), null, CancellationToken.None);
        await provider.WriteFileAsync("outside/root-08.txt", new MemoryStream(Encoding.UTF8.GetBytes("outside")), null, CancellationToken.None);

        var collectedFiles = new List<string>();
        var continuationTokens = new List<string>();
        string continuationToken = null;

        do
        {
            var page = await provider.ListFilesAsync("paged", "*.txt", true, continuationToken, CancellationToken.None);

            page.ShouldBeSuccess();
            page.Value.Files.Count().ShouldBeLessThanOrEqualTo(2);

            var pageFiles = page.Value.Files.ToList();
            pageFiles.ShouldAllBe(path => path.StartsWith("paged/", StringComparison.Ordinal));
            pageFiles.ShouldAllBe(path => path.EndsWith(".txt", StringComparison.Ordinal));

            collectedFiles.AddRange(pageFiles);

            if (continuationToken is not null)
            {
                page.Value.NextContinuationToken.ShouldNotBe(continuationToken);
            }

            continuationToken = page.Value.NextContinuationToken;
            if (continuationToken is not null)
            {
                continuationTokens.Add(continuationToken);
            }
        }
        while (continuationToken is not null);

        collectedFiles.ShouldBe(expectedFiles);
        collectedFiles.Distinct().Count().ShouldBe(expectedFiles.Count);
        continuationTokens.ShouldNotBeEmpty();
        continuationTokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(continuationTokens.Count);
    }

    [Fact]
    public virtual async Task ListDirectoriesAsync_OrdersResultsAndMatchesFinalNameSegment()
    {
        var provider = this.CreateIsolatedProvider("ordered-directories");
        await provider.CreateDirectoryAsync("dirs/gamma.txt", CancellationToken.None);
        await provider.CreateDirectoryAsync("dirs/alpha", CancellationToken.None);
        await provider.CreateDirectoryAsync("dirs/zeta", CancellationToken.None);
        await provider.CreateDirectoryAsync("dirs/archive.txt/child", CancellationToken.None);

        var orderedResult = await provider.ListDirectoriesAsync("dirs", "*", false, CancellationToken.None);
        var wildcardResult = await provider.ListDirectoriesAsync("dirs", "*.txt", true, CancellationToken.None);

        orderedResult.ShouldBeSuccess();
        orderedResult.Value.ShouldBe(["dirs/alpha", "dirs/archive.txt", "dirs/gamma.txt", "dirs/zeta"]);

        wildcardResult.ShouldBeSuccess();
        wildcardResult.Value.ShouldBe(["dirs/archive.txt", "dirs/gamma.txt"]);
    }

    [Fact]
    public virtual async Task ListFilesAsync_InvalidContinuationToken_Fails()
    {
        var provider = this.CreateIsolatedProvider("invalid-token");
        await provider.WriteFileAsync("paged/one.txt", new MemoryStream(Encoding.UTF8.GetBytes("one")), null, CancellationToken.None);

        var result = await provider.ListFilesAsync("paged", "*.txt", true, "not-a-valid-token", CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ValidationError>();
        result.Errors.ShouldContain(error => error.Message.Contains("Invalid continuation token", StringComparison.Ordinal));
    }

    [Fact]
    public virtual async Task ListFilesAsync_ContinuationTokenShapeMismatch_Fails()
    {
        var provider = this.CreateIsolatedProvider("shape-mismatch");
        await provider.WriteFileAsync("shape/one.txt", new MemoryStream(Encoding.UTF8.GetBytes("one")), null, CancellationToken.None);
        await provider.WriteFileAsync("shape/two.txt", new MemoryStream(Encoding.UTF8.GetBytes("two")), null, CancellationToken.None);
        await provider.WriteFileAsync("shape/three.txt", new MemoryStream(Encoding.UTF8.GetBytes("three")), null, CancellationToken.None);

        var firstPage = await provider.ListFilesAsync("shape", "*.txt", true, null, CancellationToken.None);
        var result = await provider.ListFilesAsync("shape", "*.bin", true, firstPage.Value.NextContinuationToken, CancellationToken.None);

        firstPage.ShouldBeSuccess();
        firstPage.Value.NextContinuationToken.ShouldNotBeNullOrEmpty();
        result.ShouldBeFailure();
        result.ShouldContainError<ValidationError>();
        result.Errors.ShouldContain(error => error.Message.Contains("shape", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public virtual async Task WriteFileAsync_TextAndBinaryPersistence_SplitsMetadataAndPayloadStorage()
    {
        var provider = this.CreateIsolatedProvider("split-payloads");
        var textBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("Hello from text storage"))
            .ToArray();
        byte[] binaryBytes = [0x00, 0x10, 0x20, 0xFF];

        var textWriteResult = await provider.WriteFileAsync("payloads/text.txt", new MemoryStream(textBytes), null, CancellationToken.None);
        var binaryWriteResult = await provider.WriteFileAsync("payloads/data.bin", new MemoryStream(binaryBytes), null, CancellationToken.None);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var textFile = await dbContext.StorageFiles.AsNoTracking().SingleAsync(f => f.LocationName == provider.LocationName && f.NormalizedPath == "payloads/text.txt");
        var binaryFile = await dbContext.StorageFiles.AsNoTracking().SingleAsync(f => f.LocationName == provider.LocationName && f.NormalizedPath == "payloads/data.bin");
        var textContent = await dbContext.StorageFileContents.AsNoTracking().SingleAsync(c => c.FileId == textFile.Id);
        var binaryContent = await dbContext.StorageFileContents.AsNoTracking().SingleAsync(c => c.FileId == binaryFile.Id);

        var readTextResult = await provider.ReadFileAsync("payloads/text.txt", null, CancellationToken.None);
        var readBinaryResult = await provider.ReadFileAsync("payloads/data.bin", null, CancellationToken.None);
        var textMetadataResult = await provider.GetFileMetadataAsync("payloads/text.txt", CancellationToken.None);
        var binaryMetadataResult = await provider.GetFileMetadataAsync("payloads/data.bin", CancellationToken.None);

        textWriteResult.ShouldBeSuccess();
        binaryWriteResult.ShouldBeSuccess();

        textContent.ContentText.ShouldBe("Hello from text storage");
        textContent.TextEncodingName.ShouldBe(Encoding.UTF8.WebName);
        textContent.TextHasByteOrderMark.ShouldBeTrue();
        textContent.ContentBinary.ShouldBeNull();

        binaryContent.ContentText.ShouldBeNull();
        binaryContent.TextEncodingName.ShouldBeNull();
        binaryContent.TextHasByteOrderMark.ShouldBeFalse();
        binaryContent.ContentBinary.ShouldBe(binaryBytes);

        textMetadataResult.ShouldBeSuccess();
        textMetadataResult.Value.Length.ShouldBe(textBytes.Length);
        binaryMetadataResult.ShouldBeSuccess();
        binaryMetadataResult.Value.Length.ShouldBe(binaryBytes.Length);

        (await ReadAllBytesAsync(readTextResult)).ShouldBe(textBytes);
        (await ReadAllBytesAsync(readBinaryResult)).ShouldBe(binaryBytes);
    }

    [Fact]
    public virtual async Task WriteFileAsync_RawBytesWithTextExtension_RoundTrips()
    {
        var provider = this.CreateIsolatedProvider("raw-bytes-roundtrip");
        byte[] rawBytes = [0x80, 0x81, 0xFE, 0xFF];

        var writeResult = await provider.WriteFileAsync("payloads/raw.txt", new MemoryStream(rawBytes), null, CancellationToken.None);
        var readResult = await provider.ReadBytesAsync("payloads/raw.txt", cancellationToken: CancellationToken.None);

        writeResult.ShouldBeSuccess();
        readResult.ShouldBeSuccess();
        readResult.Value.ShouldBe(rawBytes);
    }

    [Fact]
    public virtual async Task WriteFileAsync_RawBytesWithTextExtension_FallsBackToBinaryStorage()
    {
        var provider = this.CreateIsolatedProvider("raw-bytes-binary");
        byte[] rawBytes = [0x80, 0x81, 0xFE, 0xFF];

        var writeResult = await provider.WriteFileAsync("payloads/raw.txt", new MemoryStream(rawBytes), null, CancellationToken.None);
        var readResult = await provider.ReadBytesAsync("payloads/raw.txt", cancellationToken: CancellationToken.None);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var file = await dbContext.StorageFiles.AsNoTracking().SingleAsync(f => f.LocationName == provider.LocationName && f.NormalizedPath == "payloads/raw.txt");
        var content = await dbContext.StorageFileContents.AsNoTracking().SingleAsync(c => c.FileId == file.Id);

        writeResult.ShouldBeSuccess();
        readResult.ShouldBeSuccess();
        readResult.Value.ShouldBe(rawBytes);
        content.ContentText.ShouldBeNull();
        content.TextEncodingName.ShouldBeNull();
        content.TextHasByteOrderMark.ShouldBeFalse();
        content.ContentBinary.ShouldBe(rawBytes);
    }

    [Fact]
    public virtual async Task RootDirectoryRow_IsPersistedButNotExposedInListings()
    {
        var provider = this.CreateIsolatedProvider("root-row");
        await provider.CreateDirectoryAsync("root-visible", CancellationToken.None);
        await provider.WriteFileAsync("root-file.txt", new MemoryStream(Encoding.UTF8.GetBytes("root")), null, CancellationToken.None);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var rootRow = await dbContext.StorageDirectories
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.LocationName == provider.LocationName && d.NormalizedPath == string.Empty);
        var directoryResult = await provider.ListDirectoriesAsync(string.Empty, "*", true, CancellationToken.None);
        var fileResult = await provider.ListFilesAsync(string.Empty, "*", true, null, CancellationToken.None);

        rootRow.ShouldNotBeNull();
        rootRow.Name.ShouldBeEmpty();
        rootRow.IsExplicit.ShouldBeFalse();

        directoryResult.ShouldBeSuccess();
        directoryResult.Value.ShouldContain("root-visible");
        directoryResult.Value.ShouldNotContain(string.Empty);

        fileResult.ShouldBeSuccess();
        fileResult.Value.Files.ShouldContain("root-file.txt");
        fileResult.Value.Files.ShouldNotContain(string.Empty);
    }

    [Fact]
    public virtual async Task WriteFileAsync_NestedPath_MaterializesImplicitParentDirectories()
    {
        var provider = this.CreateIsolatedProvider("materialize-parents");

        var result = await provider.WriteFileAsync("parents/child/file.txt", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);
        var directories = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDirectories
                .AsNoTracking()
                .Where(d => d.LocationName == provider.LocationName && d.NormalizedPath != string.Empty)
                .OrderBy(d => d.NormalizedPath)
                .ToListAsync());

        result.ShouldBeSuccess();
        directories.Select(d => d.NormalizedPath).ShouldBe(["parents", "parents/child"]);
        directories.All(d => d.IsExplicit is false).ShouldBeTrue();
    }

    [Fact]
    public virtual async Task CreateDirectoryAsync_NestedPath_CreatesImplicitAncestorsAndExplicitTarget()
    {
        var provider = this.CreateIsolatedProvider("create-nested-directory");

        var result = await provider.CreateDirectoryAsync("explicit/branch/leaf", CancellationToken.None);
        var directories = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDirectories
                .AsNoTracking()
                .Where(d => d.LocationName == provider.LocationName && d.NormalizedPath != string.Empty)
                .OrderBy(d => d.NormalizedPath)
                .Select(d => new { d.NormalizedPath, d.IsExplicit })
                .ToListAsync());

        result.ShouldBeSuccess();
        directories.ShouldBe(
        [
            new { NormalizedPath = "explicit", IsExplicit = false },
            new { NormalizedPath = "explicit/branch", IsExplicit = false },
            new { NormalizedPath = "explicit/branch/leaf", IsExplicit = true }
        ]);
    }

    [Fact]
    public virtual async Task WriteFileAsync_MixedCasePath_PreservesStoredCasingAndSupportsCaseInsensitiveAccess()
    {
        var provider = this.CreateIsolatedProvider("mixed-case-file");
        const string storedPath = "Docs/MixedCase/ReadMe.TXT";
        byte[] payload = "payload"u8.ToArray();

        var writeResult = await provider.WriteFileAsync(storedPath, new MemoryStream(payload), null, CancellationToken.None);
        var readResult = await provider.ReadBytesAsync("docs/mixedcase/readme.txt", cancellationToken: CancellationToken.None);
        var metadataResult = await provider.GetFileMetadataAsync("DOCS/MIXEDCASE/README.TXT", CancellationToken.None);
        var directoryListResult = await provider.ListDirectoriesAsync("docs", "*", true, CancellationToken.None);
        var fileListResult = await provider.ListFilesAsync("docs", "*", true, null, CancellationToken.None);

        var storedState = await this.ExecuteDbContextAsync(async dbContext =>
            new
            {
                File = await dbContext.StorageFiles
                    .AsNoTracking()
                    .SingleAsync(f => f.LocationName == provider.LocationName),
                Directories = await dbContext.StorageDirectories
                    .AsNoTracking()
                    .Where(d => d.LocationName == provider.LocationName && d.NormalizedPath != string.Empty)
                    .OrderBy(d => d.NormalizedPath)
                    .Select(d => new { d.NormalizedPath, d.Name })
                    .ToListAsync()
            });

        writeResult.ShouldBeSuccess();
        readResult.ShouldBeSuccess();
        readResult.Value.ShouldBe(payload);
        metadataResult.ShouldBeSuccess();
        metadataResult.Value.Path.ShouldBe(storedPath);
        directoryListResult.ShouldBeSuccess();
        directoryListResult.Value.ShouldBe(["Docs/MixedCase"]);
        fileListResult.ShouldBeSuccess();
        fileListResult.Value.Files.ShouldBe([storedPath]);
        storedState.File.NormalizedPath.ShouldBe(storedPath);
        storedState.File.ParentPath.ShouldBe("Docs/MixedCase");
        storedState.File.Name.ShouldBe("ReadMe.TXT");
        storedState.Directories.ShouldBe(
        [
            new { NormalizedPath = "Docs", Name = "Docs" },
            new { NormalizedPath = "Docs/MixedCase", Name = "MixedCase" }
        ]);
    }

    [Fact]
    public virtual async Task WriteFileAsync_ExistingDirectoriesRetainStoredCasing_WhenAddressedWithDifferentCasing()
    {
        var provider = this.CreateIsolatedProvider("mixed-case-ancestors");
        await provider.CreateDirectoryAsync("Root/Inner", CancellationToken.None);

        var writeResult = await provider.WriteFileAsync("root/inner/File.txt", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);
        var metadataResult = await provider.GetFileMetadataAsync("ROOT/INNER/FILE.TXT", CancellationToken.None);

        var storedState = await this.ExecuteDbContextAsync(async dbContext =>
            new
            {
                File = await dbContext.StorageFiles
                    .AsNoTracking()
                    .SingleAsync(f => f.LocationName == provider.LocationName),
                Directories = await dbContext.StorageDirectories
                    .AsNoTracking()
                    .Where(d => d.LocationName == provider.LocationName && d.NormalizedPath != string.Empty)
                    .OrderBy(d => d.NormalizedPath)
                    .Select(d => d.NormalizedPath)
                    .ToListAsync()
            });

        writeResult.ShouldBeSuccess();
        metadataResult.ShouldBeSuccess();
        metadataResult.Value.Path.ShouldBe("Root/Inner/File.txt");
        storedState.File.NormalizedPath.ShouldBe("Root/Inner/File.txt");
        storedState.File.ParentPath.ShouldBe("Root/Inner");
        storedState.Directories.ShouldBe(["Root", "Root/Inner"]);
    }

    [Fact]
    public virtual async Task DeleteFileAsync_DeletingOnlyFile_PrunesImplicitParents()
    {
        var provider = this.CreateIsolatedProvider("prune-implicit-parents");
        await provider.WriteFileAsync("implicit/only/file.txt", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);

        var deleteResult = await provider.DeleteFileAsync("implicit/only/file.txt", null, CancellationToken.None);
        var implicitResult = await provider.DirectoryExistsAsync("implicit", CancellationToken.None);
        var onlyResult = await provider.DirectoryExistsAsync("implicit/only", CancellationToken.None);

        deleteResult.ShouldBeSuccess();
        implicitResult.ShouldBeFailure();
        implicitResult.ShouldContainError<NotFoundError>("Directory not found");
        onlyResult.ShouldBeFailure();
        onlyResult.ShouldContainError<NotFoundError>("Directory not found");
    }

    [Fact]
    public virtual async Task DeleteFileAsync_ExplicitDirectoriesRemainWhileImplicitParentsAreRetainedForThem()
    {
        var provider = this.CreateIsolatedProvider("retain-explicit-parents");
        await provider.CreateDirectoryAsync("explicit/only", CancellationToken.None);
        await provider.WriteFileAsync("explicit/only/file.txt", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);

        var deleteResult = await provider.DeleteFileAsync("explicit/only/file.txt", null, CancellationToken.None);
        var explicitParentResult = await provider.DirectoryExistsAsync("explicit", CancellationToken.None);
        var explicitLeafResult = await provider.DirectoryExistsAsync("explicit/only", CancellationToken.None);

        var leafDirectory = await this.ExecuteDbContextAsync(dbContext =>
            dbContext.StorageDirectories
                .AsNoTracking()
                .SingleAsync(d => d.LocationName == provider.LocationName && d.NormalizedPath == "explicit/only"));

        deleteResult.ShouldBeSuccess();
        explicitParentResult.ShouldBeSuccess();
        explicitLeafResult.ShouldBeSuccess();
        leafDirectory.IsExplicit.ShouldBeTrue();
    }

    [Fact]
    public virtual async Task DeleteDirectoryAsync_RemovingExplicitLeaf_PrunesNowEmptyImplicitParent()
    {
        var provider = this.CreateIsolatedProvider("prune-empty-parent");
        await provider.CreateDirectoryAsync("prune/leaf", CancellationToken.None);

        var deleteResult = await provider.DeleteDirectoryAsync("prune/leaf", true, CancellationToken.None);
        var parentResult = await provider.DirectoryExistsAsync("prune", CancellationToken.None);

        deleteResult.ShouldBeSuccess();
        parentResult.ShouldBeFailure();
        parentResult.ShouldContainError<NotFoundError>("Directory not found");
    }

    [Fact]
    public virtual async Task DeleteDirectoryAsync_NonRecursiveNonEmptyDirectory_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("nonrecursive-conflict");
        await provider.WriteFileAsync("nonrecursive/child/file.txt", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);

        var result = await provider.DeleteDirectoryAsync("nonrecursive", false, CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>("Directory is not empty");
    }

    [Fact]
    public virtual async Task WriteFileAsync_ExceedingMaximumBufferedContentSize_ReturnsClearFailure()
    {
        var provider = this.CreateIsolatedProvider(
            "buffer-limit",
            new EntityFrameworkFileStorageOptionsBuilder().MaximumBufferedContentSize(4).Build());

        var result = await provider.WriteFileAsync("limits/file.txt", new MemoryStream("12345"u8.ToArray()), null, CancellationToken.None);
        var existsResult = await provider.FileExistsAsync("limits/file.txt", null, CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ValidationError>();
        result.Errors.ShouldContain(error => error.Message.Contains("4 bytes", StringComparison.Ordinal));
        existsResult.ShouldBeFailure();
        existsResult.ShouldContainError<NotFoundError>("File not found");
    }

    [Fact]
    public virtual async Task WriteFileAsync_TopLevelPath_WithDefaultLeaseDuration_Succeeds()
    {
        var provider = this.CreateIsolatedProvider(
            "leased-root-write",
            new EntityFrameworkFileStorageOptionsBuilder().PageSize(2).Build());
        byte[] payload = "leased-root-write"u8.ToArray();

        var writeResult = await provider.WriteFileAsync("top-level.txt", new MemoryStream(payload), null, CancellationToken.None);

        writeResult.ShouldBeSuccess();
        await this.AssertReadFileAsync(provider, "top-level.txt", payload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public virtual async Task WriteFileAsync_Utf8Text_RoundTripsExactBytes(bool withBom)
    {
        var provider = this.CreateIsolatedProvider("utf8-roundtrip");
        var text = "Hello from EF π";
        var encoding = new UTF8Encoding(withBom);
        var payload = withBom
            ? [.. encoding.GetPreamble(), .. encoding.GetBytes(text)]
            : encoding.GetBytes(text);
        var path = withBom ? "text/with-bom.txt" : "text/without-bom.txt";

        var writeResult = await provider.WriteFileAsync(path, new MemoryStream(payload), null, CancellationToken.None);
        var readResult = await provider.ReadFileAsync(path, null, CancellationToken.None);

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var file = await dbContext.StorageFiles.AsNoTracking().SingleAsync(f => f.LocationName == provider.LocationName && f.NormalizedPath == path, CancellationToken.None);
        var content = await dbContext.StorageFileContents.AsNoTracking().SingleAsync(c => c.FileId == file.Id, CancellationToken.None);

        writeResult.ShouldBeSuccess();
        content.ContentText.ShouldBe(text);
        content.TextEncodingName.ShouldBe(Encoding.UTF8.WebName);
        content.TextHasByteOrderMark.ShouldBe(withBom);
        content.ContentBinary.ShouldBeNull();
        (await ReadAllBytesAsync(readResult)).ShouldBe(payload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public virtual async Task OpenWriteFileAsync_WithholdAndCommit_KeepsPreviousContentUntilDispose(bool useTemporaryWrite)
    {
        var provider = this.CreateIsolatedProvider("open-write-commit");
        byte[] original = "before"u8.ToArray();
        byte[] updated = new UTF8Encoding(true).GetBytes("after");
        const string path = "open-write/file.txt";

        await provider.WriteFileAsync(path, new MemoryStream(original), null, CancellationToken.None);
        var openResult = await provider.OpenWriteFileAsync(path, useTemporaryWrite, null, CancellationToken.None);
        openResult.ShouldBeSuccess();

        await using (var stream = openResult.Value)
        {
            await stream.WriteAsync(updated, CancellationToken.None);

            var beforeDisposeResult = await provider.ReadFileAsync(path, null, CancellationToken.None);
            (await ReadAllBytesAsync(beforeDisposeResult)).ShouldBe(original);
        }

        var afterDisposeResult = await provider.ReadFileAsync(path, null, CancellationToken.None);

        (await ReadAllBytesAsync(afterDisposeResult)).ShouldBe(updated);
    }

    [Fact]
    public virtual async Task GetChecksumAsync_OverwritingSameText_KeepsChecksumStable()
    {
        var provider = this.CreateIsolatedProvider("checksum-stable");
        var payload = new UTF8Encoding(true).GetBytes("checksum payload");

        await provider.WriteFileAsync("checksums/file.txt", new MemoryStream(payload), null, CancellationToken.None);
        var firstChecksum = await provider.GetChecksumAsync("checksums/file.txt", CancellationToken.None);

        await provider.WriteFileAsync("checksums/file.txt", new MemoryStream(payload), null, CancellationToken.None);
        var secondChecksum = await provider.GetChecksumAsync("checksums/file.txt", CancellationToken.None);

        firstChecksum.ShouldBeSuccess();
        secondChecksum.ShouldBeSuccess();
        firstChecksum.Value.ShouldBe(secondChecksum.Value);
        firstChecksum.Value.ShouldBe(this.ComputeSha256Hash(payload));
    }

    [Fact]
    public virtual async Task WriteFileAsync_ConcurrentCompatibleWritesUnderSameParentDirectory_SucceedsForBoth()
    {
        var locationName = this.CreateLocationName("compatible-parent");
        var provider1 = this.CreateProvider(locationName, this.DefaultOptions);
        var provider2 = this.CreateProvider(locationName, this.DefaultOptions);
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const string parentPath = "shared/new-parent";
        byte[] firstPayload = "first-payload"u8.ToArray();
        byte[] secondPayload = "second-payload"u8.ToArray();

        (await provider1.CreateDirectoryAsync(parentPath, CancellationToken.None)).ShouldBeSuccess();

        var firstWrite = Task.Run(async () =>
        {
            await startSignal.Task;
            return await provider1.WriteFileAsync(
                $"{parentPath}/alpha.txt",
                new MemoryStream(firstPayload),
                null,
                CancellationToken.None);
        });

        var secondWrite = Task.Run(async () =>
        {
            await startSignal.Task;
            return await provider2.WriteFileAsync(
                $"{parentPath}/beta.txt",
                new MemoryStream(secondPayload),
                null,
                CancellationToken.None);
        });

        startSignal.SetResult();
        var results = await Task.WhenAll(firstWrite, secondWrite);

        results.ShouldAllBe(result => result.IsSuccess);
        await this.AssertReadFileAsync(provider1, $"{parentPath}/alpha.txt", firstPayload);
        await this.AssertReadFileAsync(provider2, $"{parentPath}/beta.txt", secondPayload);

        var filePaths = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageFiles
                .AsNoTracking()
                .Where(f => f.LocationName == locationName)
                .OrderBy(f => f.NormalizedPath)
                .Select(f => f.NormalizedPath)
                .ToListAsync());

        filePaths.ShouldBe([$"{parentPath}/alpha.txt", $"{parentPath}/beta.txt"]);
    }

    [Fact]
    public virtual async Task RenameFileAsync_SameNormalizedPathWithDifferentPathSyntax_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("same-normalized-file");
        byte[] payload = "payload"u8.ToArray();
        await provider.WriteFileAsync("conflicts/source/file.txt", new MemoryStream(payload), null, CancellationToken.None);

        var result = await provider.RenameFileAsync(
            "conflicts/source/./file.txt",
            "conflicts/source/branch/../FILE.txt",
            null,
            CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>("same");
        await this.AssertReadFileAsync(provider, "conflicts/source/file.txt", payload);
    }

    [Fact]
    public virtual async Task RenameDirectoryAsync_SameNormalizedPathWithDifferentPathSyntax_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("same-normalized-directory");
        await provider.CreateDirectoryAsync("conflicts/source/child", CancellationToken.None);

        var result = await provider.RenameDirectoryAsync(
            "conflicts/source/./child",
            "conflicts/source/branch/../CHILD",
            CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>("same");
        await this.AssertDirectoryExistsAsync(provider, "conflicts/source/child");
    }

    [Fact]
    public virtual async Task WriteFileAsync_WhenDirectoryExistsAtPath_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("file-directory-conflict");
        await provider.CreateDirectoryAsync("conflicts/entry", CancellationToken.None);

        var result = await provider.WriteFileAsync("conflicts/entry", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>("directory");
    }

    [Fact]
    public virtual async Task CreateDirectoryAsync_WhenFileExistsAtPath_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("directory-file-conflict");
        await provider.WriteFileAsync("conflicts/file.txt", new MemoryStream("payload"u8.ToArray()), null, CancellationToken.None);

        var result = await provider.CreateDirectoryAsync("conflicts/file.txt", CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>("file");
    }

    [Fact]
    public virtual async Task CopyFileAsync_SamePath_ReturnsConflict()
        => await this.AssertFileMutationSamePathConflictAsync(CopyFileAsync, "same-path/copy.txt");

    [Fact]
    public virtual async Task RenameFileAsync_SamePath_ReturnsConflict()
        => await this.AssertFileMutationSamePathConflictAsync(RenameFileAsync, "same-path/rename.txt");

    [Fact]
    public virtual async Task MoveFileAsync_SamePath_ReturnsConflict()
        => await this.AssertFileMutationSamePathConflictAsync(MoveFileAsync, "same-path/move.txt");

    [Fact]
    public virtual async Task CopyFileAsync_WhenDestinationIsExistingDirectory_ReturnsConflict()
        => await this.AssertFileMutationDirectoryCollisionConflictAsync(CopyFileAsync, "directory-collision/source-copy.txt", "directory-collision/destination");

    [Fact]
    public virtual async Task RenameFileAsync_WhenDestinationIsExistingDirectory_ReturnsConflict()
        => await this.AssertFileMutationDirectoryCollisionConflictAsync(RenameFileAsync, "directory-collision/source-rename.txt", "directory-collision/destination");

    [Fact]
    public virtual async Task MoveFileAsync_WhenDestinationIsExistingDirectory_ReturnsConflict()
        => await this.AssertFileMutationDirectoryCollisionConflictAsync(MoveFileAsync, "directory-collision/source-move.txt", "directory-collision/destination");

    [Fact]
    public virtual async Task CopyFileAsync_TextPayload_ToBinaryExtension_RemainsReadable()
    {
        var provider = this.CreateIsolatedProvider("copy-text-to-binary");
        var payload = new UTF8Encoding(true).GetBytes("text payload");

        await provider.WriteFileAsync("content/source.txt", new MemoryStream(payload), null, CancellationToken.None);

        var result = await provider.CopyFileAsync("content/source.txt", "content/copied.bin", null, CancellationToken.None);

        result.ShouldBeSuccess();
        await this.AssertReadFileAsync(provider, "content/source.txt", payload);
        await this.AssertReadFileAsync(provider, "content/copied.bin", payload);
    }

    [Fact]
    public virtual async Task RenameFileAsync_BinaryPayload_ToTextExtension_RemainsReadable()
    {
        var provider = this.CreateIsolatedProvider("rename-binary-to-text");
        byte[] payload = [0x00, 0x10, 0x20, 0xFF];

        await provider.WriteFileAsync("content/source.bin", new MemoryStream(payload), null, CancellationToken.None);

        var result = await provider.RenameFileAsync("content/source.bin", "content/renamed.txt", null, CancellationToken.None);

        result.ShouldBeSuccess();
        await this.AssertFileDoesNotExistAsync(provider, "content/source.bin");
        await this.AssertReadFileAsync(provider, "content/renamed.txt", payload);
    }

    [Fact]
    public virtual async Task MoveFileAsync_TextPayload_ToBinaryExtension_RemainsReadable()
    {
        var provider = this.CreateIsolatedProvider("move-text-to-binary");
        var payload = new UTF8Encoding(true).GetBytes("moved text payload");

        await provider.WriteFileAsync("content/source.txt", new MemoryStream(payload), null, CancellationToken.None);

        var result = await provider.MoveFileAsync("content/source.txt", "content/moved.bin", null, CancellationToken.None);

        result.ShouldBeSuccess();
        await this.AssertFileDoesNotExistAsync(provider, "content/source.txt");
        await this.AssertReadFileAsync(provider, "content/moved.bin", payload);
    }

    [Fact]
    public virtual async Task MoveFileAsync_TouchesSourceAndDestinationParentLastModified()
    {
        var provider = this.CreateIsolatedProvider("move-last-modified");
        byte[] payload = "payload"u8.ToArray();
        await provider.CreateDirectoryAsync("timeline/source", CancellationToken.None);
        await provider.CreateDirectoryAsync("timeline/destination", CancellationToken.None);
        await provider.WriteFileAsync("timeline/source/file.txt", new MemoryStream(payload), null, CancellationToken.None);

        var sourceParentBefore = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/source");
        var destinationParentBefore = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/destination");

        await Task.Delay(100);

        var result = await provider.MoveFileAsync("timeline/source/file.txt", "timeline/destination/file.txt", null, CancellationToken.None);

        result.ShouldBeSuccess();
        await this.AssertReadFileAsync(provider, "timeline/destination/file.txt", payload);
        await this.AssertDirectoryExistsAsync(provider, "timeline/source");

        var sourceParentAfter = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/source");
        var destinationParentAfter = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/destination");
        sourceParentAfter.ShouldBeGreaterThan(sourceParentBefore);
        destinationParentAfter.ShouldBeGreaterThan(destinationParentBefore);
    }

    [Fact]
    public virtual async Task MoveFileAsync_CreatingAndPruningIntermediateDirectories_TouchesSurvivingParents()
    {
        var provider = this.CreateIsolatedProvider("move-intermediate-last-modified");
        byte[] payload = "payload"u8.ToArray();
        await provider.CreateDirectoryAsync("timeline/source", CancellationToken.None);
        await provider.CreateDirectoryAsync("timeline/destination", CancellationToken.None);
        await provider.WriteFileAsync("timeline/source/branch/file.txt", new MemoryStream(payload), null, CancellationToken.None);

        var sourceParentBefore = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/source");
        var destinationParentBefore = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/destination");

        await Task.Delay(100);

        var result = await provider.MoveFileAsync("timeline/source/branch/file.txt", "timeline/destination/newbranch/file.txt", null, CancellationToken.None);

        result.ShouldBeSuccess();
        await this.AssertDirectoryExistsAsync(provider, "timeline/source");
        await this.AssertDirectoryDoesNotExistAsync(provider, "timeline/source/branch");
        await this.AssertDirectoryExistsAsync(provider, "timeline/destination/newbranch");
        await this.AssertReadFileAsync(provider, "timeline/destination/newbranch/file.txt", payload);

        var sourceParentAfter = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/source");
        var destinationParentAfter = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "timeline/destination");
        sourceParentAfter.ShouldBeGreaterThan(sourceParentBefore);
        destinationParentAfter.ShouldBeGreaterThan(destinationParentBefore);
    }

    [Fact]
    public virtual async Task RenameDirectoryAsync_SubtreeRename_MovesEntireSubtreeAndTouchesParent()
    {
        var provider = this.CreateIsolatedProvider("rename-subtree");
        await provider.CreateDirectoryAsync("projects/source/docs", CancellationToken.None);
        await provider.CreateDirectoryAsync("projects/source/assets/icons", CancellationToken.None);
        await provider.WriteTextFileAsync("projects/source/readme.txt", "root");
        await provider.WriteTextFileAsync("projects/source/docs/spec.txt", "spec");
        await provider.WriteTextFileAsync("projects/source/assets/icons/logo.txt", "logo");

        var parentBefore = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "projects");

        await Task.Delay(100);

        var result = await provider.RenameDirectoryAsync("projects/source", "projects/archive", CancellationToken.None);

        result.ShouldBeSuccess();
        await this.AssertDirectoryDoesNotExistAsync(provider, "projects/source");
        await this.AssertDirectoryExistsAsync(provider, "projects/archive");
        await this.AssertDirectoryExistsAsync(provider, "projects/archive/docs");
        await this.AssertDirectoryExistsAsync(provider, "projects/archive/assets/icons");
        await this.AssertFileExistsAsync(provider, "projects/archive/readme.txt");
        await this.AssertFileExistsAsync(provider, "projects/archive/docs/spec.txt");
        await this.AssertFileExistsAsync(provider, "projects/archive/assets/icons/logo.txt");
        await this.AssertFileDoesNotExistAsync(provider, "projects/source/readme.txt");
        await this.AssertFileDoesNotExistAsync(provider, "projects/source/docs/spec.txt");
        await this.AssertFileDoesNotExistAsync(provider, "projects/source/assets/icons/logo.txt");

        var parentAfter = await this.GetDirectoryLastModifiedAsync(provider.LocationName, "projects");
        parentAfter.ShouldBeGreaterThan(parentBefore);

        var treeText = await RenderTreeAsync(provider, "projects");
        treeText.ShouldContain("archive");
        treeText.ShouldNotContain("source");

        var directories = await this.GetDirectoryPathsAsync(provider.LocationName);
        var files = await this.GetFilePathsAsync(provider.LocationName);
        directories.ShouldContain("projects/archive");
        directories.ShouldContain("projects/archive/docs");
        directories.ShouldContain("projects/archive/assets");
        directories.ShouldContain("projects/archive/assets/icons");
        directories.ShouldNotContain("projects/source");
        directories.ShouldNotContain("projects/source/docs");
        files.ShouldContain("projects/archive/readme.txt");
        files.ShouldContain("projects/archive/docs/spec.txt");
        files.ShouldContain("projects/archive/assets/icons/logo.txt");
        files.ShouldNotContain("projects/source/readme.txt");
    }

    [Fact]
    public virtual async Task RenameDirectoryAsync_SamePath_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("rename-same-path");
        await provider.CreateDirectoryAsync("conflicts/source/child", CancellationToken.None);
        await provider.WriteTextFileAsync("conflicts/source/file.txt", "payload");

        var result = await provider.RenameDirectoryAsync("conflicts/source", "conflicts/source", CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>();
        await this.AssertDirectoryExistsAsync(provider, "conflicts/source");
        await this.AssertDirectoryExistsAsync(provider, "conflicts/source/child");
        await this.AssertFileExistsAsync(provider, "conflicts/source/file.txt");
    }

    [Fact]
    public virtual async Task RenameDirectoryAsync_DestinationInsideSource_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("rename-destination-inside-source");
        await provider.CreateDirectoryAsync("conflicts/source/child", CancellationToken.None);
        await provider.WriteTextFileAsync("conflicts/source/file.txt", "payload");

        var result = await provider.RenameDirectoryAsync("conflicts/source", "conflicts/source/archive", CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>();
        await this.AssertDirectoryExistsAsync(provider, "conflicts/source");
        await this.AssertDirectoryDoesNotExistAsync(provider, "conflicts/source/archive");
        await this.AssertFileExistsAsync(provider, "conflicts/source/file.txt");
    }

    [Fact]
    public virtual async Task RenameDirectoryAsync_SourceInsideDestination_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("rename-source-inside-destination");
        await provider.CreateDirectoryAsync("conflicts/target/source", CancellationToken.None);
        await provider.WriteTextFileAsync("conflicts/target/source/file.txt", "payload");

        var result = await provider.RenameDirectoryAsync("conflicts/target/source", "conflicts/target", CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>();
        await this.AssertDirectoryExistsAsync(provider, "conflicts/target");
        await this.AssertDirectoryExistsAsync(provider, "conflicts/target/source");
        await this.AssertFileExistsAsync(provider, "conflicts/target/source/file.txt");
    }

    [Fact]
    public virtual async Task RenameDirectoryAsync_WhenDestinationIsExistingFile_ReturnsConflict()
    {
        var provider = this.CreateIsolatedProvider("rename-directory-file-collision");
        await provider.CreateDirectoryAsync("conflicts/source/child", CancellationToken.None);
        await provider.WriteTextFileAsync("conflicts/destination", "payload");

        var result = await provider.RenameDirectoryAsync("conflicts/source", "conflicts/destination", CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>("file");
        await this.AssertDirectoryExistsAsync(provider, "conflicts/source");
        await this.AssertFileExistsAsync(provider, "conflicts/destination");
    }

    [Fact]
    public virtual async Task RenderDirectoryAsync_FullStructure_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_FullStructure_Success(this.CreateIsolatedProvider("tree-full"));

    [Fact]
    public virtual async Task RenderDirectoryAsync_SubPath_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_SubPath_Success(this.CreateIsolatedProvider("tree-subpath"));

    [Fact]
    public virtual async Task RenderDirectoryAsync_EmptyStructure_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_EmptyStructure_Success(this.CreateIsolatedProvider("tree-empty"));

    [Fact]
    public virtual async Task RenderDirectoryAsync_SkipFiles_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_SkipFiles_Success(this.CreateIsolatedProvider("tree-skip-files"));

    [Fact]
    public virtual async Task RenderDirectoryAsync_ProgressReporting()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_ProgressReporting(this.CreateIsolatedProvider("tree-progress"));

    [Fact]
    public virtual async Task RenderDirectoryAsync_HtmlRenderer_Success()
        => await FileStorageTreeTestScenarios.RenderDirectoryAsync_HtmlRenderer_Success(this.CreateIsolatedProvider("tree-html"));

    [Fact]
    public virtual async Task CopyFileAsync_CrossProvider_EntityFrameworkSource_Success()
        => await FileStorageCrossProviderTestScenarios.CopyFileAsync_Success(
            this.CreateIsolatedProvider("cross-source"),
            this.CreateInMemoryProvider(this.CreateLocationName("cross-source-memory")));

    [Fact]
    public virtual async Task CopyFileAsync_CrossProvider_EntityFrameworkDestination_Success()
        => await FileStorageCrossProviderTestScenarios.CopyFileAsync_Success(
            this.CreateInMemoryProvider(this.CreateLocationName("cross-destination-memory")),
            this.CreateIsolatedProvider("cross-destination"));

    [Fact]
    public virtual async Task MoveFileAsync_CrossProvider_EntityFrameworkSource_Success()
        => await FileStorageCrossProviderTestScenarios.MoveFileAsync_Success(
            this.CreateIsolatedProvider("move-source"),
            this.CreateInMemoryProvider(this.CreateLocationName("move-source-memory")));

    [Fact]
    public virtual async Task MoveFileAsync_CrossProvider_EntityFrameworkDestination_Success()
        => await FileStorageCrossProviderTestScenarios.MoveFileAsync_Success(
            this.CreateInMemoryProvider(this.CreateLocationName("move-destination-memory")),
            this.CreateIsolatedProvider("move-destination"));

    protected override IFileStorageProvider CreateProvider()
        => this.sut ??= this.CreateIsolatedProvider(this.locationName);

    protected EntityFrameworkFileStorageProvider<StubDbContext> CreateIsolatedProvider(
        string scenarioName,
        EntityFrameworkFileStorageOptions options = null)
        => this.CreateProvider(this.CreateLocationName(scenarioName), options ?? this.DefaultOptions);

    protected string CreateLocationName(string scenarioName)
        => $"{scenarioName}-{Guid.NewGuid():N}";

    private async Task<TResult> ExecuteDbContextAsync<TResult>(Func<StubDbContext, Task<TResult>> operation)
    {
        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        return await operation(dbContext);
    }

    private async Task AssertFileMutationSamePathConflictAsync(FileMutationOperation operation, string path)
    {
        var provider = this.CreateIsolatedProvider("same-path");
        byte[] payload = "payload"u8.ToArray();
        await provider.WriteFileAsync(path, new MemoryStream(payload), null, CancellationToken.None);

        var result = await operation(provider, path, path);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>();
        await this.AssertReadFileAsync(provider, path, payload);
    }

    private async Task AssertFileMutationDirectoryCollisionConflictAsync(
        FileMutationOperation operation,
        string sourcePath,
        string destinationPath)
    {
        var provider = this.CreateIsolatedProvider("directory-collision");
        byte[] payload = "payload"u8.ToArray();
        await provider.WriteFileAsync(sourcePath, new MemoryStream(payload), null, CancellationToken.None);
        await provider.CreateDirectoryAsync(destinationPath, CancellationToken.None);

        var result = await operation(provider, sourcePath, destinationPath);

        result.ShouldBeFailure();
        result.ShouldContainError<ConflictError>("directory");
        await this.AssertReadFileAsync(provider, sourcePath, payload);
        await this.AssertDirectoryExistsAsync(provider, destinationPath);
    }

    private async Task<DateTimeOffset> GetDirectoryLastModifiedAsync(string locationName, string path)
        => await this.ExecuteDbContextAsync(dbContext =>
            dbContext.StorageDirectories
                .AsNoTracking()
                .Where(d => d.LocationName == locationName && d.NormalizedPath == path)
                .Select(d => d.LastModified)
                .SingleAsync());

    private async Task<IReadOnlyList<string>> GetDirectoryPathsAsync(string locationName)
        => await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDirectories
                .AsNoTracking()
                .Where(d => d.LocationName == locationName && d.NormalizedPath != string.Empty)
                .OrderBy(d => d.NormalizedPath)
                .Select(d => d.NormalizedPath)
                .ToListAsync());

    private async Task<IReadOnlyList<string>> GetFilePathsAsync(string locationName)
        => await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageFiles
                .AsNoTracking()
                .Where(f => f.LocationName == locationName)
                .OrderBy(f => f.NormalizedPath)
                .Select(f => f.NormalizedPath)
                .ToListAsync());

    private static async Task<byte[]> ReadAllBytesAsync(Result<Stream> result)
    {
        result.ShouldBeSuccess();
        await using var stream = result.Value;
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private static async Task<string> RenderTreeAsync(IFileStorageProvider provider, string path = null)
    {
        var renderer = new TextFileStorageTreeRenderer();
        var result = await provider.RenderDirectoryAsync(renderer, path, skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess();
        return renderer.ToString();
    }

    private static Task<Result> CopyFileAsync(IFileStorageProvider provider, string sourcePath, string destinationPath)
        => provider.CopyFileAsync(sourcePath, destinationPath, null, CancellationToken.None);

    private static Task<Result> RenameFileAsync(IFileStorageProvider provider, string sourcePath, string destinationPath)
        => provider.RenameFileAsync(sourcePath, destinationPath, null, CancellationToken.None);

    private static Task<Result> MoveFileAsync(IFileStorageProvider provider, string sourcePath, string destinationPath)
        => provider.MoveFileAsync(sourcePath, destinationPath, null, CancellationToken.None);

    private delegate Task<Result> FileMutationOperation(IFileStorageProvider provider, string sourcePath, string destinationPath);
}
