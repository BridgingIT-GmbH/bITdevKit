// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TitleShouldBeUniqueRule(string title, IGenericRepository<TodoItem> repository) : AsyncRuleBase
{
    public override string Message { get; } = "Title should not be used allready";

    public override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        return await repository
            .CountResultAsync(e => e.Title == title, cancellationToken) // use expression instead of specification
            .Ensure(e => e == 0, new ValidationError(this.Message)); // ensure title is not used already
    }
}