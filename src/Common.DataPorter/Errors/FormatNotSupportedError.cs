// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Error indicating that the requested format is not supported.
/// </summary>
public sealed class FormatNotSupportedError : DataPorterError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormatNotSupportedError"/> class.
    /// </summary>
    public FormatNotSupportedError()
        : base("The requested format is not supported.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatNotSupportedError"/> class.
    /// </summary>
    /// <param name="format">The unsupported format.</param>
    /// <param name="availableFormats">The available formats.</param>
    public FormatNotSupportedError(string format, IEnumerable<string> availableFormats = null)
        : base($"Format '{format}' is not supported." +
               (availableFormats is not null
                   ? $" Available formats: {string.Join(", ", availableFormats)}"
                   : string.Empty))
    {
        this.Format = format;
        this.AvailableFormats = availableFormats?.ToList() ?? [];
    }

    /// <summary>
    /// Gets the unsupported format.
    /// </summary>
    public string Format { get; }

    /// <summary>
    /// Gets the available formats.
    /// </summary>
    public IReadOnlyList<string> AvailableFormats { get; } = [];
}
