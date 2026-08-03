// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class TemporaryFileHelperTests
{
    [Fact]
    public void Dispose_WithCreatedLease_ClosesAndDeletesFile()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"bdk-test-{Guid.NewGuid():N}");
        var lease = TemporaryFileHelper.Create(directory);
        var path = lease.Path;

        // Act
        lease.Dispose();

        // Assert
        File.Exists(path).ShouldBeFalse();
        Directory.Delete(directory);
    }

    [Fact]
    public async Task DisposeAsync_WithCreatedLease_ClosesAndDeletesFile()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), $"bdk-test-{Guid.NewGuid():N}");
        var lease = TemporaryFileHelper.Create(directory, "sample-", "bin");
        var path = lease.Path;
        await lease.Stream.WriteAsync("payload"u8.ToArray());

        // Act
        await lease.DisposeAsync();

        // Assert
        File.Exists(path).ShouldBeFalse();
        Directory.Delete(directory);
    }
}
