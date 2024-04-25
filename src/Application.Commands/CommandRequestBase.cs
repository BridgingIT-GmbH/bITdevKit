// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using System;
using BridgingIT.DevKit.Common;
using FluentValidation.Results;

public abstract class CommandRequestBase : ICommandRequest<CommandResponse>
{
    protected CommandRequestBase()
        : this(GuidGenerator.CreateSequential().ToString("N"))
    {
    }

    protected CommandRequestBase(string id)
    {
        this.Id = id;
        this.Timestamp = DateTime.UtcNow;
    }

    public string Id { get; private set; }

    public DateTimeOffset Timestamp { get; private set; }

    public virtual ValidationResult Validate() => new();
}

public abstract class CommandRequestBase<TResult> : ICommandRequest<CommandResponse<TResult>>
{
    protected CommandRequestBase()
        : this(GuidGenerator.CreateSequential().ToString("N"))
    {
    }

    protected CommandRequestBase(string id)
    {
        this.Id = id;
        this.Timestamp = DateTime.UtcNow;
    }

    public string Id { get; private set; }

    public DateTimeOffset Timestamp { get; }

    public virtual ValidationResult Validate() => new();
}