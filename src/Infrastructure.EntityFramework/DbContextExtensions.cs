// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public static class DbContextExtensions
{
    /// <summary>
    /// Only save entities of the specified type <see cref="TEntity"/>
    /// </summary>
    public static async Task<int> SaveChangesAsync<TEntity>(this DbContext context, CancellationToken cancellationToken = default(CancellationToken))
        where TEntity : class
    {
        var states = context.ChangeTracker.Entries()
                    .Where(e => e.Entity is not TEntity && e.State is not EntityState.Unchanged)
                    .GroupBy(x => x.State)
                    .ToList();

        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.Entity is not TEntity))
        {
            entry.State = EntityState.Unchanged;
        }

        var count = await context.SaveChangesAsync(cancellationToken);

        foreach (var state in states) // reset the states on the 'unchanged' entities
        {
            foreach (var entry in state)
            {
                entry.State = state.Key;
            }
        }

        return count;
    }
}
