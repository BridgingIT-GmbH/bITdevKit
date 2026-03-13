// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDocVerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment;

/// <summary>
/// PDF data porter provider using MigraDoc (export only).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PdfDataPorterProvider"/> class.
/// </remarks>
/// <param name="configuration">The PDF configuration.</param>
/// <param name="loggerFactory">The logger factory.</param>
public sealed class PdfDataPorterProvider(
    PdfConfiguration configuration = null,
    ILoggerFactory loggerFactory = null) : IDataExportProvider
{
    private readonly PdfConfiguration configuration = configuration ?? new PdfConfiguration();
    private readonly ILogger<PdfDataPorterProvider> logger = loggerFactory?.CreateLogger<PdfDataPorterProvider>() ?? NullLogger<PdfDataPorterProvider>.Instance;

    /// <inheritdoc/>
    public Format Format => Format.Pdf;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> SupportedExtensions => [".pdf"];

    /// <inheritdoc/>
    public bool SupportsImport => false;

    /// <inheritdoc/>
    public bool SupportsExport => true;

    /// <inheritdoc/>
    public bool SupportsStreaming => false;

    /// <inheritdoc/>
    public Task<ExportResult> ExportAsync<TSource>(
        IEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var dataList = data.ToList();
        var columns = exportConfiguration.Columns.ToList();

        var document = this.CreateDocument();
        var section = this.CreateSection(document);

        // Header
        if (!string.IsNullOrEmpty(this.configuration.HeaderText) ||
            !string.IsNullOrEmpty(exportConfiguration.SheetName))
        {
            this.AddHeader(section, exportConfiguration);
        }

        // Content table
        this.AddContentTable(section, dataList, columns, exportConfiguration);

        // Footer
        this.AddFooter(section);

        // Render to stream
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputStream, false);

        return Task.FromResult(new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = dataList.Count,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }

    /// <inheritdoc/>
    public Task<ExportResult> ExportAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var dataSetsList = dataSets.ToList();
        var totalRows = 0;

        var document = this.CreateDocument();

        foreach (var (data, exportConfiguration) in dataSetsList)
        {
            var dataList = data.ToList();
            var columns = exportConfiguration.Columns.ToList();
            totalRows += dataList.Count;

            var section = this.CreateSection(document);

            // Header with sheet name
            this.AddHeader(section, exportConfiguration);

            // Content table
            this.AddContentTable(section, dataList, columns, exportConfiguration);

            // Footer
            this.AddFooter(section);
        }

        // Render to stream
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputStream, false);

        return Task.FromResult(new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = totalRows,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }

    private Document CreateDocument()
    {
        var document = new Document();

        // Set document info
        if (!string.IsNullOrEmpty(this.configuration.Title))
        {
            document.Info.Title = this.configuration.Title;
        }

        if (!string.IsNullOrEmpty(this.configuration.Author))
        {
            document.Info.Author = this.configuration.Author;
        }

        if (!string.IsNullOrEmpty(this.configuration.Subject))
        {
            document.Info.Subject = this.configuration.Subject;
        }

        // Define default style
        var style = document.Styles[StyleNames.Normal];
        style.Font.Name = this.configuration.FontFamily;
        style.Font.Size = Unit.FromPoint(this.configuration.BodyFontSize);

        return document;
    }

    private Section CreateSection(Document document)
    {
        var section = document.AddSection();

        // Configure page size
        var (width, height) = this.configuration.PageSize switch
        {
            PdfPageSize.A3 => (Unit.FromMillimeter(297), Unit.FromMillimeter(420)),
            PdfPageSize.Letter => (Unit.FromInch(8.5), Unit.FromInch(11)),
            PdfPageSize.Legal => (Unit.FromInch(8.5), Unit.FromInch(14)),
            _ => (Unit.FromMillimeter(210), Unit.FromMillimeter(297)) // A4
        };

        if (this.configuration.Orientation == PdfPageOrientation.Landscape)
        {
            section.PageSetup.PageWidth = height;
            section.PageSetup.PageHeight = width;
        }
        else
        {
            section.PageSetup.PageWidth = width;
            section.PageSetup.PageHeight = height;
        }

        // Set margins
        var margin = Unit.FromPoint(this.configuration.Margin);
        section.PageSetup.TopMargin = margin;
        section.PageSetup.BottomMargin = margin;
        section.PageSetup.LeftMargin = margin;
        section.PageSetup.RightMargin = margin;

        return section;
    }

    private void AddHeader(Section section, ExportConfiguration exportConfiguration)
    {
        var title = this.configuration.HeaderText ?? exportConfiguration.SheetName ?? this.configuration.Title;
        if (!string.IsNullOrEmpty(title))
        {
            var paragraph = section.AddParagraph(title);
            paragraph.Format.Font.Size = Unit.FromPoint(this.configuration.HeaderFontSize + 4);
            paragraph.Format.Font.Bold = true;
            paragraph.Format.SpaceAfter = Unit.FromPoint(5);
        }

        if (this.configuration.ShowGenerationDate)
        {
            var dateParagraph = section.AddParagraph($"Generated: {DateTime.Now.ToString(this.configuration.DateFormat)}");
            dateParagraph.Format.Font.Size = Unit.FromPoint(this.configuration.HeaderFontSize - 1);
            dateParagraph.Format.Font.Color = Colors.Gray;
            dateParagraph.Format.SpaceAfter = Unit.FromPoint(10);
        }
    }

    private void AddContentTable<TSource>(
        Section section,
        List<TSource> dataList,
        List<ColumnConfiguration> columns,
        ExportConfiguration exportConfiguration)
        where TSource : class
    {
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.LightGray;

        // Define columns
        foreach (var column in columns)
        {
            var tableColumn = table.AddColumn();
            if (column.Width > 0)
            {
                tableColumn.Width = Unit.FromPoint(column.Width);
            }
            else
            {
                // Calculate approximate width based on available space
                var availableWidth = section.PageSetup.PageWidth - section.PageSetup.LeftMargin - section.PageSetup.RightMargin;
                tableColumn.Width = availableWidth / columns.Count;
            }
        }

        // Header row
        var headerRow = table.AddRow();
        headerRow.HeadingFormat = true;
        headerRow.Format.Font.Bold = true;
        headerRow.Format.Font.Color = this.ParseColor(this.configuration.TableHeaderTextColor);
        headerRow.Shading.Color = this.ParseColor(this.configuration.TableHeaderBackgroundColor);

        for (var i = 0; i < columns.Count; i++)
        {
            var cell = headerRow.Cells[i];
            cell.AddParagraph(columns[i].HeaderName ?? columns[i].PropertyName);
            cell.Format.Font.Size = Unit.FromPoint(this.configuration.BodyFontSize);
            cell.VerticalAlignment = MigraDocVerticalAlignment.Top;
            cell.Format.LeftIndent = Unit.FromPoint(5);
        }

        // Data rows
        var rowIndex = 0;
        foreach (var item in dataList)
        {
            var isAlternate = rowIndex % 2 == 1;
            rowIndex++;

            var row = table.AddRow();

            if (this.configuration.UseAlternatingRowColors && isAlternate)
            {
                row.Shading.Color = this.ParseColor(this.configuration.AlternateRowBackgroundColor);
            }

            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var value = column.GetValue(item);

                // Apply converter if present
                if (column.Converter is not null)
                {
                    var context = new ValueConversionContext
                    {
                        PropertyName = column.PropertyName,
                        PropertyType = column.PropertyInfo?.PropertyType ?? typeof(object),
                        EntityType = exportConfiguration.SourceType,
                        Format = column.Format,
                        Culture = exportConfiguration.Culture
                    };

                    value = column.Converter.ConvertToExport(value, context);
                }

                var cell = row.Cells[i];
                cell.AddParagraph(this.FormatValue(value, column, exportConfiguration.Culture));
                cell.Format.Font.Size = Unit.FromPoint(this.configuration.BodyFontSize);
                cell.VerticalAlignment = MigraDocVerticalAlignment.Top;
                cell.Format.LeftIndent = Unit.FromPoint(5);

                // Apply alignment
                cell.Format.Alignment = column.HorizontalAlignment switch
                {
                    HorizontalAlignment.Right => ParagraphAlignment.Right,
                    HorizontalAlignment.Center => ParagraphAlignment.Center,
                    _ => ParagraphAlignment.Left
                };
            }
        }
    }

    private void AddFooter(Section section)
    {
        var footer = section.Footers.Primary;

        if (!string.IsNullOrEmpty(this.configuration.FooterText))
        {
            var paragraph = footer.AddParagraph(this.configuration.FooterText);
            paragraph.Format.Font.Size = Unit.FromPoint(this.configuration.HeaderFontSize - 2);
            paragraph.Format.Font.Color = Colors.Gray;
        }

        if (this.configuration.ShowPageNumbers)
        {
            var paragraph = footer.AddParagraph();
            paragraph.Format.Alignment = ParagraphAlignment.Right;
            paragraph.Format.Font.Size = Unit.FromPoint(this.configuration.HeaderFontSize - 2);
            paragraph.Format.Font.Color = Colors.Gray;
            paragraph.AddText("Page ");
            paragraph.AddPageField();
            paragraph.AddText(" of ");
            paragraph.AddNumPagesField();
        }
    }

    private string FormatValue(object value, ColumnConfiguration column, System.Globalization.CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(column.Format))
        {
            return value switch
            {
                IFormattable formattable => formattable.ToString(column.Format, culture),
                _ => value.ToString() ?? string.Empty
            };
        }

        return value switch
        {
            DateTime dt => dt.ToString(this.configuration.DateFormat),
            DateTimeOffset dto => dto.ToString(this.configuration.DateFormat),
            bool b => b ? "Yes" : "No",
            _ => value.ToString() ?? string.Empty
        };
    }

    private Color ParseColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor))
        {
            return Colors.White;
        }

        hexColor = hexColor.TrimStart('#');

        if (hexColor.Length != 6)
        {
            return Colors.White;
        }

        var r = Convert.ToByte(hexColor[..2], 16);
        var g = Convert.ToByte(hexColor.Substring(2, 2), 16);
        var b = Convert.ToByte(hexColor.Substring(4, 2), 16);

        return new Color(r, g, b);
    }
}
