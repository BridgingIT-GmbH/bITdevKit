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
/// Uploads or replaces a todo-item attachment in the dedicated attachments storage location.
/// </summary>
/// <example>
/// <code>
/// await using var stream = File.OpenRead("notes.txt");
/// var result = await requester.SendAsync(
///     new TodoItemAttachmentUploadCommand(todoItemId, "notes.txt", stream),
///     cancellationToken);
/// </code>
/// </example>
[Command]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class TodoItemAttachmentUploadCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentUploadCommand"/> class.
    /// </summary>
    public TodoItemAttachmentUploadCommand()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoItemAttachmentUploadCommand"/> class.
    /// </summary>
    /// <param name="todoItemId">The todo item identifier.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <param name="stream">The content stream.</param>
    public TodoItemAttachmentUploadCommand(string todoItemId, string fileName, Stream stream)
    {
        this.TodoItemId = todoItemId;
        this.FileName = fileName;
        this.Stream = stream;
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

    /// <summary>
    /// Gets the content stream to store.
    /// </summary>
    [ValidateNotNull]
    public Stream Stream { get; private set; }

    [Handle]
    private async Task<Result<TodoItemAttachmentModel>> HandleAsync(
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
            return Result<TodoItemAttachmentModel>.Failure().WithErrors(todoItemResult.Errors);
        }

        IFileStorageProvider provider;
        try
        {
            provider = providerFactory.CreateProvider("attachments");
        }
        catch (Exception ex)
        {
            return Result<TodoItemAttachmentModel>.Failure()
                .WithError(new ExceptionError(ex, "Unable to resolve the 'attachments' file storage provider."));
        }

        var pathResult = TodoItemAttachmentPathHelper.CreateFilePath(this.TodoItemId, this.FileName);
        if (pathResult.IsFailure)
        {
            return Result<TodoItemAttachmentModel>.Failure().WithErrors(pathResult.Errors);
        }

        if (this.Stream.CanSeek)
        {
            this.Stream.Position = 0;
        }

        var writeResult = await provider.WriteFileAsync(pathResult.Value, this.Stream, cancellationToken: cancellationToken);
        if (writeResult.IsFailure)
        {
            return Result<TodoItemAttachmentModel>.Failure().WithErrors(writeResult.Errors);
        }

        var metadataResult = await provider.GetFileMetadataAsync(pathResult.Value, cancellationToken);
        if (metadataResult.IsFailure)
        {
            return Result<TodoItemAttachmentModel>.Failure().WithErrors(metadataResult.Errors);
        }

        return Result<TodoItemAttachmentModel>.Success(new TodoItemAttachmentModel
        {
            FileName = Path.GetFileName(pathResult.Value),
            Length = metadataResult.Value.Length,
            LastModified = metadataResult.Value.LastModified,
            ContentType = ContentTypeExtensions.FromFileName(pathResult.Value, ContentType.DEFAULT).MimeType()
        });
    }
}
