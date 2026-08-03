// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Security.Cryptography;

[UnitTest("Common")]
public class OpaqueContinuationTokenCodecTests
{
    [Fact]
    public void SerializeDeserialize_WithoutProtector_Roundtrips()
    {
        // Arrange
        var payload = new TokenPayload("azure", "hash");

        // Act
        var token = OpaqueContinuationTokenCodec.Serialize(payload, "blob-storage");
        var result = OpaqueContinuationTokenCodec.Deserialize<TokenPayload>(token, "blob-storage");

        // Assert
        token.ShouldStartWith("u1.");
        result.ShouldBe(payload);
    }

    [Fact]
    public void Deserialize_WithProtector_RejectsTamperingWrongPurposeAndUnsignedTokens()
    {
        // Arrange
        var protector = new HmacContinuationTokenProtector(RandomNumberGenerator.GetBytes(32));
        var token = OpaqueContinuationTokenCodec.Serialize(new TokenPayload("azure", "hash"), "blob-storage", protector);
        var tampered = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');
        var unsigned = OpaqueContinuationTokenCodec.Serialize(new TokenPayload("azure", "hash"), "blob-storage");

        // Act & Assert
        OpaqueContinuationTokenCodec.Deserialize<TokenPayload>(token, "blob-storage", protector).Provider.ShouldBe("azure");
        Should.Throw<FormatException>(() => OpaqueContinuationTokenCodec.Deserialize<TokenPayload>(tampered, "blob-storage", protector));
        Should.Throw<FormatException>(() => OpaqueContinuationTokenCodec.Deserialize<TokenPayload>(token, "document-storage", protector));
        Should.Throw<FormatException>(() => OpaqueContinuationTokenCodec.Deserialize<TokenPayload>(unsigned, "blob-storage", protector));
    }

    private sealed record TokenPayload(string Provider, string QueryHash);
}
