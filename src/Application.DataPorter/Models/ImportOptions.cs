// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Options for import operations.
/// </summary>
public sealed record ImportOptions
{
    /// <summary>
    /// Gets or sets the format to import from.
    /// </summary>
    public DataPorterFormat Format { get; init; } = DataPorterFormat.Excel;

    /// <summary>
    /// Gets or sets the name of a specific profile to use.
    /// </summary>
    public string ProfileName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to use attribute-based configuration.
    /// </summary>
    public bool UseAttributes { get; init; } = true;

    /// <summary>
    /// Gets or sets the culture to use for parsing.
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; init; } = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the sheet/section name to import from.
    /// </summary>
    public string SheetName { get; init; }

    /// <summary>
    /// Gets or sets the sheet index to import from (0-based).
    /// </summary>
    public int? SheetIndex { get; init; }

    /// <summary>
    /// Gets or sets the row index containing headers (0-based).
    /// </summary>
    public int HeaderRowIndex { get; init; } = 0;

    /// <summary>
    /// Gets or sets the number of rows to skip after the header.
    /// </summary>
    public int SkipRows { get; init; } = 0;

    /// <summary>
    /// Gets or sets the behavior when validation fails.
    /// </summary>
    public ImportValidationBehavior ValidationBehavior { get; init; } = ImportValidationBehavior.CollectErrors;

    /// <summary>
    /// Gets or sets the maximum number of errors to collect before stopping.
    /// </summary>
    public int? MaxErrors { get; init; }

    /// <summary>
    /// Gets or sets provider-specific options.
    /// </summary>
    public IReadOnlyDictionary<string, object> ProviderOptions { get; init; } = new Dictionary<string, object>();
}
