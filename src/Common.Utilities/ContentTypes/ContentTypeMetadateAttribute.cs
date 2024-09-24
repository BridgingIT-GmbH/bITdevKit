// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

[AttributeUsage(AttributeTargets.Field)]
public sealed class ContentTypeMetadateAttribute : Attribute
{
    public ContentTypeMetadateAttribute()
    {
        this.MimeType = "text/plain";
        this.IsText = true;
    }

    public string MimeType { get; set; }

    public string FileExtension { get; set; }

    public bool IsText { get; set; }

    public bool IsBinary
    {
        get => !this.IsText;

        set => this.IsText = !value;
    }
}