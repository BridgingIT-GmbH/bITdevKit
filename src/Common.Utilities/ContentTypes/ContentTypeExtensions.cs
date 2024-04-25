// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;

public static class ContentTypeExtensions
{
    public static ContentType FromMimeType(string mimeType, ContentType @default = ContentType.TXT)
    {
        if (string.IsNullOrEmpty(mimeType))
        {
            return @default;
        }

        foreach (var enumValue in Enum.GetValues(typeof(ContentType)))
        {
            Enum.TryParse(enumValue.ToString(), true, out ContentType contentType);
            var metaDataValue = contentType.GetAttributeValue<ContentTypeMetadateAttribute, string>(x => x.MimeType);
            if (metaDataValue is not null && metaDataValue.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
            {
                return contentType;
            }
        }

        return @default;
    }

    public static ContentType FromFileName(string fileName, ContentType @default = ContentType.TXT)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return @default;
        }

        return FromExtension(fileName.SliceFromLast("."), @default);
    }

    public static ContentType FromExtension(string extension, ContentType @default = ContentType.TXT)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return @default;
        }

        foreach (var enumValue in Enum.GetValues(typeof(ContentType)))
        {
            Enum.TryParse(enumValue.ToString(), true, out ContentType contentType);
            var metaDataValue = contentType.GetAttributeValue<ContentTypeMetadateAttribute, string>(x => x.FileExtension ?? enumValue.ToString());
            if (metaDataValue is not null)
            {
                if (metaDataValue.SafeEquals(extension)) // compare the attribute value with the extension or enum value
                {
                    return contentType;
                }
            }
            else
            {
                if (enumValue.ToString().SafeEquals(extension)) // compare the enum value with the extension
                {
                    return contentType;
                }
            }
        }

        return @default;
    }

    public static string MimeType(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return (metadata is not null) ? ((ContentTypeMetadateAttribute)metadata).MimeType : string.Empty;
    }

    public static string FileExtension(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return (metadata is not null && !string.IsNullOrEmpty(((ContentTypeMetadateAttribute)metadata).FileExtension))
            ? ((ContentTypeMetadateAttribute)metadata).FileExtension
            : contentType.ToString().ToLowerInvariant();
    }

    public static bool IsText(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return (metadata is not null) ? ((ContentTypeMetadateAttribute)metadata).IsText : true;
    }

    public static bool IsBinary(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return (metadata is not null) ? ((ContentTypeMetadateAttribute)metadata).IsBinary : false;
    }

    private static object GetMetadata(ContentType contentType)
    {
        var type = contentType.GetType();
        var info = type.GetMember(contentType.ToString());

        if ((info is not null) && (info.Length > 0))
        {
            var attrs = info[0].GetCustomAttributes(typeof(ContentTypeMetadateAttribute), false);
            if ((attrs is not null) && (attrs.Length > 0))
            {
                return attrs[0];
            }
        }

        return null;
    }
}