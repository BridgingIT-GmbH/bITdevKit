// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Security;

[UnitTest("Common")]
public class EncryptionKeyProviderTests
{
    [Fact]
    public async Task GetActiveAndHistoricalKeyAsync_WithConfiguredKeys_ResolvesByIdentifier()
    {
        // Arrange
        var provider = new DictionaryEncryptionKeyProvider("current", new Dictionary<string, byte[]>
        {
            ["old"] = [1, 2, 3],
            ["current"] = [4, 5, 6]
        });

        // Act
        var active = await provider.GetActiveKeyAsync();
        var historical = await provider.GetKeyAsync("old");
        var missing = await provider.GetKeyAsync("missing");

        // Assert
        active.KeyId.ShouldBe("current");
        active.Key.ToArray().ShouldBe([4, 5, 6]);
        historical.Key.ToArray().ShouldBe([1, 2, 3]);
        missing.ShouldBeNull();
    }

    [Fact]
    public async Task Constructor_WithMutableKeyArray_CopiesSuppliedMaterial()
    {
        // Arrange
        var key = new byte[] { 1, 2, 3 };
        var provider = new DictionaryEncryptionKeyProvider(
            "current",
            new Dictionary<string, byte[]> { ["current"] = key });

        // Act
        key[0] = 9;
        var resolved = await provider.GetActiveKeyAsync();

        // Assert
        resolved.Key.ToArray().ShouldBe([1, 2, 3]);
    }
}
