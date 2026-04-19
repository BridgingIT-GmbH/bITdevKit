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
/// Lists all attachments that belong to a todo item.
/// </summary>
/// <example>
/// <code>
/// var result = await requester.SendAsync(new TodoItemAttachmentFindAllQuery(todoItemId), cancellationToken);
/// if (result.IsSuccess)
/// {
///     foreach (var attachment in result.Value)
///     {
///         Console.WriteLine($"{attachment.FileName} ({attachment.Length} bytes)");
///     }
/// }
/// </code>
/// </example>
[Query]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class TodoItemAttachmentFindAllQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentFindAllQuery"/> class.
    /// </summary>
    public TodoItemAttachmentFindAllQuery()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentFindAllQuery"/> class.
    /// </summary>
    /// <param name="todoItemId">The todo item identifier.</param>
    public TodoItemAttachmentFindAllQuery(string todoItemId)
    {
        this.TodoItemId = todoItemId;
    }

    /// <summary>
    /// Gets the todo item identifier.
    /// </summary>
    [ValidateNotEmpty("Todo item id is required.")]
    [ValidateValidGuid("Invalid guid.")]
    public string TodoItemId { get; private set; }

    [Handle]
    private async Task<Result<IEnumerable<TodoItemAttachmentModel>>> HandleAsync(
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
            return Result<IEnumerable<TodoItemAttachmentModel>>.Failure().WithErrors(todoItemResult.Errors);
        }

        IFileStorageProvider provider;
        try
        {
            provider = providerFactory.CreateProvider("attachments");
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<TodoItemAttachmentModel>>.Failure()
                .WithError(new ExceptionError(ex, "Unable to resolve the 'attachments' file storage provider."));
        }

        var folderPath = TodoItemAttachmentPathHelper.CreateFolderPath(this.TodoItemId);
        var filesResult = await provider.ListFilesAsync(folderPath, recursive: false, cancellationToken: cancellationToken);
        if (filesResult.IsFailure)
        {
            var isNotFound = filesResult.Errors?.Any(error =>
                error is NotFoundError ||
                error is FileSystemError fileSystemError &&
                fileSystemError.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)) == true;

            return isNotFound
                ? Result<IEnumerable<TodoItemAttachmentModel>>.Success([])
                : Result<IEnumerable<TodoItemAttachmentModel>>.Failure().WithErrors(filesResult.Errors);
        }

        var attachments = new List<TodoItemAttachmentModel>();
        foreach (var path in filesResult.Value.Files
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var metadataResult = await provider.GetFileMetadataAsync(path, cancellationToken);
            if (metadataResult.IsFailure)
            {
                return Result<IEnumerable<TodoItemAttachmentModel>>.Failure().WithErrors(metadataResult.Errors);
            }

            attachments.Add(new TodoItemAttachmentModel
            {
                FileName = Path.GetFileName(path),
                Length = metadataResult.Value.Length,
                LastModified = metadataResult.Value.LastModified,
                ContentType = ContentTypeExtensions.FromFileName(path, ContentType.DEFAULT).MimeType()
            });
        }

        return Result<IEnumerable<TodoItemAttachmentModel>>.Success(attachments);
    }
}
