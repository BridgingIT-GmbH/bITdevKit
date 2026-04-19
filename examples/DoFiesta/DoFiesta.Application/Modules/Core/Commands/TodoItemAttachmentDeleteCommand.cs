// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

/// <summary>
/// Deletes a todo-item attachment from the dedicated attachments storage location.
/// </summary>
/// <example>
/// <code>
/// var result = await requester.SendAsync(
///     new TodoItemAttachmentDeleteCommand(todoItemId, "notes.txt"),
///     cancellationToken);
/// </code>
/// </example>
[Command]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class TodoItemAttachmentDeleteCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentDeleteCommand"/> class.
    /// </summary>
    public TodoItemAttachmentDeleteCommand()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentDeleteCommand"/> class.
    /// </summary>
    /// <param name="todoItemId">The todo item identifier.</param>
    /// <param name="fileName">The attachment file name.</param>
    public TodoItemAttachmentDeleteCommand(string todoItemId, string fileName)
    {
        this.TodoItemId = todoItemId;
        this.FileName = fileName;
    }

    /// <summary>
    /// Gets the todo item identifier.
    /// </summary>
    [ValidateNotEmpty("Todo item id is required.")]
    [ValidateValidGuid("Invalid guid.")]
    public string TodoItemId { get; private set; }

    /// <summary>
    /// Gets the attachment file name.
    /// </summary>
    [ValidateNotEmpty("Attachment file name is required.")]
    public string FileName { get; private set; }

    [Handle]
    private async Task<Result<string>> HandleAsync(
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TodoItem> permissionEvaluator,
        IFileStorageProviderFactory providerFactory,
        CancellationToken cancellationToken)
    {
        var todoItemResult = await repository.FindOneResultAsync(BridgingIT.DevKit.Examples.DoFiesta.Domain.Model.TodoItemId.Create(this.TodoItemId), cancellationToken: cancellationToken)
            .EnsureAsync(
                async (todoItem, ct) => await permissionEvaluator.HasPermissionAsync(currentUserAccessor, todoItem.Id, Permission.Write, cancellationToken: ct),
                new UnauthorizedError(),
                cancellationToken);
        if (todoItemResult.IsFailure)
        {
            return Result<string>.Failure().WithErrors(todoItemResult.Errors);
        }

        IFileStorageProvider provider;
        try
        {
            provider = providerFactory.CreateProvider("attachments");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure()
                .WithError(new ExceptionError(ex, "Unable to resolve the 'attachments' file storage provider."));
        }

        var pathResult = TodoItemAttachmentPathHelper.CreateFilePath(this.TodoItemId, this.FileName);
        if (pathResult.IsFailure)
        {
            return Result<string>.Failure().WithErrors(pathResult.Errors);
        }

        var deleteResult = await provider.DeleteFileAsync(pathResult.Value, cancellationToken: cancellationToken);
        return deleteResult.IsSuccess
            ? Result<string>.Success(this.FileName)
            : Result<string>.Failure().WithErrors(deleteResult.Errors);
    }
}
