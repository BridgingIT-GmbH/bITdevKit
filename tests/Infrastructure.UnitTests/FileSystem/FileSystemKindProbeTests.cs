// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Checks filesystem observations using pure classifications and local OS temporary storage.</summary>
public sealed class FileSystemKindProbeTests
{
    /// <summary>Recognizes supported Linux filesystem names and leaves unknown names unsupported.</summary>
    /// <param name="name">The mount-table filesystem name.</param>
    /// <param name="expected">The observed filesystem kind.</param>
    [Theory]
    [InlineData("ext4", FileSystemKind.Local)]
    [InlineData("xfs", FileSystemKind.Local)]
    [InlineData("overlay", FileSystemKind.Local)]
    [InlineData("tmpfs", FileSystemKind.Local)]
    [InlineData("ntfs3", FileSystemKind.Local)]
    [InlineData("exfat", FileSystemKind.Local)]
    [InlineData("cifs", FileSystemKind.Smb)]
    [InlineData("smb3", FileSystemKind.Smb)]
    [InlineData("smbfs", FileSystemKind.Smb)]
    [InlineData("nfs", FileSystemKind.Nfs)]
    [InlineData("nfs4", FileSystemKind.Nfs)]
    [InlineData("fuse.sshfs", FileSystemKind.Unsupported)]
    [InlineData("9p", FileSystemKind.Unsupported)]
    [InlineData("EXT4", FileSystemKind.Unsupported)]
    [InlineData("", FileSystemKind.Unsupported)]
    [InlineData(null, FileSystemKind.Unsupported)]
    public void ClassifyLinux_MountName_ReturnsObservedKind(string name, FileSystemKind expected)
    {
        var result = FileSystemKindProbe.ClassifyLinux(name);

        result.ShouldBe(expected);
    }

    /// <summary>Distinguishes UNC namespaces from local extended paths and device namespaces without I/O.</summary>
    /// <param name="path">The Windows namespace syntax.</param>
    /// <param name="expected">Whether the syntax denotes a UNC path.</param>
    [Theory]
    [InlineData(@"\\server.invalid\share\reports", true)]
    [InlineData(@"\\?\UNC\server.invalid\share\reports", true)]
    [InlineData(@"\\?\unc\server.invalid\share\reports", true)]
    [InlineData(@"C:\reports", false)]
    [InlineData(@"\\?\C:\reports", false)]
    [InlineData(@"\\?\Volume{00000000-0000-0000-0000-000000000000}\reports", false)]
    [InlineData(@"\\.\C:\reports", false)]
    [InlineData(@"\\.\pipe\reports", false)]
    [InlineData(@"reports\daily", false)]
    public void IsUncPath_WindowsSyntax_DistinguishesDevicePaths(string path, bool expected)
    {
        var result = FileSystemKindProbe.IsUncPath(path);

        result.ShouldBe(expected);
    }

    /// <summary>Reports the named argument when no path is supplied to the pure syntax classifier.</summary>
    [Fact]
    public void IsUncPath_NullPath_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => FileSystemKindProbe.IsUncPath(null)).ParamName.ShouldBe("path");

    /// <summary>Observes the existing OS temporary directory without creating storage.</summary>
    [SkippableFact]
    public void Probe_ExistingTemporaryDirectory_ReturnsLocalKind()
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Local native observations support Windows and Linux.");

        var result = FileSystemKindProbe.Probe(Path.GetFullPath(Path.GetTempPath()));

        result.ShouldBe(FileSystemKind.Local);
    }

    /// <summary>Finds the containing filesystem even when the proposed root and its immediate ancestor do not exist.</summary>
    [SkippableFact]
    public void Probe_MissingTemporaryDescendant_DoesNotCreateDirectories()
    {
        Skip.IfNot(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Local native observations support Windows and Linux.");
        var parent = Path.Combine(Path.GetTempPath(), "filesystem-kind-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "reports");

        var result = FileSystemKindProbe.Probe(root);

        result.ShouldBe(FileSystemKind.Local);
        Directory.Exists(root).ShouldBeFalse();
        Directory.Exists(parent).ShouldBeFalse();
    }
}