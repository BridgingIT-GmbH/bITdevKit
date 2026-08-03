// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public sealed class EncryptionHelperTests
{
    [Fact]
    public async Task EncryptAndDecryptAsync_WithString_ShouldRoundtrip()
    {
        // Arrange
        const string source = "secret text";
        var key = EncryptionHelper.GenerateAesKey();

        // Act
        var encrypted = await EncryptionHelper.EncryptAsync(source, key);
        var decrypted = await EncryptionHelper.DecryptAsync(encrypted, key);

        // Assert
        encrypted.ShouldNotBeNull();
        encrypted.ShouldNotBe(source);
        decrypted.ShouldBe(source);
    }

    [Fact]
    public async Task EncryptAndDecryptAsync_WithBytes_ShouldRoundtrip()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("secret bytes");
        var key = EncryptionHelper.GenerateAesKey();

        // Act
        var encrypted = await EncryptionHelper.EncryptAsync(source, key);
        var decrypted = await EncryptionHelper.DecryptAsync(encrypted, key);

        // Assert
        encrypted.ShouldNotBeNull();
        encrypted.Length.ShouldBeGreaterThan(source.Length);
        encrypted[..EncryptionHelper.AesBlockSizeBytes].ShouldNotBe(source.Take(EncryptionHelper.AesBlockSizeBytes).ToArray());
        decrypted.ShouldBe(source);
    }

    [Fact]
    public async Task EncryptAndDecryptAsync_WithNullPayloads_ShouldReturnNull()
    {
        // Arrange
        var key = EncryptionHelper.GenerateAesKey();

        // Act
        var encryptedString = await EncryptionHelper.EncryptAsync((string)null, key);
        var decryptedString = await EncryptionHelper.DecryptAsync((string)null, key);
        var encryptedBytes = await EncryptionHelper.EncryptAsync((byte[])null, key);
        var decryptedBytes = await EncryptionHelper.DecryptAsync((byte[])null, key);

        // Assert
        encryptedString.ShouldBeNull();
        decryptedString.ShouldBeNull();
        encryptedBytes.ShouldBeNull();
        decryptedBytes.ShouldBeNull();
    }

    [Fact]
    public async Task DecryptAsync_WithPayloadWithoutCiphertext_ShouldThrow()
    {
        // Arrange
        var key = EncryptionHelper.GenerateAesKey();
        var payload = EncryptionHelper.GenerateAesCbcInitializationVector();

        // Act
        var action = () => EncryptionHelper.DecryptAsync(payload, key);

        // Assert
        await Should.ThrowAsync<InvalidDataException>(action);
    }

    [Fact]
    public async Task CreateAesCbcStreams_ShouldRoundtripBytes()
    {
        // Arrange
        var source = Encoding.UTF8.GetBytes("secret payload");
        var key = EncryptionHelper.GenerateAesKey();
        var iv = EncryptionHelper.GenerateAesCbcInitializationVector();
        await using var encrypted = new MemoryStream();

        // Act
        await using (var encryptor = EncryptionHelper.CreateAesCbcEncryptionStream(encrypted, key, iv))
        {
            await encryptor.WriteAsync(source);
            encryptor.FlushFinalBlock();
        }

        encrypted.Position = 0;
        await using var decrypted = new MemoryStream();
        await using (var decryptor = EncryptionHelper.CreateAesCbcDecryptionStream(encrypted, key, iv))
        {
            await decryptor.CopyToAsync(decrypted);
        }

        // Assert
        decrypted.ToArray().ShouldBe(source);
    }

    [Fact]
    public void GenerateAesKey_WithDefaultSize_ReturnsAes256Key()
    {
        // Act
        var result = EncryptionHelper.GenerateAesKey();

        // Assert
        result.Length.ShouldBe(EncryptionHelper.Aes256KeySizeBytes);
        EncryptionHelper.IsValidAesKey(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateAesCbcInitializationVector_ReturnsBlockSizedVector()
    {
        // Act
        var result = EncryptionHelper.GenerateAesCbcInitializationVector();

        // Assert
        result.Length.ShouldBe(EncryptionHelper.AesBlockSizeBytes);
        EncryptionHelper.IsValidAesInitializationVector(result).ShouldBeTrue();
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(24, true)]
    [InlineData(32, true)]
    [InlineData(15, false)]
    [InlineData(33, false)]
    public void IsValidAesKeySize_WithKnownSizes_ReturnsExpectedValue(int keySize, bool expected)
    {
        // Act
        var result = EncryptionHelper.IsValidAesKeySize(keySize);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void CreateAesCbcEncryptionStream_WithInvalidKey_Throws()
    {
        // Arrange
        using var destination = new MemoryStream();

        // Act
        var action = () => EncryptionHelper.CreateAesCbcEncryptionStream(
            destination,
            [1, 2, 3],
            EncryptionHelper.GenerateAesCbcInitializationVector());

        // Assert
        Should.Throw<ArgumentException>(action)
            .Message.ShouldContain("AES key");
    }

    [Fact]
    public void CreateAesCbcDecryptionStream_WithInvalidInitializationVector_Throws()
    {
        // Arrange
        using var source = new MemoryStream();

        // Act
        var action = () => EncryptionHelper.CreateAesCbcDecryptionStream(
            source,
            EncryptionHelper.GenerateAesKey(),
            [1, 2, 3]);

        // Assert
        Should.Throw<ArgumentException>(action)
            .Message.ShouldContain("initialization vector");
    }
}
