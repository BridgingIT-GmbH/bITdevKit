// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Client;

/// <summary>
/// Represents a file parameter for multipart/form-data requests.
/// Used by NSwag-generated API client code for file upload operations.
/// </summary>
/// <remarks>
/// NSwag 14.6.3 does not include a /GenerateFileParameters option,
/// so this class must be manually defined to support file upload endpoints.
/// </remarks>
public class FileParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileParameter"/> class.
    /// </summary>
    /// <param name="data">The file data stream.</param>
    public FileParameter(Stream data)
        : this(data, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileParameter"/> class.
    /// </summary>
    /// <param name="data">The file data stream.</param>
    /// <param name="fileName">The name of the file.</param>
    public FileParameter(Stream data, string fileName)
        : this(data, fileName, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileParameter"/> class.
    /// </summary>
    /// <param name="data">The file data stream.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="contentType">The MIME content type of the file.</param>
    public FileParameter(Stream data, string fileName, string contentType)
    {
        this.Data = data;
        this.FileName = fileName;
        this.ContentType = contentType;
    }

    /// <summary>
    /// Gets the file data stream.
    /// </summary>
    public Stream Data { get; }

    /// <summary>
    /// Gets the name of the file.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the MIME content type of the file.
    /// </summary>
    public string ContentType { get; }
}
