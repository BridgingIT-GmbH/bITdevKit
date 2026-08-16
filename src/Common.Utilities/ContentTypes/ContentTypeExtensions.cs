// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Provides metadata lookup and parsing helpers for <see cref="ContentType"/> values.
/// </summary>
public static class ContentTypeExtensions
{
    /// <summary>
    ///     Resolves a content type from its MIME type without regard to case.
    /// </summary>
    /// <param name="mimeType">The MIME type to resolve.</param>
    /// <param name="default">The value returned when no match exists.</param>
    /// <returns>The matching content type or the supplied default.</returns>
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

    /// <summary>
    ///     Resolves a content type from the suffix after the final period in a file name.
    /// </summary>
    /// <param name="fileName">The file name to inspect.</param>
    /// <param name="default">The value returned when no match exists.</param>
    /// <returns>The matching content type or the supplied default.</returns>
    public static ContentType FromFileName(string fileName, ContentType @default = ContentType.TXT)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return @default;
        }

        return FromExtension(fileName.SliceFromLast("."), @default);
    }

    /// <summary>
    ///     Resolves a content type from its configured extension or enum name.
    /// </summary>
    /// <param name="extension">The extension to resolve.</param>
    /// <param name="default">The value returned when no match exists.</param>
    /// <returns>The matching content type or the supplied default.</returns>
    public static ContentType FromExtension(string extension, ContentType @default = ContentType.TXT)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return @default;
        }

        foreach (var enumValue in Enum.GetValues(typeof(ContentType)))
        {
            Enum.TryParse(enumValue.ToString(), true, out ContentType contentType);
            var metaDataValue =
                contentType.GetAttributeValue<ContentTypeMetadateAttribute, string>(x =>
                    x.FileExtension ?? enumValue.ToString());
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

    /// <summary>Gets the configured MIME type for a content type.</summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The configured MIME type, or an empty string when metadata is absent.</returns>
    public static string MimeType(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return metadata is not null ? ((ContentTypeMetadateAttribute)metadata).MimeType : string.Empty;
    }

    /// <summary>Gets the configured file extension or the lower-case enum name when none is configured.</summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The file extension.</returns>
    public static string FileExtension(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return metadata is not null && !string.IsNullOrEmpty(((ContentTypeMetadateAttribute)metadata).FileExtension)
            ? ((ContentTypeMetadateAttribute)metadata).FileExtension
            : contentType.ToString().ToLowerInvariant();
    }

    /// <summary>Determines whether a content type represents text.</summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The configured text flag, or <see langword="true"/> when metadata is absent.</returns>
    public static bool IsText(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return metadata is not null ? ((ContentTypeMetadateAttribute)metadata).IsText : true;
    }

    /// <summary>Determines whether a content type represents binary data.</summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The configured binary flag, or <see langword="false"/> when metadata is absent.</returns>
    public static bool IsBinary(this ContentType contentType)
    {
        var metadata = GetMetadata(contentType);

        return metadata is not null ? ((ContentTypeMetadateAttribute)metadata).IsBinary : false;
    }

    private static object GetMetadata(ContentType contentType)
    {
        var type = contentType.GetType();
        var info = type.GetMember(contentType.ToString());

        if (info is not null && info.Length > 0)
        {
            var attrs = info[0].GetCustomAttributes(typeof(ContentTypeMetadateAttribute), false);
            if (attrs is not null && attrs.Length > 0)
            {
                return attrs[0];
            }
        }

        return null;
    }
}
