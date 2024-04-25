// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using System;
using BridgingIT.DevKit.Common;
using FluentValidation.Results;

public abstract class QueryRequestBase<TResult> :
    IQueryRequest<QueryResponse<TResult>>,
    IQueryHandler
{
    protected QueryRequestBase()
        : this(GuidGenerator.CreateSequential().ToString("N"))
    {
    }

    protected QueryRequestBase(string id)
    {
        this.Id = id;
        this.Timestamp = DateTime.UtcNow;
    }

    public string Id { get; }

    public DateTimeOffset Timestamp { get; }

    public virtual ValidationResult Validate() => new();
}