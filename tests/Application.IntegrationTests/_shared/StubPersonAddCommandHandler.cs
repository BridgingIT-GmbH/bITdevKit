// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Commands;

using Application.Commands;
using Microsoft.Extensions.Logging;

public class StubPersonAddCommandHandler(ILoggerFactory loggerFactory) : CommandHandlerBase<StubPersonAddCommand, bool>(loggerFactory)
{
    public override async Task<CommandResponse<bool>> Process(StubPersonAddCommand request, CancellationToken cancellationToken)
    {
        var validationResult = request.Validate();
        if (!validationResult.IsValid)
        {
            // TODO entscheidung: cancel oder exception?
            // throw new FluentValidation.ValidationException(validationResult.Errors);
            return await Task.FromResult(new CommandResponse<bool>(string.Join(", ",
                    validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))))
                .AnyContext();
        }

        // TODO : repo verwenden
        //request.Person = await this.repository.InsertAsync(request.Person).AnyContext();
        this.Logger.LogInformation("command: person inserted");
        request.Person.Id = Guid.NewGuid();

        return await Task.FromResult(new CommandResponse<bool> { Result = true })
            .AnyContext();
    }
}