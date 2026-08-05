// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class NameGeneratorTests
{
    [Fact]
    public void Create_Repeatedly_ReturnsVariedLowercaseNames()
    {
        // Arrange
        const int sampleSize = 100;

        // Act
        var names = Enumerable.Range(0, sampleSize)
            .Select(_ => NameGenerator.Create())
            .ToArray();

        // Assert
        names.All(name => !string.IsNullOrWhiteSpace(name)).ShouldBeTrue();
        names.All(name => name.All(IsLowercaseAsciiLetter)).ShouldBeTrue();
        names.Distinct().Count().ShouldBeGreaterThan(1);
    }

    private static bool IsLowercaseAsciiLetter(char value) =>
        value is >= 'a' and <= 'z';
}
