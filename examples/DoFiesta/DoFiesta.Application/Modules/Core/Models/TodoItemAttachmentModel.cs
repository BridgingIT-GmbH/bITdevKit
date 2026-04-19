// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

/// <summary>
/// Represents a single todo-item attachment exposed through the todo API.
/// </summary>
/// <example>
/// <code>
/// var attachment = new TodoItemAttachmentModel
/// {
///     FileName = "notes.txt",
///     Length = 128,
///     ContentType = "text/plain",
///     LastModified = DateTime.UtcNow
/// };
/// </code>
/// </example>
public class TodoItemAttachmentModel
{
    /// <summary>
    /// Gets or sets the attachment file name within the todo item's attachment folder.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the attachment file size in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets the attachment content type inferred from the file name.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the last modification.
    /// </summary>
    public DateTime? LastModified { get; set; }
}
