// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using BridgingIT.DevKit.Application.Storage;

/// <summary>Checks portable local storage paths against physical nested directories.</summary>
/// <example>dotnet test --filter FullyQualifiedName~LocalFileStoragePathTests</example>
public class LocalFileStoragePathTests
{
    /// <summary>Both accepted separators resolve to native directories and return canonical relative paths.</summary>
    /// <param name="filePath">The caller's relative file path.</param>
    /// <param name="trailingSeparator">Whether the configured root ends in a native separator.</param>
    /// <example>await test.NestedPaths_UseNativeDirectoriesAndCanonicalRelativeListings("nested/file.txt", false);</example>
    [Theory]
    [InlineData("nested/file.txt", false)]
    [InlineData("nested\\file.txt", false)]
    [InlineData("nested/file.txt", true)]
    [InlineData("nested\\file.txt", true)]
    public async Task NestedPaths_UseNativeDirectoriesAndCanonicalRelativeListings(string filePath, bool trailingSeparator)
    {
        var root = Path.Combine(Path.GetTempPath(), "bitdevkit-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var provider = new LocalFileStorageProvider("portable", root + (trailingSeparator ? Path.DirectorySeparatorChar.ToString() : string.Empty));
            await using var input = new MemoryStream([1, 2, 3]);
            (await provider.WriteFileAsync(filePath, input)).ShouldBeSuccess();
            File.Exists(Path.Combine(root, "nested", "file.txt")).ShouldBeTrue();

            var files = await provider.ListFilesAsync("nested/");
            files.ShouldBeSuccess();
            files.Value.Files.ShouldBe(["nested/file.txt"]);
            var directories = await provider.ListDirectoriesAsync("/");
            directories.ShouldBeSuccess();
            directories.Value.ShouldBe(["nested"]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
