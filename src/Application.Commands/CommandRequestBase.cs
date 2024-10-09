// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using FluentValidation.Results;

public abstract class CommandRequestBase(Guid id) : ICommandRequest<CommandResponse>
{
    protected CommandRequestBase()
        : this(GuidGenerator.CreateSequential()) { }

    public Guid RequestId { get; } = id;

    public DateTimeOffset RequestTimestamp { get; } = DateTime.UtcNow;

    public virtual ValidationResult Validate()
    {
        return new ValidationResult();
    }
}

public abstract class CommandRequestBase<TResult>(Guid id) : ICommandRequest<CommandResponse<TResult>>
{
    protected CommandRequestBase()
        : this(GuidGenerator.CreateSequential()) { }

    public Guid RequestId { get; } = id;

    public DateTimeOffset RequestTimestamp { get; } = DateTime.UtcNow;

    public virtual ValidationResult Validate()
    {
        return new ValidationResult();
    }
}