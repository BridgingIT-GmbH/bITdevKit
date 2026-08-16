// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Associates MIME, extension, and text/binary metadata with a <see cref="ContentType"/> field.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ContentTypeMetadateAttribute : Attribute
{
    /// <summary>
    ///     Initializes metadata with the <c>text/plain</c> MIME type and text classification.
    /// </summary>
    public ContentTypeMetadateAttribute()
    {
        this.MimeType = "text/plain";
        this.IsText = true;
    }

    /// <summary>Gets or sets the MIME type.</summary>
    public string MimeType { get; set; }

    /// <summary>Gets or sets the preferred file extension.</summary>
    public string FileExtension { get; set; }

    /// <summary>Gets or sets whether the content is text.</summary>
    public bool IsText { get; set; }

    /// <summary>Gets or sets whether the content is binary, as the inverse of <see cref="IsText"/>.</summary>
    public bool IsBinary
    {
        get => !this.IsText;

        set => this.IsText = !value;
    }
}
