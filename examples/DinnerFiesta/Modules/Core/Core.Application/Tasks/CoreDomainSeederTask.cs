// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class CoreDomainSeederTask(
    ILoggerFactory loggerFactory,
    IGenericRepository<User> userRepository,
    IGenericRepository<Host> hostRepository,
    IGenericRepository<Menu> menuRepository,
    IGenericRepository<Dinner> dinnerRepository) : IStartupTask
{
    private readonly ILogger<CoreDomainSeederTask> logger = loggerFactory?.CreateLogger<CoreDomainSeederTask>() ?? NullLoggerFactory.Instance.CreateLogger<CoreDomainSeederTask>();

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await this.SeedUsers(userRepository);
        await this.SeedHosts(hostRepository);
        await this.SeedMenus(menuRepository);
        await this.SeedDinners(dinnerRepository);
    }

    private async Task SeedUsers(IGenericRepository<User> repository)
    {
        foreach (var entity in CoreSeedModels.Users(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                await repository.InsertAsync(entity);
            }
        }
    }

    private async Task SeedHosts(IGenericRepository<Host> repository)
    {
        foreach (var entity in CoreSeedModels.Hosts(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                await repository.InsertAsync(entity);
            }
        }
    }

    private async Task SeedMenus(IGenericRepository<Menu> repository)
    {
        foreach (var entity in CoreSeedModels.Menus(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                await repository.InsertAsync(entity);
            }
        }
    }

    private async Task SeedDinners(IGenericRepository<Dinner> repository)
    {
        foreach (var entity in CoreSeedModels.Dinners(0))
        {
            if (!await repository.ExistsAsync(entity.Id))
            {
                await repository.InsertAsync(entity);
            }
        }
    }
}
