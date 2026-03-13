// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Collections;
using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDocUnit = MigraDoc.DocumentObjectModel.Unit;
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
    private static readonly Lock fontResolverLock = new();
    private static bool fontResolverInitialized;

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
        this.EnsureFontResolution();

        var dataList = data.ToList();
        var columns = this.GetExportColumns(exportConfiguration);

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
        this.EnsureFontResolution();

        var dataSetsList = dataSets.ToList();
        var totalRows = 0;

        var document = this.CreateDocument();

        foreach (var (data, exportConfiguration) in dataSetsList)
        {
            var dataList = data.ToList();
            var columns = this.GetExportColumns(exportConfiguration);
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
        style.Font.Size = MigraDocUnit.FromPoint(this.configuration.BodyFontSize);

        return document;
    }

    private Section CreateSection(Document document)
    {
        var section = document.AddSection();

        // Configure page size
        var (width, height) = this.configuration.PageSize switch
        {
            PdfPageSize.A3 => (MigraDocUnit.FromMillimeter(297), MigraDocUnit.FromMillimeter(420)),
            PdfPageSize.Letter => (MigraDocUnit.FromInch(8.5), MigraDocUnit.FromInch(11)),
            PdfPageSize.Legal => (MigraDocUnit.FromInch(8.5), MigraDocUnit.FromInch(14)),
            _ => (MigraDocUnit.FromMillimeter(210), MigraDocUnit.FromMillimeter(297)) // A4
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
        var margin = MigraDocUnit.FromPoint(this.configuration.Margin);
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
            paragraph.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.HeaderFontSize + 4);
            paragraph.Format.Font.Bold = true;
            paragraph.Format.SpaceAfter = MigraDocUnit.FromPoint(5);
        }

        if (this.configuration.ShowGenerationDate)
        {
            var dateParagraph = section.AddParagraph($"Generated: {DateTime.Now.ToString(this.configuration.DateFormat)}");
            dateParagraph.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.HeaderFontSize - 1);
            dateParagraph.Format.Font.Color = Colors.Gray;
            dateParagraph.Format.SpaceAfter = MigraDocUnit.FromPoint(10);
        }
    }

    private void EnsureFontResolution()
    {
        lock (fontResolverLock)
        {
            if (fontResolverInitialized)
            {
                return;
            }

            try
            {
                var globalFontSettingsType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("PdfSharp.Fonts.GlobalFontSettings", throwOnError: false))
                    .FirstOrDefault(type => type is not null);

                var fontResolverProperty = globalFontSettingsType?.GetProperty("FontResolver", BindingFlags.Public | BindingFlags.Static);
                if (fontResolverProperty?.GetValue(null) is null)
                {
                    fontResolverProperty?.SetValue(null, new WindowsFontResolver());
                }

                fontResolverInitialized = true;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Could not initialize PDFsharp font resolution.");
            }
        }
    }

    private sealed class WindowsFontResolver : PdfSharp.Fonts.IFontResolver
    {
        private static readonly Dictionary<string, string> fileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial#Regular"] = "arial.ttf",
            ["Arial#Bold"] = "arialbd.ttf",
            ["Arial#Italic"] = "ariali.ttf",
            ["Arial#BoldItalic"] = "arialbi.ttf",
            ["Courier New#Regular"] = "cour.ttf",
            ["Courier New#Bold"] = "courbd.ttf",
            ["Courier New#Italic"] = "couri.ttf",
            ["Courier New#BoldItalic"] = "courbi.ttf",
            ["Helvetica#Regular"] = "arial.ttf",
            ["Helvetica#Bold"] = "arialbd.ttf",
            ["Helvetica#Italic"] = "ariali.ttf",
            ["Helvetica#BoldItalic"] = "arialbi.ttf",
            ["Times New Roman#Regular"] = "arial.ttf",
            ["Times New Roman#Bold"] = "arialbd.ttf",
            ["Times New Roman#Italic"] = "ariali.ttf",
            ["Times New Roman#BoldItalic"] = "arialbi.ttf"
        };

        public byte[] GetFont(string faceName)
        {
            if (!fileNames.TryGetValue(faceName, out var fileName))
            {
                fileName = "arial.ttf";
            }

            return File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", fileName));
        }

        public PdfSharp.Fonts.FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            familyName = string.IsNullOrWhiteSpace(familyName) ? "Arial" : familyName;
            var style = isBold && isItalic
                ? "BoldItalic"
                : isBold
                    ? "Bold"
                    : isItalic
                        ? "Italic"
                        : "Regular";

            var normalizedFamily = familyName switch
            {
                "Helvetica" => "Helvetica",
                "Courier" => "Courier New",
                _ => familyName
            };

            var faceName = $"{normalizedFamily}#{style}";
            if (!fileNames.ContainsKey(faceName))
            {
                faceName = $"Arial#{style}";
            }

            return new PdfSharp.Fonts.FontResolverInfo(faceName);
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
                tableColumn.Width = MigraDocUnit.FromPoint(column.Width);
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
            cell.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.BodyFontSize);
            cell.VerticalAlignment = MigraDocVerticalAlignment.Top;
            cell.Format.LeftIndent = MigraDocUnit.FromPoint(5);
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

                if (column.Converter is null && (column.PropertyInfo?.PropertyType?.SupportsStructuredValue() == true))
                {
                    value = this.FormatStructuredValue(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
                }

                var cell = row.Cells[i];
                cell.AddParagraph(this.FormatValue(value, column, exportConfiguration.Culture));
                cell.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.BodyFontSize);
                cell.VerticalAlignment = MigraDocVerticalAlignment.Top;
                cell.Format.LeftIndent = MigraDocUnit.FromPoint(5);

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
            paragraph.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.HeaderFontSize - 2);
            paragraph.Format.Font.Color = Colors.Gray;
        }

        if (this.configuration.ShowPageNumbers)
        {
            var paragraph = footer.AddParagraph();
            paragraph.Format.Alignment = ParagraphAlignment.Right;
            paragraph.Format.Font.Size = MigraDoc.DocumentObjectModel.Unit.FromPoint(this.configuration.HeaderFontSize - 2);
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

    private List<ColumnConfiguration> GetExportColumns(ExportConfiguration config)
    {
        return [.. config.Columns.Where(column => !column.Ignore && !this.ShouldIgnoreNestedColumn(column))];
    }

    private bool ShouldIgnoreNestedColumn(ColumnConfiguration column)
    {
        return !this.configuration.UseNesting
            && column.Converter is null
            && column.PropertyInfo?.PropertyType.SupportsStructuredValue() == true;
    }

    private string FormatStructuredValue(object value, HashSet<object> visited)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var type = value.GetType();
        if (!type.IsValueType && !visited.Add(value))
        {
            return string.Empty;
        }

        try
        {
            if (type.IsCollectionType())
            {
                var parts = new List<string>();
                foreach (var item in (IEnumerable)value)
                {
                    var formatted = this.FormatStructuredValue(item, visited);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        parts.Add(formatted);
                    }
                }

                return string.Join(" | ", parts);
            }

            var partsForObject = new List<string>();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(property => property.CanRead && property.GetIndexParameters().Length == 0))
            {
                var propertyValue = property.GetValue(value);
                if (propertyValue is null)
                {
                    continue;
                }

                if (property.PropertyType.SupportsStructuredValue())
                {
                    var nested = this.FormatStructuredValue(propertyValue, visited);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        partsForObject.Add($"{property.Name}: {nested}");
                    }

                    continue;
                }

                partsForObject.Add($"{property.Name}: {propertyValue}");
            }

            return string.Join(", ", partsForObject);
        }
        finally
        {
            if (!type.IsValueType)
            {
                visited.Remove(value);
            }
        }
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
