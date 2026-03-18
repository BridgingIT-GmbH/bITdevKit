// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents the complete import configuration for a type.
/// </summary>
public sealed class ImportConfiguration
{
    /// <summary>
    /// Gets or sets the target type this configuration is for.
    /// </summary>
    public Type TargetType { get; set; }

    /// <summary>
    /// Gets or sets the sheet/section name to import from.
    /// </summary>
    public string SheetName { get; set; }

    /// <summary>
    /// Gets or sets the sheet index to import from (0-based). -1 means use SheetName.
    /// </summary>
    public int SheetIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the row index containing headers (0-based).
    /// </summary>
    public int HeaderRowIndex { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of rows to skip after the header.
    /// </summary>
    public int SkipRows { get; set; } = 0;

    /// <summary>
    /// Gets the column configurations.
    /// </summary>
    public List<ImportColumnConfiguration> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the behavior when validation fails.
    /// </summary>
    public ImportValidationBehavior ValidationBehavior { get; set; } = ImportValidationBehavior.CollectErrors;

    /// <summary>
    /// Gets or sets the maximum number of errors to collect before stopping.
    /// </summary>
    public int? MaxErrors { get; set; }

    /// <summary>
    /// Gets or sets the progress reporter for import operations.
    /// </summary>
    public IProgress<ImportProgressReport> Progress { get; set; }

    /// <summary>
    /// Gets or sets the payload compression or packaging settings.
    /// </summary>
    public PayloadCompressionOptions Compression { get; set; } = PayloadCompressionOptions.None;

    /// <summary>
    /// Gets or sets the factory function for creating target instances.
    /// </summary>
    public Func<object> Factory { get; set; }

    /// <summary>
    /// Gets or sets the culture to use for parsing.
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.InvariantCulture;

    internal ImportProgressTracker ProgressTracker { get; set; }
    internal object RowInterceptionExecutor { get; set; }
}
