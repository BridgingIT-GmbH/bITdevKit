// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Sequences;

using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Threading.Tasks;
using Xunit;

[IntegrationTest("Infrastructure")]
public class InMemorySequenceNumberGeneratorTests : SequenceNumberGeneratorTestsBase
{
    private readonly ILoggerFactory mockLoggerFactory;

    public InMemorySequenceNumberGeneratorTests()
    {
        this.mockLoggerFactory = Substitute.For<ILoggerFactory>();
        this.mockLoggerFactory.CreateLogger(Arg.Any<string>())
            .Returns(Substitute.For<ILogger>());
    }

    protected override ISequenceNumberGenerator CreateGenerator()
    {
        var sut = new InMemorySequenceNumberGenerator();
        sut.ConfigureSequence("TestSequence", startValue: 1, increment: 1);
        sut.ConfigureSequence("OtherTestSequence1", startValue: 1, increment: 1);
        sut.ConfigureSequence("OtherTestSequence2", startValue: 1, increment: 1);

        return sut;
    }

    [Fact]
    public override async Task GetNextAsync_SequenceExists_ReturnsNextValue()
    {
        await base.GetNextAsync_SequenceExists_ReturnsNextValue();
    }

    [Fact]
    public override async Task GetNextAsync_SequenceDoesNotExist_ReturnsFailureWithNotFoundError()
    {
        await base.GetNextAsync_SequenceDoesNotExist_ReturnsFailureWithNotFoundError();
    }

    [Fact]
    public override async Task GetSequenceInfoAsync_SequenceExists_ReturnsInfo()
    {
        await base.GetSequenceInfoAsync_SequenceExists_ReturnsInfo();
    }

    [Fact]
    public override async Task ResetSequenceAsync_SequenceExists_ResetsSuccessfully()
    {
        await base.ResetSequenceAsync_SequenceExists_ResetsSuccessfully();
    }

    [Fact]
    public override async Task GetNextMultipleAsync_MultipleSequences_ReturnsValues()
    {
        await base.GetNextMultipleAsync_MultipleSequences_ReturnsValues();
    }
}
