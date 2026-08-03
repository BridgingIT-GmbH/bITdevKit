// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Text.RegularExpressions;

[UnitTest("Application")]
public sealed partial class BlobStorageSourceGuardTests
{
    [Fact]
    public void ProductionBlobStorageSource_DoesNotDeclareInternalSymbols()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var directories = new[]
        {
            "src/Application.Storage/Blobs",
            "src/Infrastructure.EntityFramework/Storage/Blobs",
            "src/Infrastructure.Azure.Storage/Blobs",
            "src/Presentation.Web.Storage/Blobs"
        };

        // Act
        var violations = directories
            .Select(path => Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            .SelectMany(path => Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => new { File = file, Line = index + 1, Text = line }))
            .Where(candidate => InternalDeclarationRegex().IsMatch(candidate.Text))
            .Select(candidate => $"{Path.GetRelativePath(repositoryRoot, candidate.File)}:{candidate.Line}")
            .ToArray();

        // Assert
        violations.ShouldBeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("The repository root could not be located.");
    }

    [GeneratedRegex(@"^\s*(?:protected\s+|public\s+|private\s+)?internal\s+", RegexOptions.CultureInvariant)]
    private static partial Regex InternalDeclarationRegex();
}
