// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.FileSystem;

using BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Checks native Windows path conversion independently of the current operating system.</summary>
public sealed class FileSystemWindowsPathTests
{
    /// <summary>Preserves publication's existing extended-prefix conversion without normalizing the path.</summary>
    /// <param name="path">The fully qualified Windows path.</param>
    /// <param name="expected">The path passed to native APIs.</param>
    [Theory]
    [InlineData(@"C:\reports\daily.txt", @"\\?\C:\reports\daily.txt")]
    [InlineData(@"C:\", @"\\?\C:\")]
    [InlineData(@"\\server.invalid\share\daily.txt", @"\\?\UNC\server.invalid\share\daily.txt")]
    [InlineData(@"\\?\C:\reports\daily.txt", @"\\?\C:\reports\daily.txt")]
    [InlineData(@"\\?\UNC\server.invalid\share\daily.txt", @"\\?\UNC\server.invalid\share\daily.txt")]
    [InlineData(@"\\?\unc\server.invalid\share\daily.txt", @"\\?\unc\server.invalid\share\daily.txt")]
    [InlineData(@"\\.\C:\reports\daily.txt", @"\\?\UNC\.\C:\reports\daily.txt")]
    public void ToExtendedWindowsPath_AbsolutePath_PreservesNativeConversion(string path, string expected)
    {
        var result = FileSystemPaths.ToExtendedWindowsPath(path);

        result.ShouldBe(expected);
    }

    /// <summary>Reports the named argument when no path is supplied.</summary>
    [Fact]
    public void ToExtendedWindowsPath_NullPath_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => FileSystemPaths.ToExtendedWindowsPath(null)).ParamName.ShouldBe("path");
}