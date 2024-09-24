// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application.Jobs;

using Common;
using DevKit.Application.JobScheduling;
using DevKit.Application.Storage;
using DevKit.Domain.Repositories;
using Domain;
using Microsoft.Extensions.Logging;
using Quartz;

public class DinnerSnapshotExportJob(
    ILoggerFactory loggerFactory,
    IGenericRepository<Dinner> dinnerRepository,
    IGenericRepository<Menu> menuRepository,
    IDocumentStoreClient<DinnerSnapshotDocument>
        documentStoreClient) : JobBase(loggerFactory) //IRetryJob, IChaosExceptionJobScheduling
{
    //RetryJobOptions IRetryJob.Options => new() { Attempts = 3, Backoff = new TimeSpan(0, 0, 0, 1) };

    //ChaosExceptionJobSchedulingOptions IChaosExceptionJobScheduling.Options => new() { InjectionRate = 0.10 };

    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var dinners = await dinnerRepository.FindAllAsync(cancellationToken: cancellationToken);
        var menus = await menuRepository.FindAllAsync(cancellationToken: cancellationToken);

        foreach (var dinner in dinners.SafeNull())
        {
            var menu = menus.FirstOrDefault(m => m.Id == dinner.MenuId);
            var document = new DinnerSnapshotDocument
            {
                Id = dinner.Id.ToString(),
                Name = dinner.Name,
                Description = dinner.Description,
                Menu = new MenuSnapshotDocument
                {
                    Id = menu?.Id?.ToString(), Name = menu?.Name, Description = menu?.Description
                }
            };
            // TODO: map everything incl. the menus to the snapshot

            await documentStoreClient.UpsertAsync(new DocumentKey("Dinner", document.Id),
                document,
                cancellationToken);
        }
    }
}