// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

/// <summary>
/// PDF data porter provider using QuestPDF (export only).
/// </summary>
public sealed class PdfDataPorterProvider : IDataExportProvider
{
    private readonly PdfConfiguration configuration;
    private readonly ILogger<PdfDataPorterProvider> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDataPorterProvider"/> class.
    /// </summary>
    /// <param name="configuration">The PDF configuration.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public PdfDataPorterProvider(
        PdfConfiguration configuration = null,
        ILoggerFactory loggerFactory = null)
    {
        this.configuration = configuration ?? new PdfConfiguration();
        this.logger = loggerFactory?.CreateLogger<PdfDataPorterProvider>() ?? NullLogger<PdfDataPorterProvider>.Instance;

        // Configure QuestPDF license for community use
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc/>
    public string Format => "pdf";

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

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                this.ConfigurePage(page);

                // Header
                if (!string.IsNullOrEmpty(this.configuration.HeaderText) ||
                    !string.IsNullOrEmpty(exportConfiguration.SheetName))
                {
                    page.Header().Element(c => this.ComposeHeader(c, exportConfiguration));
                }

                // Content
                page.Content().Element(c => this.ComposeContent(c, dataList, columns, exportConfiguration));

                // Footer
                page.Footer().Element(this.ComposeFooter);
            });
        });

        document.GeneratePdf(outputStream);

        return Task.FromResult(new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = dataList.Count,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }

    /// <inheritdoc/>
    public Task<ExportResult> ExportMultipleAsync(
        IEnumerable<(IEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var dataSetsList = dataSets.ToList();
        var totalRows = 0;

        var document = Document.Create(container =>
        {
            foreach (var (data, exportConfiguration) in dataSetsList)
            {
                var dataList = data.ToList();
                var columns = exportConfiguration.Columns.ToList();
                totalRows += dataList.Count;

                container.Page(page =>
                {
                    this.ConfigurePage(page);

                    // Header with sheet name
                    page.Header().Element(c => this.ComposeHeader(c, exportConfiguration));

                    // Content
                    page.Content().Element(c => this.ComposeContent(c, dataList, columns, exportConfiguration));

                    // Footer
                    page.Footer().Element(this.ComposeFooter);
                });
            }
        });

        document.GeneratePdf(outputStream);

        return Task.FromResult(new ExportResult
        {
            BytesWritten = outputStream.Length,
            RowsExported = totalRows,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }

    private void ConfigurePage(PageDescriptor page)
    {
        var pageSize = this.configuration.PageSize switch
        {
            PdfPageSize.A3 => PageSizes.A3,
            PdfPageSize.Letter => PageSizes.Letter,
            PdfPageSize.Legal => PageSizes.Legal,
            _ => PageSizes.A4
        };

        if (this.configuration.Orientation == PdfPageOrientation.Landscape)
        {
            pageSize = pageSize.Landscape();
        }

        page.Size(pageSize);
        page.Margin(this.configuration.Margin, Unit.Point);
        page.DefaultTextStyle(x => x.FontSize(this.configuration.BodyFontSize));
    }

    private void ComposeHeader(IContainer container, ExportConfiguration exportConfiguration)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                var title = this.configuration.HeaderText ?? exportConfiguration.SheetName ?? this.configuration.Title;
                if (!string.IsNullOrEmpty(title))
                {
                    column.Item()
                        .Text(title)
                        .FontSize(this.configuration.HeaderFontSize + 4)
                        .Bold();
                }

                if (this.configuration.ShowGenerationDate)
                {
                    column.Item()
                        .Text($"Generated: {DateTime.Now.ToString(this.configuration.DateFormat)}")
                        .FontSize(this.configuration.HeaderFontSize - 1)
                        .FontColor(Colors.Grey.Medium);
                }
            });
        });

        container.PaddingBottom(10);
    }

    private void ComposeContent<TSource>(
        IContainer container,
        List<TSource> dataList,
        List<ColumnConfiguration> columns,
        ExportConfiguration exportConfiguration)
        where TSource : class
    {
        container.Table(table =>
        {
            // Define columns
            table.ColumnsDefinition(def =>
            {
                foreach (var column in columns)
                {
                    if (column.Width > 0)
                    {
                        def.ConstantColumn((float)column.Width, Unit.Point);
                    }
                    else
                    {
                        def.RelativeColumn();
                    }
                }
            });

            // Header row
            table.Header(header =>
            {
                foreach (var column in columns)
                {
                    header.Cell()
                        .Background(this.ParseColor(this.configuration.TableHeaderBackgroundColor))
                        .Padding(5)
                        .Text(column.HeaderName ?? column.PropertyName)
                        .FontColor(this.ParseColor(this.configuration.TableHeaderTextColor))
                        .Bold()
                        .FontSize(this.configuration.BodyFontSize);
                }
            });

            // Data rows
            var rowIndex = 0;
            foreach (var item in dataList)
            {
                var isAlternate = rowIndex % 2 == 1;
                rowIndex++;

                foreach (var column in columns)
                {
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

                    var cell = table.Cell();

                    if (this.configuration.UseAlternatingRowColors && isAlternate)
                    {
                        cell.Background(this.ParseColor(this.configuration.AlternateRowBackgroundColor));
                    }

                    // Apply alignment
                    if (column.HorizontalAlignment == HorizontalAlignment.Right)
                    {
                        cell.AlignRight();
                    }
                    else if (column.HorizontalAlignment == HorizontalAlignment.Center)
                    {
                        cell.AlignCenter();
                    }

                    cell.Padding(5)
                        .Text(this.FormatValue(value, column, exportConfiguration.Culture))
                        .FontSize(this.configuration.BodyFontSize);
                }
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            if (!string.IsNullOrEmpty(this.configuration.FooterText))
            {
                row.RelativeItem()
                    .AlignLeft()
                    .Text(this.configuration.FooterText)
                    .FontSize(this.configuration.HeaderFontSize - 2)
                    .FontColor(Colors.Grey.Medium);
            }
            else
            {
                row.RelativeItem();
            }

            if (this.configuration.ShowPageNumbers)
            {
                row.RelativeItem()
                    .AlignRight()
                    .Text(text =>
                    {
                        text.Span("Page ").FontSize(this.configuration.HeaderFontSize - 2).FontColor(Colors.Grey.Medium);
                        text.CurrentPageNumber().FontSize(this.configuration.HeaderFontSize - 2).FontColor(Colors.Grey.Medium);
                        text.Span(" of ").FontSize(this.configuration.HeaderFontSize - 2).FontColor(Colors.Grey.Medium);
                        text.TotalPages().FontSize(this.configuration.HeaderFontSize - 2).FontColor(Colors.Grey.Medium);
                    });
            }
        });
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

        return Color.FromRGB(r, g, b);
    }
}
