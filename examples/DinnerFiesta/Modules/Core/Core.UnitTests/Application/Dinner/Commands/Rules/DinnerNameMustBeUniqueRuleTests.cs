// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using NSubstitute;
using Shouldly;
using Xunit;

public class DinnerNameMustBeUniqueRuleTests
{
    [Fact]
    public async Task IsSatisfiedAsync_Checks_Success()
    {
        // Arrange
        const string name = "Garden Delights";
        var repository = Substitute.For<IGenericRepository<Dinner>>();
        repository.FindAllAsync(
            Arg.Any<ISpecification<Dinner>>(),
            Arg.Any<IFindOptions<Dinner>>(),
            Arg.Any<CancellationToken>()).Returns([]);
        var rule = new DinnerNameMustBeUniqueRule(repository, name);

        // Act
        var result = await rule.ApplyAsync();

        // Assert
        result.ShouldBeTrue();
    }
}
