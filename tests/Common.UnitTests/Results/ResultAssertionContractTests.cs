// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

[UnitTest("Common")]
public class ResultAssertionContractTests
{
    [Fact]
    public void ShouldBeValue_NullValueAndNonNullExpectation_Throws()
    {
        var sut = Result<string>.Success(value: null);

        Should.Throw<ShouldAssertException>(() => sut.ShouldBeValue("expected"));
        Should.NotThrow(() => sut.ShouldBeValue(null));
    }

    [Fact]
    public void ShouldNotBeValue_NullValueAndNullExpectation_Throws()
    {
        var sut = Result<string>.Success(value: null);

        Should.Throw<ShouldAssertException>(() => sut.ShouldNotBeValue(null));
        Should.NotThrow(() => sut.ShouldNotBeValue("different"));
    }
}
