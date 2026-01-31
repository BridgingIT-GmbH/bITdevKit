// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Represents a data format provider for export/import operations.
/// </summary>
public interface IDataPorterProvider
{
    /// <summary>
    /// Gets the unique format identifier.
    /// </summary>
    DataPorterFormat Format { get; }

    /// <summary>
    /// Gets the supported file extensions for this provider.
    /// </summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>
    /// Gets a value indicating whether this provider supports import operations.
    /// </summary>
    bool SupportsImport { get; }

    /// <summary>
    /// Gets a value indicating whether this provider supports export operations.
    /// </summary>
    bool SupportsExport { get; }

    /// <summary>
    /// Gets a value indicating whether this provider supports streaming import for large files.
    /// </summary>
    bool SupportsStreaming { get; }
}
