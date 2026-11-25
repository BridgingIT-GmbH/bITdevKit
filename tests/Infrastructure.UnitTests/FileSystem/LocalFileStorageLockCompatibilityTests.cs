// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using Infrastructure.FileSystem;

/// <summary>Verifies the local file provider keeps its path comparer and disposal behavior.</summary>
/// <example><code>using var provider = new LocalFileStorageProvider("reports", root);</code></example>
public sealed class LocalFileStorageLockCompatibilityTests
{
    /// <summary>Checks that path casing controls lock identity according to the operating system.</summary>
    /// <example><code>var result = await provider.OpenWriteFileAsync("Report", useTemporaryWrite: true);</code></example>
    [LocalFileSystemFact]
    public async Task OpenWriteFileAsync_CaseVariantPath_UsesPlatformLockComparer()
    {
        using var root = new OwnedFileSystemRoot();
        using var sut = new LocalFileStorageProvider("reports", root.Path, lockTimeout: TimeSpan.Zero);
        var opened = await sut.OpenWriteFileAsync("Report", useTemporaryWrite: true);
        opened.IsSuccess.ShouldBeTrue();
        await using var writer = opened.Value;

        var caseVariant = await sut.OpenWriteFileAsync("report", useTemporaryWrite: true);

        await using var competingWriter = caseVariant.Value;
        caseVariant.HasError<TimeoutError>().ShouldBe(OperatingSystem.IsWindows());
        caseVariant.IsSuccess.ShouldBe(!OperatingSystem.IsWindows());
    }

    /// <summary>Checks the disposed object reported by the existing File Storage API.</summary>
    /// <example><code>provider.Dispose();</code></example>
    [LocalFileSystemFact]
    public async Task FileExistsAsync_DisposedProvider_PreservesDisposedObjectName()
    {
        using var root = new OwnedFileSystemRoot();
        using var sut = new LocalFileStorageProvider("reports", root.Path);
        sut.Dispose();

        var error = await Should.ThrowAsync<ObjectDisposedException>(() => sut.FileExistsAsync("report"));

        error.ObjectName.ShouldBe("BridgingIT.DevKit.Infrastructure.FileSystem.FileStorageLockPool");
    }
}
