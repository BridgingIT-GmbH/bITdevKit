// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Security.Cryptography;

[UnitTest("Common")]
public sealed class AesGcmContinuationTokenProtectorTests
{
    private const string Purpose = "catalog.cursor.v1";
    private const string FormatOneToken = "01000000000000000000000000B538C1260E7FE906EB270129E961ACF6A2C2275C2E194B0D723CB6BCC8";

    [Fact]
    public void TryUnprotect_FormatOneFixture_PreservesExistingFrameAndOwnsKeyCopy()
    {
        // Arrange
        var key = new byte[AesGcmContinuationTokenProtector.KeySize];
        IContinuationTokenProtector sut = new AesGcmContinuationTokenProtector(key);
        Array.Fill(key, (byte)255);

        // Act
        var restored = sut.TryUnprotect(Purpose, Convert.FromHexString(FormatOneToken), out var payload);

        // Assert
        restored.ShouldBeTrue();
        payload.ShouldBe("legacy cursor"u8.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(29)]
    public void TryUnprotect_TamperedFormatNonceTagOrCiphertext_ReturnsFalse(int position)
    {
        // Arrange
        IContinuationTokenProtector sut = new AesGcmContinuationTokenProtector(new byte[32]);
        var token = Convert.FromHexString(FormatOneToken);
        token[position] ^= 1;

        // Act
        var restored = sut.TryUnprotect(Purpose, token, out var payload);

        // Assert
        restored.ShouldBeFalse();
        payload.ShouldBeNull();
    }

    [Fact]
    public void TryUnprotect_WrongPurposeKeyOrBounds_ReturnsFalse()
    {
        // Arrange
        IContinuationTokenProtector sut = new AesGcmContinuationTokenProtector(new byte[32]);
        IContinuationTokenProtector other = new AesGcmContinuationTokenProtector(Enumerable.Repeat((byte)1, 32).ToArray());
        var token = Convert.FromHexString(FormatOneToken);

        // Act & Assert
        sut.TryUnprotect("other.cursor.v1", token, out var wrongPurpose).ShouldBeFalse();
        other.TryUnprotect(Purpose, token, out var wrongKey).ShouldBeFalse();
        sut.TryUnprotect(Purpose, [], out var empty).ShouldBeFalse();
        sut.TryUnprotect(Purpose, new byte[AesGcmContinuationTokenProtector.MaximumProtectedBytes + 1], out var excessive).ShouldBeFalse();
        wrongPurpose.ShouldBeNull();
        wrongKey.ShouldBeNull();
        empty.ShouldBeNull();
        excessive.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16384)]
    public void Protect_ValidBounds_RoundtripsThroughInstanceAndStaticApis(int length)
    {
        // Arrange
        var key = RandomNumberGenerator.GetBytes(32);
        var payload = RandomNumberGenerator.GetBytes(length);
        IContinuationTokenProtector sut = new AesGcmContinuationTokenProtector(key);

        // Act
        var first = sut.Protect(Purpose, payload);
        var second = AesGcmContinuationTokenProtector.Protect(payload, key, Purpose);

        // Assert
        first.Length.ShouldBe(length + 29);
        first.ShouldNotBe(second);
        AesGcmContinuationTokenProtector.Unprotect(first, key, Purpose).ShouldBe(payload);
        sut.TryUnprotect(Purpose, second, out var restored).ShouldBeTrue();
        restored.ShouldBe(payload);
    }

    [Fact]
    public void Protect_InvalidConfigurationOrPayload_RejectsInput()
    {
        // Arrange
        var key = new byte[32];
        var sut = new AesGcmContinuationTokenProtector(key);

        // Act & Assert
        Should.Throw<ArgumentException>(() => new AesGcmContinuationTokenProtector(new byte[31]));
        Should.Throw<ArgumentException>(() => sut.Protect(Purpose, new byte[AesGcmContinuationTokenProtector.MaximumPlaintextBytes + 1]));
        Should.Throw<ArgumentException>(() => sut.Protect(new string('p', 129), []));
        Should.Throw<ArgumentException>(() => sut.TryUnprotect("", [], out _));
        Should.Throw<ArgumentException>(() => sut.Protect(new string('\u20ac', 43), []));
    }

    [Fact]
    public void OpaqueContinuationTokenCodec_AesProtector_EncryptsAndRestoresJsonPayload()
    {
        // Arrange
        IContinuationTokenProtector sut = new AesGcmContinuationTokenProtector(RandomNumberGenerator.GetBytes(32));
        var payload = new CursorPayload("private-position");

        // Act
        var token = OpaqueContinuationTokenCodec.Serialize(payload, Purpose, sut);
        var restored = OpaqueContinuationTokenCodec.Deserialize<CursorPayload>(token, Purpose, sut);

        // Assert
        token.ShouldStartWith("p1.");
        token.ShouldNotContain(payload.Position);
        restored.ShouldBe(payload);
        Should.Throw<FormatException>(() => OpaqueContinuationTokenCodec.Deserialize<CursorPayload>(token, "other.cursor.v1", sut));
    }

    private sealed record CursorPayload(string Position);
}