// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Queries;

using Application.Queries;
using Microsoft.Extensions.Logging;

public class StubPersonQueryHandler(ILoggerFactory loggerFactory) : QueryHandlerBase<StubPersonQuery, IEnumerable<PersonStub>>(loggerFactory)
{
    public override async Task<QueryResponse<IEnumerable<PersonStub>>> Process(StubPersonQuery request, CancellationToken cancellationToken)
    {
        var validationResult = request.Validate();
        if (!validationResult.IsValid)
        {
            return await Task.FromResult(new QueryResponse<IEnumerable<PersonStub>>(string.Join(", ",
                    validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))))
                .AnyContext();
        }

        // TODO : repo verwenden

        var persons = new List<PersonStub>
        {
            new() { Id = Guid.NewGuid(), FirstName = "Frank", LastName = "Sinatra" },
            new() { Id = Guid.NewGuid(), FirstName = "John", LastName = "Wick" },
            new() { Id = Guid.NewGuid(), FirstName = "Petra", LastName = "Mustermann" },
            new() { Id = Guid.NewGuid(), FirstName = "John", LastName = "Smith" }
        };

        return new QueryResponse<IEnumerable<PersonStub>>
        {
            Result = persons.Where(x =>
                string.Equals(x.FirstName, request.FirstName, StringComparison.OrdinalIgnoreCase))
        };
    }
}