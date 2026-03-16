// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
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
    private static readonly System.Threading.Lock fontResolverLock = new();
    private static bool fontResolverInitialized;
    private static readonly PlatformFontResolver platformFontResolver = new();

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
            !string.IsNullOrEmpty(exportConfiguration.SheetName) ||
            this.configuration.ShowSummaryHeader)
        {
            this.AddHeader(section, exportConfiguration, dataList.Count);
        }

        this.AddContent(section, dataList, columns, exportConfiguration);

        // Footer
        this.AddFooter(section);

        // Render to stream
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputStream, false);

        return Task.FromResult(new ExportResult
        {
            BytesWritten = outputStream.Length,
            TotalRows = dataList.Count,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync<TSource>(
        IAsyncEnumerable<TSource> data,
        Stream outputStream,
        ExportConfiguration exportConfiguration,
        CancellationToken cancellationToken = default)
        where TSource : class
    {
        var dataList = await data.ToListAsync(cancellationToken);
        return await this.ExportAsync(dataList, outputStream, exportConfiguration, cancellationToken);
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
            this.AddHeader(section, exportConfiguration, dataList.Count);

            this.AddContent(section, dataList, columns, exportConfiguration);

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
            TotalRows = totalRows,
            Duration = TimeSpan.Zero,
            Format = this.Format
        });
    }

    /// <inheritdoc/>
    public async Task<ExportResult> ExportAsync(
        IEnumerable<(IAsyncEnumerable<object> Data, ExportConfiguration Configuration)> dataSets,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var materializedDataSets = new List<(IEnumerable<object> Data, ExportConfiguration Configuration)>();

        foreach (var (data, configuration) in dataSets)
        {
            materializedDataSets.Add((await data.ToListAsync(cancellationToken), configuration));
        }

        return await this.ExportAsync(materializedDataSets, outputStream, cancellationToken);
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

    private void AddHeader(Section section, ExportConfiguration exportConfiguration, int itemCount)
    {
        var title = this.configuration.HeaderText ?? exportConfiguration.SheetName ?? this.configuration.Title;
        if (!string.IsNullOrEmpty(title))
        {
            var paragraph = section.AddParagraph(title);
            paragraph.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.HeaderFontSize + 4);
            paragraph.Format.Font.Bold = true;
            paragraph.Format.SpaceAfter = MigraDocUnit.FromPoint(5);
        }

        if (this.configuration.ShowSummaryHeader)
        {
            var summaryParts = new List<string> { $"Items: {itemCount}" };
            if (this.configuration.ShowGenerationDate)
            {
                summaryParts.Insert(0, $"Generated: {DateTime.Now.ToString(this.configuration.DateFormat)}");
            }

            var summaryParagraph = section.AddParagraph(string.Join(" | ", summaryParts));
            summaryParagraph.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.HeaderFontSize - 1);
            summaryParagraph.Format.Font.Color = Colors.Gray;
            summaryParagraph.Format.SpaceAfter = MigraDocUnit.FromPoint(10);
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
                var fallbackFontResolverProperty = globalFontSettingsType?.GetProperty("FallbackFontResolver", BindingFlags.Public | BindingFlags.Static);
                var currentFontResolver = fontResolverProperty?.GetValue(null);
                var currentFallbackFontResolver = fallbackFontResolverProperty?.GetValue(null);

                if (this.configuration.FontResolver is not null && currentFontResolver is null)
                {
                    fontResolverProperty?.SetValue(null, this.configuration.FontResolver);
                }

                if (this.configuration.FallbackFontResolver is not null && currentFallbackFontResolver is null)
                {
                    fallbackFontResolverProperty?.SetValue(null, this.configuration.FallbackFontResolver);
                }

                if (fontResolverProperty?.GetValue(null) is null)
                {
                    fontResolverProperty?.SetValue(null, platformFontResolver);
                }

                if (fallbackFontResolverProperty?.GetValue(null) is null)
                {
                    fallbackFontResolverProperty?.SetValue(null, this.configuration.FallbackFontResolver ?? platformFontResolver);
                }

                fontResolverInitialized = true;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Could not initialize PDFsharp font resolution.");
            }
        }
    }

    private sealed class PlatformFontResolver : IFontResolver
    {
        private static readonly Dictionary<string, string[]> fontFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial#Regular"] = ["arial.ttf", "Arial.ttf", "LiberationSans-Regular.ttf", "DejaVuSans.ttf"],
            ["Arial#Bold"] = ["arialbd.ttf", "Arial Bold.ttf", "LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"],
            ["Arial#Italic"] = ["ariali.ttf", "Arial Italic.ttf", "LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf"],
            ["Arial#BoldItalic"] = ["arialbi.ttf", "Arial Bold Italic.ttf", "LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf"],
            ["Courier New#Regular"] = ["cour.ttf", "Courier New.ttf", "LiberationMono-Regular.ttf", "DejaVuSansMono.ttf"],
            ["Courier New#Bold"] = ["courbd.ttf", "Courier New Bold.ttf", "LiberationMono-Bold.ttf", "DejaVuSansMono-Bold.ttf"],
            ["Courier New#Italic"] = ["couri.ttf", "Courier New Italic.ttf", "LiberationMono-Italic.ttf", "DejaVuSansMono-Oblique.ttf"],
            ["Courier New#BoldItalic"] = ["courbi.ttf", "Courier New Bold Italic.ttf", "LiberationMono-BoldItalic.ttf", "DejaVuSansMono-BoldOblique.ttf"],
            ["Helvetica#Regular"] = ["arial.ttf", "Arial.ttf", "LiberationSans-Regular.ttf", "DejaVuSans.ttf"],
            ["Helvetica#Bold"] = ["arialbd.ttf", "Arial Bold.ttf", "LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"],
            ["Helvetica#Italic"] = ["ariali.ttf", "Arial Italic.ttf", "LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf"],
            ["Helvetica#BoldItalic"] = ["arialbi.ttf", "Arial Bold Italic.ttf", "LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf"],
            ["Times New Roman#Regular"] = ["times.ttf", "Times New Roman.ttf", "LiberationSerif-Regular.ttf", "DejaVuSerif.ttf"],
            ["Times New Roman#Bold"] = ["timesbd.ttf", "Times New Roman Bold.ttf", "LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf"],
            ["Times New Roman#Italic"] = ["timesi.ttf", "Times New Roman Italic.ttf", "LiberationSerif-Italic.ttf", "DejaVuSerif-Italic.ttf"],
            ["Times New Roman#BoldItalic"] = ["timesbi.ttf", "Times New Roman Bold Italic.ttf", "LiberationSerif-BoldItalic.ttf", "DejaVuSerif-BoldItalic.ttf"]
        };

        private static readonly string[] fontDirectories = GetFontDirectories();

        public byte[] GetFont(string faceName)
        {
            if (!fontFileNames.TryGetValue(faceName, out var fileNames))
            {
                fileNames = fontFileNames["Arial#Regular"];
            }

            foreach (var fileName in fileNames)
            {
                foreach (var directory in fontDirectories)
                {
                    var path = Path.Combine(directory, fileName);
                    if (File.Exists(path))
                    {
                        return File.ReadAllBytes(path);
                    }
                }
            }

            throw new FileNotFoundException($"Could not resolve font file for '{faceName}'.");
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
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
            if (!fontFileNames.ContainsKey(faceName))
            {
                faceName = $"Arial#{style}";
            }

            return new FontResolverInfo(faceName);
        }

        private static string[] GetFontDirectories()
        {
            var directories = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                directories.AddRange([
                    "/usr/share/fonts",
                    "/usr/local/share/fonts",
                    "/usr/share/fonts/truetype",
                    "/usr/share/fonts/truetype/dejavu",
                    "/usr/share/fonts/truetype/liberation2",
                    "/usr/share/fonts/opentype",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "fonts")
                ]);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                directories.AddRange([
                    "/System/Library/Fonts",
                    "/Library/Fonts",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts")
                ]);
            }

            return [.. directories.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)];
        }
    }

    private void AddContent<TSource>(
        Section section,
        List<TSource> dataList,
        List<ColumnConfiguration> columns,
        ExportConfiguration exportConfiguration)
        where TSource : class
    {
        switch (this.configuration.RenderTemplate)
        {
            case PdfRenderTemplate.Paragraph:
                this.AddContentParagraphs(section, dataList, columns, exportConfiguration);
                break;
            default:
                this.AddContentTable(section, dataList, columns, exportConfiguration);
                break;
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
                var value = this.GetColumnValue(item, column, exportConfiguration);

                var cell = row.Cells[i];
                cell.AddParagraph(this.FormatValue(value, column, exportConfiguration.Culture));
                cell.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.BodyFontSize);
                cell.VerticalAlignment = MigraDocVerticalAlignment.Top;
                cell.Format.LeftIndent = MigraDocUnit.FromPoint(5);
                cell.Format.Alignment = column.HorizontalAlignment switch
                {
                    HorizontalAlignment.Right => ParagraphAlignment.Right,
                    HorizontalAlignment.Center => ParagraphAlignment.Center,
                    _ => ParagraphAlignment.Left
                };
            }
        }
    }

    private void AddContentParagraphs<TSource>(
        Section section,
        List<TSource> dataList,
        List<ColumnConfiguration> columns,
        ExportConfiguration exportConfiguration)
        where TSource : class
    {
        for (var index = 0; index < dataList.Count; index++)
        {
            var item = dataList[index];

            if (index > 0)
            {
                this.AddParagraphSeparator(section);
            }

            var titleParagraph = section.AddParagraph($"Item {index + 1}");
            titleParagraph.Format.Font.Bold = true;
            titleParagraph.Format.Font.Size = MigraDocUnit.FromPoint(this.configuration.BodyFontSize + 2);
            titleParagraph.Format.SpaceBefore = MigraDocUnit.FromPoint(0);
            titleParagraph.Format.SpaceAfter = MigraDocUnit.FromPoint(4);

            foreach (var column in columns)
            {
            var rawValue = this.GetRawColumnValue(item, column, exportConfiguration);
            if (rawValue is null)
            {
                continue;
            }

            var propertyType = column.PropertyInfo?.PropertyType ?? rawValue.GetType();
            if (column.Converter is null && propertyType.IsCollectionType())
                {
                this.AddCollectionParagraphs(section, column, (IEnumerable)rawValue);
                continue;
                }

            if (column.Converter is null && propertyType.SupportsStructuredValue())
            {
                this.AddObjectParagraphs(section, column, rawValue);
                continue;
            }

            var value = this.GetColumnValue(item, column, exportConfiguration);
            var text = this.FormatValue(value, column, exportConfiguration.Culture);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

                var paragraph = section.AddParagraph();
                paragraph.Format.SpaceAfter = MigraDocUnit.FromPoint(2);
                paragraph.Format.LeftIndent = MigraDocUnit.FromPoint(8);

                var label = paragraph.AddFormattedText($"{column.HeaderName ?? column.PropertyName}: ");
                label.Bold = true;
                paragraph.AddText(text);
            }
        }
    }

    private void AddParagraphSeparator(Section section)
    {
        var separator = section.AddParagraph();
        separator.Format.SpaceBefore = MigraDocUnit.FromPoint(4);
        separator.Format.SpaceAfter = MigraDocUnit.FromPoint(6);
        separator.Format.Borders.Top.Width = 1;
        separator.Format.Borders.Top.Color = Colors.Gray;
    }

    private void AddObjectParagraphs(Section section, ColumnConfiguration column, object value)
    {
        var lines = this.GetStructuredValueLines(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
        if (lines.Count == 0)
        {
            return;
        }

        var labelParagraph = section.AddParagraph();
        labelParagraph.Format.SpaceAfter = MigraDocUnit.FromPoint(1);
        labelParagraph.Format.LeftIndent = MigraDocUnit.FromPoint(8);

        var label = labelParagraph.AddFormattedText($"{column.HeaderName ?? column.PropertyName}:");
        label.Bold = true;

        foreach (var line in lines)
        {
            var paragraph = section.AddParagraph();
            paragraph.Format.SpaceAfter = MigraDocUnit.FromPoint(1);
            paragraph.Format.LeftIndent = MigraDocUnit.FromPoint(18);
            paragraph.AddText(line);
        }
    }

    private void AddCollectionParagraphs(Section section, ColumnConfiguration column, IEnumerable values)
    {
        var items = values.Cast<object>()
            .Where(value => value is not null)
            .Select((value, index) => new
            {
                Index = index + 1,
                Lines = this.GetStructuredValueLines(value, new HashSet<object>(ReferenceEqualityComparer.Instance))
            })
            .Where(item => item.Lines.Count > 0)
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        var labelParagraph = section.AddParagraph();
        labelParagraph.Format.SpaceAfter = MigraDocUnit.FromPoint(1);
        labelParagraph.Format.LeftIndent = MigraDocUnit.FromPoint(8);

        var label = labelParagraph.AddFormattedText($"{column.HeaderName ?? column.PropertyName}:");
        label.Bold = true;

        foreach (var item in items)
        {
            var itemParagraph = section.AddParagraph();
            itemParagraph.Format.SpaceAfter = MigraDocUnit.FromPoint(1);
            itemParagraph.Format.LeftIndent = MigraDocUnit.FromPoint(18);

            var itemLabel = itemParagraph.AddFormattedText($"Item {item.Index}");
            itemLabel.Bold = true;

            foreach (var line in item.Lines)
            {
                var paragraph = section.AddParagraph();
                paragraph.Format.SpaceAfter = MigraDocUnit.FromPoint(1);
                paragraph.Format.LeftIndent = MigraDocUnit.FromPoint(28);
                paragraph.AddText($"• {line}");
            }
        }
    }

    private List<string> GetStructuredValueLines(object value, HashSet<object> visited)
    {
        var lines = new List<string>();
        if (value is null)
        {
            return lines;
        }

        var type = value.GetType();
        if (!type.IsValueType && !visited.Add(value))
        {
            return lines;
        }

        try
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(property => property.CanRead && property.GetIndexParameters().Length == 0))
            {
                var propertyValue = property.GetValue(value);
                if (propertyValue is null)
                {
                    continue;
                }

                if (property.PropertyType.IsCollectionType())
                {
                    var collectionItems = ((IEnumerable)propertyValue).Cast<object>()
                        .Where(item => item is not null)
                        .Select(item => this.FormatStructuredValue(item, visited))
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Select(text => $"{property.Name}: {text}");

                    lines.AddRange(collectionItems);
                    continue;
                }

                if (property.PropertyType.SupportsStructuredValue())
                {
                    var nestedLines = this.GetStructuredValueLines(propertyValue, visited);
                    foreach (var nestedLine in nestedLines)
                    {
                        lines.Add($"{property.Name} {nestedLine}");
                    }

                    continue;
                }

                lines.Add($"{property.Name}: {propertyValue}");
            }

            return lines;
        }
        finally
        {
            if (!type.IsValueType)
            {
                visited.Remove(value);
            }
        }
    }

    private object GetRawColumnValue(object item, ColumnConfiguration column, ExportConfiguration exportConfiguration)
    {
        var value = column.GetValue(item);

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

        return value;
    }

    private object GetColumnValue(object item, ColumnConfiguration column, ExportConfiguration exportConfiguration)
    {
        var value = this.GetRawColumnValue(item, column, exportConfiguration);

        if (column.Converter is null && (column.PropertyInfo?.PropertyType?.SupportsStructuredValue() == true))
        {
            value = this.FormatStructuredValue(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        return value;
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
