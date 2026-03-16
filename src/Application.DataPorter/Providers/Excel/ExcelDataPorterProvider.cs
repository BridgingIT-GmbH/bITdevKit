// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Common;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Excel data porter provider using ClosedXML.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ExcelDataPorterProvider"/> class.
/// </remarks>
/// <param name="configuration">The Excel configuration.</param>
/// <param name="loggerFactory">The logger factory.</param>
public sealed class ExcelDataPorterProvider(
    ExcelConfiguration configuration = null,
    ILoggerFactory loggerFactory = null) : IDataExportProvider, IDataImportProvider
{
    private readonly ExcelConfiguration configuration = configuration ?? new ExcelConfiguration();
    private readonly ILogger<ExcelDataPorterProvider> logger = loggerFactory?.CreateLogger<ExcelDataPorterProvider>() ?? NullLogger<ExcelDataPorterProvider>.Instance;

    /// <inheritdoc/>
    public Format Format => Format.Excel;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedExtensions => [".xlsx", ".xlsm"];

    /// <inheritdoc/>
    public bool SupportsImport => true;

    /// <inheritdoc/>
    public bool SupportsExport => true;

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        using var workbook = new XLWorkbook();

        var sheetName = exportConfiguration.SheetName ?? typeof(TSource).Name;
        var worksheet = workbook.Worksheets.Add(sheetName);

        var rowsExported = 0;
        var currentRow = 1;

        // Write header rows (if configured)
        foreach (var headerRow in exportConfiguration.HeaderRows)
        {
            worksheet.Cell(currentRow, 1).Value = headerRow.Content;
            var headerRange = worksheet.Range(currentRow, 1, currentRow, exportConfiguration.Columns.Count);
            headerRange.Merge();

            if (headerRow.IsBold)
            {
                headerRange.Style.Font.SetBold(true);
            }

            if (headerRow.FontSize.HasValue)
            {
                headerRange.Style.Font.FontSize = headerRow.FontSize.Value;
            }

            currentRow++;
        }

        // Write column headers
        var headerRowIndex = currentRow;
        if (exportConfiguration.IncludeHeaders)
        {
            for (var col = 0; col < exportConfiguration.Columns.Count; col++)
            {
                var column = exportConfiguration.Columns[col];
                var cell = worksheet.Cell(currentRow, col + 1);
                cell.Value = column.HeaderName;
                cell.Style.Font.SetBold(true);
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;

                if (column.Width > 0)
                {
                    worksheet.Column(col + 1).Width = column.Width;
                }
            }

            currentRow++;
        }

        // Write data rows
        var dataList = data.ToList();
        foreach (var item in dataList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var col = 0; col < exportConfiguration.Columns.Count; col++)
            {
                var column = exportConfiguration.Columns[col];
                var value = column.GetValue(item);
                var cell = worksheet.Cell(currentRow, col + 1);

                this.SetCellValue(cell, value, column, exportConfiguration);
                this.ApplyStyles(cell, value, column);
            }

            currentRow++;
            rowsExported++;
        }

        // Write footer rows (if configured)
        foreach (var footerRow in exportConfiguration.FooterRows)
        {
            var footerContent = footerRow.ContentFactory?.Invoke(dataList.Cast<object>()) ?? footerRow.Content;
            worksheet.Cell(currentRow, 1).Value = footerContent;

            var footerRange = worksheet.Range(currentRow, 1, currentRow, exportConfiguration.Columns.Count);
            footerRange.Merge();

            if (footerRow.IsItalic)
            {
                footerRange.Style.Font.SetItalic(true);
            }

            if (footerRow.IsBold)
            {
                footerRange.Style.Font.SetBold(true);
            }

            currentRow++;
        }

        // Auto-fit columns if configured
        if (this.configuration.AutoFitColumns)
        {
            foreach (var col in worksheet.ColumnsUsed())
            {
                col.AdjustToContents();
                if (col.Width > this.configuration.MaxColumnWidth)
                {
                    col.Width = this.configuration.MaxColumnWidth;
                }
            }
        }

        // Add table formatting if configured
        if (this.configuration.UseTableFormatting && rowsExported > 0 && exportConfiguration.IncludeHeaders)
        {
            var dataRange = worksheet.Range(
                headerRowIndex, 1,
                headerRowIndex + rowsExported,
                exportConfiguration.Columns.Count);

            try
            {
                dataRange.CreateTable(this.configuration.DefaultTableStyleName);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to create table formatting, continuing without it");
            }
        }

        // Freeze header row if configured
        if (this.configuration.FreezeHeaderRow && exportConfiguration.IncludeHeaders)
        {
            worksheet.SheetView.FreezeRows(headerRowIndex);
        }

        await Task.Run(() => workbook.SaveAs(outputStream), cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = rowsExported,
            Duration = TimeSpan.Zero,
            Format = this.Format
        };
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        var totalRows = 0;

        foreach (var (data, exportConfiguration) in dataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sheetName = exportConfiguration.SheetName ?? "Sheet" + (workbook.Worksheets.Count + 1);
            var worksheet = workbook.Worksheets.Add(sheetName);

            var currentRow = 1;

            // Write column headers
            if (exportConfiguration.IncludeHeaders)
            {
                for (var col = 0; col < exportConfiguration.Columns.Count; col++)
                {
                    var column = exportConfiguration.Columns[col];
                    var cell = worksheet.Cell(currentRow, col + 1);
                    cell.Value = column.HeaderName;
                    cell.Style.Font.SetBold(true);
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                currentRow++;
            }

            // Write data rows
            foreach (var item in data)
            {
                for (var col = 0; col < exportConfiguration.Columns.Count; col++)
                {
                    var column = exportConfiguration.Columns[col];
                    var value = column.GetValue(item);
                    var cell = worksheet.Cell(currentRow, col + 1);

                    this.SetCellValue(cell, value, column, exportConfiguration);
                }

                currentRow++;
                totalRows++;
            }

            // Auto-fit columns
            if (this.configuration.AutoFitColumns)
            {
                foreach (var col in worksheet.ColumnsUsed())
                {
                    col.AdjustToContents();
                }
            }
        }

        await Task.Run(() => workbook.SaveAs(outputStream), cancellationToken);

        return new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = totalRows,
            Duration = TimeSpan.Zero,
            Format = this.Format
        };
    }

    /// <inheritdoc/>
    public async Task<ImportResult<TTarget>> ImportAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var results = new List<TTarget>();
        var errors = new List<ImportRowError>();
        var warnings = new List<string>();

        using var workbook = new XLWorkbook(inputStream);
        var worksheet = this.GetWorksheet(workbook, importConfiguration);

        if (worksheet is null)
        {
            return new ImportResult<TTarget>
            {
                Data = [],
                TotalRows = 0,
                SuccessfulRows = 0,
                FailedRows = 0,
                Duration = TimeSpan.Zero,
                Errors = [new ImportRowError
                {
                    RowNumber = 0,
                    Column = "N/A",
                    Message = "Worksheet not found",
                    Severity = ErrorSeverity.Critical
                }]
            };
        }

        // Read headers
        var headerRow = worksheet.Row(importConfiguration.HeaderRowIndex + 1);
        var columnMap = this.BuildColumnMap(headerRow, importConfiguration);

        var firstDataRow = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows + 2;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRow;
        var totalRows = 0;

        for (var rowNum = firstDataRow; rowNum <= lastRow; rowNum++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalRows++;

            var row = worksheet.Row(rowNum);

            // Skip empty rows
            if (row.IsEmpty())
            {
                continue;
            }

            try
            {
                var item = this.MapRow<TTarget>(row, columnMap, importConfiguration, rowNum, errors);

                if (item is not null)
                {
                    results.Add(item);
                }
                else if (importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNum,
                    Column = "N/A",
                    Message = ex.Message,
                    Severity = ErrorSeverity.Error
                });

                if (importConfiguration.ValidationBehavior == ImportValidationBehavior.StopImport)
                {
                    break;
                }
            }
        }

        return await Task.FromResult(new ImportResult<TTarget>
        {
            Data = results,
            TotalRows = totalRows,
            SuccessfulRows = results.Count,
            FailedRows = totalRows - results.Count,
            Duration = TimeSpan.Zero,
            Errors = errors,
            Warnings = warnings
        });
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Result<TTarget>> ImportStreamAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        XLWorkbook workbook;
        Result<TTarget>? loadError = null;
        try
        {
            workbook = new XLWorkbook(inputStream);
        }
        catch (Exception ex)
        {
            loadError = Result<TTarget>.Failure()
                .WithError(new ImportError($"Invalid Excel workbook: {ex.Message}", ex));
            workbook = null;
        }

        if (loadError.HasValue)
        {
            yield return loadError.Value;
            yield break;
        }

        using (workbook)
        {
            var worksheet = this.GetWorksheet(workbook, importConfiguration);

            if (worksheet is null)
            {
                yield return Result<TTarget>.Failure()
                    .WithError(new ImportError("Worksheet not found"));
                yield break;
            }

            // Read headers
            var headerRow = worksheet.Row(importConfiguration.HeaderRowIndex + 1);
            var columnMap = this.BuildColumnMap(headerRow, importConfiguration);

            var firstDataRow = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows + 2;
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRow;

            for (var rowNum = firstDataRow; rowNum <= lastRow; rowNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = worksheet.Row(rowNum);

                // Skip empty rows
                if (row.IsEmpty())
                {
                    continue;
                }

                var errors = new List<ImportRowError>();

                Result<TTarget>? result = null;
                try
                {
                    var item = this.MapRow<TTarget>(row, columnMap, importConfiguration, rowNum, errors);

                    if (item is not null)
                    {
                        result = Result<TTarget>.Success(item);
                    }
                    else if (errors.Count > 0)
                    {
                        result = Result<TTarget>.Failure()
                            .WithError(new ImportValidationError(
                                errors[0].RowNumber,
                                errors[0].Column,
                                errors[0].Message));
                    }
                }
                catch (Exception ex)
                {
                    result = Result<TTarget>.Failure()
                        .WithError(new ImportError($"Row {rowNum}: {ex.Message}", ex));
                }

                if (result.HasValue)
                {
                    yield return await Task.FromResult(result.Value);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ValidationResult> ValidateAsync<TTarget>(
        Stream inputStream,
        ImportConfiguration importConfiguration,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
    {
        var errors = new List<ImportRowError>();
        var warnings = new List<string>();

        using var workbook = new XLWorkbook(inputStream);
        var worksheet = this.GetWorksheet(workbook, importConfiguration);

        if (worksheet is null)
        {
            return ValidationResult.Failure(0, 0, [new ImportRowError
            {
                RowNumber = 0,
                Column = "N/A",
                Message = "Worksheet not found",
                Severity = ErrorSeverity.Critical
            }]);
        }

        // Read headers
        var headerRow = worksheet.Row(importConfiguration.HeaderRowIndex + 1);
        var columnMap = this.BuildColumnMap(headerRow, importConfiguration);

        var firstDataRow = importConfiguration.HeaderRowIndex + importConfiguration.SkipRows + 2;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRow;
        var totalRows = 0;
        var validRows = 0;

        for (var rowNum = firstDataRow; rowNum <= lastRow; rowNum++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = worksheet.Row(rowNum);
            if (row.IsEmpty())
            {
                continue;
            }

            totalRows++;
            var rowErrors = new List<ImportRowError>();

            foreach (var (columnConfig, cellIndex) in columnMap)
            {
                var cell = row.Cell(cellIndex);
                var rawValue = cell.GetValue<string>();

                // Validate required
                if (columnConfig.IsRequired && string.IsNullOrWhiteSpace(rawValue))
                {
                    rowErrors.Add(new ImportRowError
                    {
                        RowNumber = rowNum,
                        Column = columnConfig.SourceName,
                        Message = columnConfig.RequiredMessage ?? $"{columnConfig.PropertyName} is required",
                        RawValue = rawValue,
                        Severity = ErrorSeverity.Error
                    });
                }

                // Run custom validators
                foreach (var validator in columnConfig.Validators)
                {
                    if (!validator.Validate(rawValue))
                    {
                        rowErrors.Add(new ImportRowError
                        {
                            RowNumber = rowNum,
                            Column = columnConfig.SourceName,
                            Message = validator.ErrorMessage,
                            RawValue = rawValue,
                            Severity = ErrorSeverity.Error
                        });
                    }
                }
            }

            if (rowErrors.Count == 0)
            {
                validRows++;
            }
            else
            {
                errors.AddRange(rowErrors);
            }
        }

        return await Task.FromResult(errors.Count == 0
            ? ValidationResult.Success(totalRows)
            : ValidationResult.Failure(totalRows, validRows, errors));
    }

    private IXLWorksheet GetWorksheet(XLWorkbook workbook, ImportConfiguration config)
    {
        if (config.SheetIndex >= 0 && config.SheetIndex < workbook.Worksheets.Count)
        {
            return workbook.Worksheet(config.SheetIndex + 1);
        }

        if (!string.IsNullOrEmpty(config.SheetName))
        {
            return workbook.Worksheets.FirstOrDefault(
                ws => ws.Name.Equals(config.SheetName, StringComparison.OrdinalIgnoreCase));
        }

        return workbook.Worksheets.FirstOrDefault();
    }

    private Dictionary<ImportColumnConfiguration, int> BuildColumnMap(
        IXLRow headerRow,
        ImportConfiguration config)
    {
        var map = new Dictionary<ImportColumnConfiguration, int>();

        foreach (var column in config.Columns)
        {
            if (column.Ignore)
            {
                continue;
            }

            if (column.SourceIndex >= 0)
            {
                map[column] = column.SourceIndex + 1;
                continue;
            }

            // Find column by header name
            foreach (var cell in headerRow.CellsUsed())
            {
                var headerValue = cell.GetValue<string>();
                if (headerValue.Equals(column.SourceName, StringComparison.OrdinalIgnoreCase) ||
                    headerValue.Equals(column.PropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    map[column] = cell.Address.ColumnNumber;
                    break;
                }
            }
        }

        return map;
    }

    private TTarget MapRow<TTarget>(
        IXLRow row,
        Dictionary<ImportColumnConfiguration, int> columnMap,
        ImportConfiguration config,
        int rowNumber,
        List<ImportRowError> errors)
        where TTarget : class, new()
    {
        var item = config.Factory is not null
            ? (TTarget)config.Factory()
            : new TTarget();

        var hasErrors = false;

        foreach (var (columnConfig, cellIndex) in columnMap)
        {
            var cell = row.Cell(cellIndex);
            var rawValue = cell.GetValue<string>();

            try
            {
                // Validate required
                if (columnConfig.IsRequired && string.IsNullOrWhiteSpace(rawValue))
                {
                    hasErrors = true;
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Column = columnConfig.SourceName,
                        Message = columnConfig.RequiredMessage ?? $"{columnConfig.PropertyName} is required",
                        RawValue = rawValue,
                        Severity = ErrorSeverity.Error
                    });

                    continue;
                }

                // Run custom validators
                foreach (var validator in columnConfig.Validators)
                {
                    if (!validator.Validate(rawValue))
                    {
                        hasErrors = true;
                        errors.Add(new ImportRowError
                        {
                            RowNumber = rowNumber,
                            Column = columnConfig.SourceName,
                            Message = validator.ErrorMessage,
                            RawValue = rawValue,
                            Severity = ErrorSeverity.Error
                        });
                    }
                }

                if (hasErrors && config.ValidationBehavior == ImportValidationBehavior.SkipRow)
                {
                    continue;
                }

                // Convert and set value
                var convertedValue = columnConfig.ConvertValue(rawValue);
                columnConfig.SetValue(item, convertedValue);
            }
            catch (Exception ex)
            {
                hasErrors = true;
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Column = columnConfig.SourceName,
                    Message = $"Failed to convert value: {ex.Message}",
                    RawValue = rawValue,
                    Severity = ErrorSeverity.Error
                });
            }
        }

        return hasErrors && config.ValidationBehavior == ImportValidationBehavior.SkipRow
            ? null
            : item;
    }

    private void SetCellValue(
        IXLCell cell,
        object value,
        ColumnConfiguration column,
        ExportConfiguration config)
    {
        if (value is null)
        {
            cell.Value = column.NullValue ?? string.Empty;
            return;
        }

        // Apply converter if present
        if (column.Converter is not null)
        {
            var context = new ValueConversionContext
            {
                PropertyName = column.PropertyName,
                PropertyType = column.PropertyInfo?.PropertyType ?? typeof(object),
                EntityType = config.SourceType,
                Format = column.Format,
                Culture = config.Culture
            };

            value = column.Converter.ConvertToExport(value, context);
        }

        cell.Value = value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            decimal d => d,
            double d => d,
            int i => i,
            long l => l,
            bool b => b,
            _ => value.ToString()
        };

        if (!string.IsNullOrEmpty(column.Format))
        {
            cell.Style.NumberFormat.Format = column.Format;
        }
    }

    private void ApplyStyles(IXLCell cell, object value, ColumnConfiguration column)
    {
        // Apply alignment
        cell.Style.Alignment.Horizontal = column.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => XLAlignmentHorizontalValues.Left,
            HorizontalAlignment.Center => XLAlignmentHorizontalValues.Center,
            HorizontalAlignment.Right => XLAlignmentHorizontalValues.Right,
            _ => XLAlignmentHorizontalValues.Left
        };

        cell.Style.Alignment.Vertical = column.VerticalAlignment switch
        {
            VerticalAlignment.Top => XLAlignmentVerticalValues.Top,
            VerticalAlignment.Middle => XLAlignmentVerticalValues.Center,
            VerticalAlignment.Bottom => XLAlignmentVerticalValues.Bottom,
            _ => XLAlignmentVerticalValues.Center
        };

        // Apply conditional styles
        foreach (var conditionalStyle in column.ConditionalStyles)
        {
            if (conditionalStyle.Condition(value))
            {
                if (conditionalStyle.IsBold)
                {
                    cell.Style.Font.SetBold(true);
                }

                if (conditionalStyle.IsItalic)
                {
                    cell.Style.Font.SetItalic(true);
                }

                if (!string.IsNullOrEmpty(conditionalStyle.ForegroundColor))
                {
                    cell.Style.Font.FontColor = XLColor.FromHtml(conditionalStyle.ForegroundColor);
                }

                if (!string.IsNullOrEmpty(conditionalStyle.BackgroundColor))
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml(conditionalStyle.BackgroundColor);
                }
            }
        }
    }
}
