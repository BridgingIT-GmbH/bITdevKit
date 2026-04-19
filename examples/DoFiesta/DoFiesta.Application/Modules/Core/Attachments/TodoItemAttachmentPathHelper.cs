// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using System.IO;
using BridgingIT.DevKit.Common;

internal static class TodoItemAttachmentPathHelper
{
    public static string CreateFolderPath(string todoItemId)
        => Guid.Parse(todoItemId).ToString("D");

    public static Result<string> NormalizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result<string>.Failure()
                .WithError(new ValidationError("Attachment file name is required.", nameof(fileName), fileName));
        }

        var normalizedFileName = fileName.Trim();
        if (normalizedFileName is "." or "..")
        {
            return Result<string>.Failure()
                .WithError(new ValidationError("Attachment file name is invalid.", nameof(fileName), fileName));
        }

        if (!string.Equals(normalizedFileName, Path.GetFileName(normalizedFileName), StringComparison.Ordinal))
        {
            return Result<string>.Failure()
                .WithError(new ValidationError("Attachment file names must not contain path segments.", nameof(fileName), fileName));
        }

        if (normalizedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return Result<string>.Failure()
                .WithError(new ValidationError("Attachment file name contains invalid characters.", nameof(fileName), fileName));
        }

        return Result<string>.Success(normalizedFileName);
    }

    public static Result<string> CreateFilePath(string todoItemId, string fileName)
        => NormalizeFileName(fileName)
            .Map(normalizedFileName => $"{CreateFolderPath(todoItemId)}/{normalizedFileName}");
}
