// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Text;

namespace BridgingIT.DevKit.Common;
/// <summary>
/// Represents rendered diagram output in a format-safe way.
/// </summary>
public sealed class DiagramRenderResult
{
    private readonly byte[] content;

    private DiagramRenderResult(DiagramRenderFormat format, string contentType, byte[] content, bool isText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(content);

        this.Format = format;
        this.ContentType = contentType;
        this.content = content;
        this.IsText = isText;
    }

    /// <summary>
    /// Gets the output format.
    /// </summary>
    public DiagramRenderFormat Format { get; }

    /// <summary>
    /// Gets the output content type.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the raw output bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Content => this.content;

    /// <summary>
    /// Gets a value indicating whether the payload represents text content.
    /// </summary>
    public bool IsText { get; }

    /// <summary>
    /// Creates a text render result.
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <param name="content">The rendered text.</param>
    /// <param name="contentType">The optional content type.</param>
    /// <returns>The render result.</returns>
    /// <example>
    /// <code>
    /// var result = DiagramRenderResult.FromText(
    ///     DiagramRenderFormat.Mermaid,
    ///     "stateDiagram-v2\n    Created",
    ///     "text/plain; charset=utf-8");
    /// </code>
    /// </example>
    public static DiagramRenderResult FromText(DiagramRenderFormat format, string content, string contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new DiagramRenderResult(format, contentType ?? GetDefaultTextContentType(format), Encoding.UTF8.GetBytes(content), true);
    }

    /// <summary>
    /// Creates a binary render result.
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <param name="content">The rendered bytes.</param>
    /// <param name="contentType">The binary content type.</param>
    /// <returns>The render result.</returns>
    /// <example>
    /// <code>
    /// var result = DiagramRenderResult.FromBinary(
    ///     DiagramRenderFormat.Bitmap,
    ///     imageBytes,
    ///     "image/png");
    /// </code>
    /// </example>
    public static DiagramRenderResult FromBinary(DiagramRenderFormat format, byte[] content, string contentType)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new DiagramRenderResult(format, contentType, [.. content], false);
    }

    /// <summary>
    /// Gets the rendered text using UTF-8 by default.
    /// </summary>
    /// <param name="encoding">The optional text encoding.</param>
    /// <returns>The rendered text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the payload is not text.</exception>
    /// <example>
    /// <code>
    /// var text = result.GetText();
    /// </code>
    /// </example>
    public string GetText(Encoding encoding = null)
    {
        if (!this.IsText)
        {
            throw new InvalidOperationException("The render result does not contain text content.");
        }

        return (encoding ?? Encoding.UTF8).GetString(this.content);
    }

    private static string GetDefaultTextContentType(DiagramRenderFormat format)
    {
        return format switch
        {
            DiagramRenderFormat.Svg => "image/svg+xml; charset=utf-8",
            _ => "text/plain; charset=utf-8",
        };
    }
}