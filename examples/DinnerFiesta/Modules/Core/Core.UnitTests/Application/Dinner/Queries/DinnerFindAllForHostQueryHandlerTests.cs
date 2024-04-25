// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Application;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

public class DinnerFindAllForHostQueryHandlerTests
{
    [Fact]
    public async Task Process_ReturnsResult_Success()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var hostId = HostId.CreateUnique();
        var dinners = Stubs.Dinners(ticks).Take(2);
        var repository = Substitute.For<IGenericRepository<Dinner>>();
        repository.FindAllAsync(
            Arg.Any<ISpecification<Dinner>>(),
            Arg.Any<IFindOptions<Dinner>>(),
            Arg.Any<CancellationToken>()).Returns(dinners.AsEnumerable());
        var query = new DinnerFindAllForHostQuery(hostId.ToString());

        // Act
        var sut = new DinnerFindAllForHostQueryHandler(loggerFactory, repository);
        var response = await sut.Process(query, CancellationToken.None);

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldNotBeNull();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Value.ShouldBe(dinners);
    }
}