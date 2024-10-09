// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using FluentValidation.Results;

public abstract class QueryRequestBase<TResult>(Guid id) : IQueryRequest<QueryResponse<TResult>>, IQueryHandler
{
    protected QueryRequestBase()
        : this(GuidGenerator.CreateSequential()) { }

    public Guid RequestId { get; } = id;

    public DateTimeOffset RequestTimestamp { get; } = DateTime.UtcNow;

    public virtual ValidationResult Validate()
    {
        return new ValidationResult();
    }
}