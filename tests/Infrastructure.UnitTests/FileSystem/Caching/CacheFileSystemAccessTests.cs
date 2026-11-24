// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

/// <summary>Checks ownership protocol selection without claiming a network filesystem qualification.</summary>
public sealed class CacheFileSystemAccessTests
{
    /// <summary>Rejects known remote filesystems under a local or incompatible remote protocol.</summary>
    /// <param name="declared">The configured ownership protocol.</param>
    /// <param name="observed">The observable native filesystem kind.</param>
    [Theory]
    [InlineData("Local", "Smb")]
    [InlineData("Local", "Nfs")]
    [InlineData("Smb", "Nfs")]
    [InlineData("Nfs", "Smb")]
    [InlineData("Local", "Unsupported")]
    [InlineData("Smb", "Unsupported")]
    [InlineData("Nfs", "Unsupported")]
    [InlineData("local", "Local")]
    [InlineData(null, "Local")]
    public void MismatchedRemoteKind_IsRejected(string declared, string observed) =>
        Should.Throw<CacheTransferException>(() => CacheFileSystemAccess.ValidateKind(declared, observed)).Code.ShouldBe(CacheErrorCode.Unsupported);

    /// <summary>Preserves conservative shared-marker protocol tests on actual local temporary storage.</summary>
    /// <param name="declared">The local or stronger shared ownership protocol.</param>
    [Theory]
    [InlineData("Local")]
    [InlineData("Smb")]
    [InlineData("Nfs")]
    public void KnownLocalKind_AllowsConservativeProtocol(string declared) =>
        Should.NotThrow(() => CacheFileSystemAccess.ValidateKind(declared, "Local"));

    /// <summary>Classifies filesystem names independently of the machine running the tests.</summary>
    /// <param name="native">The filesystem name observed in a Linux mount table.</param>
    /// <param name="kind">The selected safe protocol class.</param>
    [Theory]
    [InlineData("ext4", "Local")]
    [InlineData("xfs", "Local")]
    [InlineData("overlay", "Local")]
    [InlineData("tmpfs", "Local")]
    [InlineData("cifs", "Smb")]
    [InlineData("nfs4", "Nfs")]
    [InlineData("fuse.sshfs", "Unsupported")]
    [InlineData("9p", "Unsupported")]
    public void LinuxFilesystemName_SelectsItsActualKind(string native, string kind) => CacheFileSystemAccess.ClassifyLinux(native).ShouldBe(kind);

    /// <summary>Recognizes network paths without contacting the named server.</summary>
    /// <param name="path">The ordinary or extended Windows filesystem path.</param>
    /// <param name="remote">Whether it names a UNC namespace.</param>
    [Theory]
    [InlineData(@"\\server\share\cache", true)]
    [InlineData(@"\\?\UNC\server\share\cache", true)]
    [InlineData(@"\\?\C:\cache", false)]
    [InlineData(@"C:\cache", false)]
    [InlineData(@"\\.\C:\cache", false)]
    [InlineData(@"\\?\unc\server\share\cache", true)]
    public void WindowsPath_RecognizesUncWithoutIo(string path, bool remote) => CacheFileSystemAccess.IsUncPath(path).ShouldBe(remote);

    /// <summary>Rejects the unsafe default local protocol for UNC before any cache root can be created.</summary>
    [SkippableFact]
    public void WindowsUncLocalProfile_IsRejectedBeforeCreation()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows UNC syntax is rejected by Windows profile construction.");
        Should.Throw<ArgumentException>(() => new FileSystemCacheOptions(new() { RootPath = @"\\cache-probe.invalid\share\cache", AccessKind = "Local" }));
    }

    /// <summary>Inspects the OS temporary filesystem without creating the proposed root.</summary>
    [Fact]
    public void LocalTemporaryRoot_ValidatesWithoutCreatingCacheFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "cache-kind-" + Guid.NewGuid().ToString("N"));

        CacheFileSystemAccess.ValidateBeforeCreate(root, "Local");

        Directory.Exists(root).ShouldBeFalse();
    }

    /// <summary>Allows conservative shared-marker protocol checks on local temporary storage without claiming share qualification.</summary>
    /// <param name="declared">The shared ownership protocol exercised on a local filesystem.</param>
    [Theory]
    [InlineData("Smb")]
    [InlineData("Nfs")]
    public void ValidateBeforeCreate_LocalTemporaryRoot_AllowsConservativeProtocol(string declared)
    {
        var root = Path.Combine(Path.GetTempPath(), "cache-kind-" + Guid.NewGuid().ToString("N"));

        Should.NotThrow(() => CacheFileSystemAccess.ValidateBeforeCreate(root, declared));

        Directory.Exists(root).ShouldBeFalse();
    }
}