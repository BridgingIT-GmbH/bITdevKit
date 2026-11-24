// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Security.Cryptography;

/// <summary>Checks authenticated confidential continuation bounds and caller-owned buffers.</summary>
public sealed class AesGcmContinuationProtectionTests
{
    /// <summary>Empty through maximum-size continuations round-trip without changing caller data or keys.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(16384)]
    public void Protect_WithinBounds_RoundTripsWithoutChangingInputs(int length)
    {
        var secret = RandomNumberGenerator.GetBytes(AesGcmContinuationTokenProtector.KeySize);
        var secretBefore = secret.ToArray();
        var original = RandomNumberGenerator.GetBytes(length);
        var before = original.ToArray();
        var protectedData = AesGcmContinuationTokenProtector.Protect(original, secret, "test.cursor.v1");
        var restored = AesGcmContinuationTokenProtector.Unprotect(protectedData, secret, "test.cursor.v1");

        restored.ShouldBe(original);
        original.ShouldBe(before);
        secret.ShouldBe(secretBefore);
        protectedData.Length.ShouldBe(length + 29);
    }

    /// <summary>Repeated protection uses independent unpredictable nonces.</summary>
    [Fact]
    public void Protect_SameProgress_UsesDifferentCiphertext()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var first = AesGcmContinuationTokenProtector.Protect([1, 2, 3], secret, "test.cursor.v1");
        var second = AesGcmContinuationTokenProtector.Protect([1, 2, 3], secret, "test.cursor.v1");
        first.SequenceEqual(second).ShouldBeFalse();
    }

    /// <summary>Store secrets and fixed provider purposes both bind authentication.</summary>
    [Fact]
    public void Unprotect_WrongSecretOrPurpose_RejectsAuthentication()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var token = AesGcmContinuationTokenProtector.Protect([1, 2, 3], secret, "test.cursor.v1");
        Assert.ThrowsAny<CryptographicException>(() => AesGcmContinuationTokenProtector.Unprotect(token, RandomNumberGenerator.GetBytes(32), "test.cursor.v1"));
        Assert.ThrowsAny<CryptographicException>(() => AesGcmContinuationTokenProtector.Unprotect(token, secret, "other.cursor.v1"));
    }

    /// <summary>Nonce, authentication tag, and ciphertext edits cannot be interpreted as progress.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(29)]
    public void Unprotect_TamperedCiphertext_RejectsBeforeReturningPlaintext(int offset)
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var token = AesGcmContinuationTokenProtector.Protect([1, 2, 3], secret, "test.cursor.v1");
        token[offset] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => AesGcmContinuationTokenProtector.Unprotect(token, secret, "test.cursor.v1"));
    }

    /// <summary>All public data/key/purpose sizes and the protected format are checked before allocating plaintext.</summary>
    [Fact]
    public void Protector_InvalidBoundsOrFormat_RejectsInput()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        Should.Throw<ArgumentException>(() => AesGcmContinuationTokenProtector.Protect(new byte[16385], secret, "test.cursor.v1"));
        Should.Throw<ArgumentException>(() => AesGcmContinuationTokenProtector.Protect([1], new byte[31], "test.cursor.v1"));
        Should.Throw<ArgumentException>(() => AesGcmContinuationTokenProtector.Protect([1], secret, new string('p', 129)));
        Should.Throw<InvalidDataException>(() => AesGcmContinuationTokenProtector.Unprotect(new byte[28], secret, "test.cursor.v1"));
        Should.Throw<InvalidDataException>(() => AesGcmContinuationTokenProtector.Unprotect(new byte[AesGcmContinuationTokenProtector.MaximumProtectedBytes + 1], secret, "test.cursor.v1"));
        var token = AesGcmContinuationTokenProtector.Protect([1], secret, "test.cursor.v1");
        token[0] = 2;
        Should.Throw<InvalidDataException>(() => AesGcmContinuationTokenProtector.Unprotect(token, secret, "test.cursor.v1"));
    }
}