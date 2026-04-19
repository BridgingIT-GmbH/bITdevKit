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
/// Reads the content stream of a todo-item attachment.
/// </summary>
/// <example>
/// <code>
/// var result = await requester.SendAsync(new TodoItemAttachmentReadQuery(todoItemId, "notes.txt"), cancellationToken);
/// if (result.IsSuccess)
/// {
///     await using var stream = result.Value;
///     // Copy the stream to the HTTP response or another destination.
/// }
/// </code>
/// </example>
[Query]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class TodoItemAttachmentReadQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentReadQuery"/> class.
    /// </summary>
    public TodoItemAttachmentReadQuery()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentReadQuery"/> class.
    /// </summary>
    /// <param name="todoItemId">The todo item identifier.</param>
    /// <param name="fileName">The attachment file name.</param>
    public TodoItemAttachmentReadQuery(string todoItemId, string fileName)
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
    private async Task<Result<Stream>> HandleAsync(
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TodoItem> permissionEvaluator,
        IFileStorageProviderFactory providerFactory,
        CancellationToken cancellationToken)
    {
        var todoItemResult = await repository.FindOneResultAsync(BridgingIT.DevKit.Examples.DoFiesta.Domain.Model.TodoItemId.Create(this.TodoItemId), cancellationToken: cancellationToken)
            .EnsureAsync(
                async (todoItem, ct) => await permissionEvaluator.HasPermissionAsync(currentUserAccessor, todoItem.Id, Permission.Read, cancellationToken: ct),
                new UnauthorizedError(),
                cancellationToken);
        if (todoItemResult.IsFailure)
        {
            return Result<Stream>.Failure().WithErrors(todoItemResult.Errors);
        }

        IFileStorageProvider provider;
        try
        {
            provider = providerFactory.CreateProvider("attachments");
        }
        catch (Exception ex)
        {
            return Result<Stream>.Failure()
                .WithError(new ExceptionError(ex, "Unable to resolve the 'attachments' file storage provider."));
        }

        var pathResult = TodoItemAttachmentPathHelper.CreateFilePath(this.TodoItemId, this.FileName);
        if (pathResult.IsFailure)
        {
            return Result<Stream>.Failure().WithErrors(pathResult.Errors);
        }

        return await provider.ReadFileAsync(pathResult.Value, cancellationToken: cancellationToken);
    }
}
