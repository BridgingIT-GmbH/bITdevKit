// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class MarketingDomainSeederTask(
    ILoggerFactory loggerFactory
    /*IGenericRepository<Customer> userRepository*/) : IStartupTask
{
    private readonly ILogger<MarketingDomainSeederTask> logger = loggerFactory?.CreateLogger<MarketingDomainSeederTask>() ?? NullLoggerFactory.Instance.CreateLogger<MarketingDomainSeederTask>();

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // no need to seed customers, as they are created by the UserCreatedMessageHandler
        //await this.SeedCustomers(userRepository);

        return Task.CompletedTask;
    }

    //private async Task SeedCustomers(IGenericRepository<Customer> repository)
    //{
    //    foreach (var entity in MarketingSeedModels.Customers(0))
    //    {
    //        if (!await repository.ExistsAsync(entity.Id))
    //        {
    //            await repository.InsertAsync(entity);
    //        }
    //    }
    //}
}