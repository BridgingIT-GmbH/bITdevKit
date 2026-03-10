// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;
using FluentValidation;

public class TodoItemImportCommand(Stream stream, DataPorterFormat format) : RequestBase<ImportResult<TodoItemModel>>
{
    public Stream Stream { get; } = stream;

    public DataPorterFormat Format { get; } = format;

    public class Validator : AbstractValidator<TodoItemImportCommand>
    {
        public Validator()
        {
            this.RuleFor(x => x.Stream)
                .NotNull()
                .WithMessage("Stream is required");

            this.RuleFor(x => x.Format)
                .IsInEnum()
                .WithMessage("Invalid import format");
        }
    }
}
