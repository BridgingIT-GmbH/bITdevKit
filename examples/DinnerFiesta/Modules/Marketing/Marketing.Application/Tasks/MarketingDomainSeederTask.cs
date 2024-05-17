// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class MarketingDomainSeederTask(
    ILoggerFactory loggerFactory,
    IGenericRepository<Customer> userRepository) : IStartupTask
{
    private readonly ILogger<MarketingDomainSeederTask> logger = loggerFactory?.CreateLogger<MarketingDomainSeederTask>() ?? NullLoggerFactory.Instance.CreateLogger<MarketingDomainSeederTask>();

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await this.SeedCustomers(userRepository);
    }

    private async Task SeedCustomers(IGenericRepository<Customer> repository)
    {
        foreach (var entity in MarketingSeedModels.Customers(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                entity.AuditState.SetCreated("seed");

                await repository.InsertAsync(entity);
            }
        }
    }
}