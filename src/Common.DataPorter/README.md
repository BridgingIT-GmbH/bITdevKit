# DataPorter

A flexible, extensible data export/import framework for .NET supporting multiple file formats with both profile-based and attribute-based configuration.

## Features

- **Multiple Format Support**: Excel (.xlsx), CSV, JSON, XML, and PDF (export only)
- **Dual Configuration Approaches**: Profile-based (similar to AutoMapper) or attribute-based
- **Streaming Support**: Memory-efficient import for large files using `IAsyncEnumerable`
- **Validation**: Built-in validation with customizable rules and error handling
- **Value Converters**: Transform values during import/export with custom converters
- **Result Pattern**: Integrated with bIT.bITdevKit Result pattern for consistent error handling
- **Conditional Styling**: Apply styles based on cell values (Excel/PDF)

## Installation

Add the package to your project:

```xml
<PackageReference Include="BridgingIT.DevKit.Common.DataPorter" />
```

## Quick Start

### Service Registration

```csharp
services.AddDataPorter(configuration)
    .WithExcel(config => config.UseTableFormatting = true)
    .WithCsv(config => config.Delimiter = ",")
    .WithJson()
    .WithXml()
    .WithPdf();
```

### Basic Export

```csharp
public class MyService
{
    private readonly IDataExporter exporter;

    public MyService(IDataExporter exporter)
    {
        this.exporter = exporter;
    }

    public async Task ExportOrdersAsync(IEnumerable<Order> orders, Stream output)
    {
        var result = await this.exporter.ExportAsync(orders, output, new ExportOptions
        {
            Format = "excel"
        });

        if (result.IsSuccess)
        {
            Console.WriteLine($"Exported {result.Value.RowsExported} rows");
        }
    }
}
```

### Basic Import

```csharp
public async Task<IEnumerable<Order>> ImportOrdersAsync(Stream input)
{
    var result = await this.importer.ImportAsync<Order>(input, new ImportOptions
    {
        Format = "csv"
    });

    if (result.IsSuccess)
    {
        return result.Value.Data;
    }

    // Handle errors
    foreach (var error in result.Value.Errors)
    {
        Console.WriteLine($"Row {error.RowNumber}: {error.Message}");
    }

    return [];
}
```

## Configuration Approaches

### 1. Attribute-Based Configuration

Use attributes directly on your DTOs for simple scenarios:

```csharp
[DataPorterSheet("Products")]
public class ProductDto
{
    [DataPorterColumn("Product ID", Order = 0)]
    public string Id { get; set; }

    [DataPorterColumn("Name", Order = 1, Required = true)]
    public string Name { get; set; }

    [DataPorterColumn("Price", Format = "C2", HorizontalAlignment = HorizontalAlignment.Right)]
    public decimal Price { get; set; }

    [DataPorterColumn("In Stock", Order = 3)]
    public bool InStock { get; set; }

    [DataPorterIgnore]
    public string InternalCode { get; set; }
}
```

**Available Attributes:**

| Attribute | Target | Description |
|-----------|--------|-------------|
| `[DataPorterSheet]` | Class | Sets the sheet/section name |
| `[DataPorterColumn]` | Property | Configures column settings |
| `[DataPorterIgnore]` | Property | Excludes property from export/import |
| `[DataPorterConverter]` | Property | Specifies a custom value converter |
| `[DataPorterValidation]` | Property | Adds validation rules |

### 2. Profile-Based Configuration

Use profiles for complex scenarios with full control:

#### Export Profile

```csharp
public class OrderExportProfile : ExportProfileBase<Order>
{
    protected override void Configure()
    {
        ToSheet("Orders");

        ForColumn(o => o.Id)
            .HasName("Order ID")
            .HasOrder(0);

        ForColumn(o => o.CustomerName)
            .HasName("Customer")
            .HasOrder(1)
            .HasWidth(30);

        ForColumn(o => o.TotalAmount)
            .HasName("Total")
            .HasFormat("C2")
            .Align(HorizontalAlignment.Right)
            .StyleWhen(amount => amount > 1000, style => style
                .Bold()
                .WithBackgroundColor("#FFFF00"));

        ForColumn(o => o.OrderDate)
            .HasName("Date")
            .HasFormat("yyyy-MM-dd");

        ForColumn(o => o.IsShipped)
            .HasName("Shipped")
            .UseConverter(new BooleanYesNoConverter());

        Ignore(o => o.InternalNotes);

        AddHeader("Order Report");
        AddFooter(orders => $"Total Orders: {orders.Count()}");
    }
}
```

#### Import Profile

```csharp
public class OrderImportProfile : ImportProfileBase<Order>
{
    protected override void Configure()
    {
        FromSheet("Orders");
        HeaderRow(0);
        SkipDataRows(0);
        OnValidationFailure(ImportValidationBehavior.CollectErrors);

        ForColumn(o => o.Id)
            .FromHeader("Order ID")
            .IsRequired("Order ID is required");

        ForColumn(o => o.CustomerName)
            .FromHeader("Customer")
            .IsRequired()
            .Validate(name => name.Length <= 100, "Customer name too long");

        ForColumn(o => o.TotalAmount)
            .FromHeader("Total")
            .ParseWith(value => decimal.Parse(value, NumberStyles.Currency));

        ForColumn(o => o.OrderDate)
            .FromHeader("Date")
            .HasFormat("yyyy-MM-dd");

        Ignore(o => o.InternalNotes);

        UseFactory(() => new Order { CreatedAt = DateTime.UtcNow });
    }
}
```

### Registering Profiles

```csharp
services.AddDataPorter(configuration)
    .AddExportProfile<OrderExportProfile>()
    .AddImportProfile<OrderImportProfile>()
    // Or scan an assembly
    .AddProfilesFromAssemblyContaining<OrderExportProfile>();
```

## Format Providers

### Excel Provider

Uses ClosedXML for Excel file handling.

```csharp
services.AddDataPorter()
    .WithExcel(config =>
    {
        config.UseTableFormatting = true;
        config.DefaultTableStyleName = "TableStyleMedium2";
        config.AutoFitColumns = true;
        config.FreezeHeaderRow = true;
        config.DefaultFontName = "Calibri";
        config.DefaultFontSize = 11;
        config.MaxColumnWidth = 100;
    });
```

**Features:**
- Table formatting with styles
- Auto-fit columns
- Freeze header row
- Conditional formatting
- Multi-sheet export/import

### CSV Provider

Uses CsvHelper for CSV file handling.

```csharp
services.AddDataPorter()
    .WithCsv(config =>
    {
        config.Delimiter = ",";
        config.Encoding = Encoding.UTF8;
        config.Culture = CultureInfo.InvariantCulture;
        config.IncludeHeader = true;
        config.QuoteCharacter = '"';
        config.TrimFields = true;
        config.IgnoreBlankLines = true;
    });
```

### JSON Provider

Uses System.Text.Json for JSON handling.

```csharp
services.AddDataPorter()
    .WithJson(config =>
    {
        config.WriteIndented = true;
        config.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        config.IgnoreNullValues = false;
        config.DateFormat = "yyyy-MM-ddTHH:mm:ssZ";
    });
```

### XML Provider

Uses System.Xml for XML handling.

```csharp
services.AddDataPorter()
    .WithXml(config =>
    {
        config.RootElementName = "Data";
        config.ItemElementName = "Item";
        config.UseAttributes = false;
        config.Indent = true;
        config.DateFormat = "yyyy-MM-dd";
    });
```

### PDF Provider (Export Only)

Uses PDFsharp-MigraDoc for PDF generation (MIT licensed).

```csharp
services.AddDataPorter()
    .WithPdf(config =>
    {
        config.PageSize = PdfPageSize.A4;
        config.Orientation = PdfPageOrientation.Landscape;
        config.Margin = 50;
        config.Title = "Report";
        config.HeaderText = "My Report";
        config.FooterText = "Confidential";
        config.ShowPageNumbers = true;
        config.ShowGenerationDate = true;
        config.FontFamily = "Helvetica";
        config.HeaderFontSize = 10;
        config.BodyFontSize = 9;
        config.TableHeaderBackgroundColor = "#4472C4";
        config.TableHeaderTextColor = "#FFFFFF";
        config.UseAlternatingRowColors = true;
        config.AlternateRowBackgroundColor = "#F2F2F2";
    });
```

## Advanced Features

### Streaming Import

For large files, use streaming to avoid loading everything into memory:

```csharp
await foreach (var result in importer.ImportStreamAsync<Order>(stream, options))
{
    if (result.IsSuccess)
    {
        await ProcessOrderAsync(result.Value);
    }
    else
    {
        LogError(result.Errors.First().Message);
    }
}
```

### Validation Without Import

Validate data without actually importing:

```csharp
var validationResult = await importer.ValidateAsync<Order>(stream, options);

if (validationResult.IsSuccess && validationResult.Value.IsValid)
{
    Console.WriteLine($"All {validationResult.Value.TotalRows} rows are valid");
}
else
{
    foreach (var error in validationResult.Value.Errors)
    {
        Console.WriteLine($"Row {error.RowNumber}, Column {error.Column}: {error.Message}");
    }
}
```

### Multi-Sheet Export

Export multiple data sets to different sheets (Excel) or sections (JSON/XML):

```csharp
var dataSets = new[]
{
    new ExportDataSet(orders, typeof(Order), "Orders"),
    new ExportDataSet(products, typeof(Product), "Products"),
    new ExportDataSet(customers, typeof(Customer), "Customers")
};

await exporter.ExportMultipleAsync(dataSets, stream, new ExportOptions
{
    Format = "excel"
});
```

### Custom Value Converters

Create custom converters for complex transformations:

```csharp
public class StatusConverter : IValueConverter<OrderStatus>
{
    public object ConvertToExport(OrderStatus value, ValueConversionContext context)
    {
        return value switch
        {
            OrderStatus.Pending => "Pending",
            OrderStatus.Processing => "In Progress",
            OrderStatus.Shipped => "Shipped",
            OrderStatus.Delivered => "Delivered",
            _ => "Unknown"
        };
    }

    public OrderStatus ConvertFromImport(object value, ValueConversionContext context)
    {
        var str = value?.ToString();
        return str switch
        {
            "Pending" => OrderStatus.Pending,
            "In Progress" => OrderStatus.Processing,
            "Shipped" => OrderStatus.Shipped,
            "Delivered" => OrderStatus.Delivered,
            _ => OrderStatus.Pending
        };
    }
}
```

Use in a profile:

```csharp
ForColumn(o => o.Status)
    .UseConverter(new StatusConverter());
```

### Built-in Converters

| Converter | Description |
|-----------|-------------|
| `BooleanYesNoConverter` | Converts bool to "Yes"/"No" |
| `EnumDisplayNameConverter<T>` | Uses `[Display]` attribute for enum names |

### Validation Behaviors

Control how validation errors are handled during import:

```csharp
public enum ImportValidationBehavior
{
    CollectErrors,  // Continue import, collect all errors
    SkipRow,        // Skip invalid rows, continue with valid ones
    StopImport      // Stop on first error
}
```

### Conditional Styling

Apply styles based on values (Excel/PDF):

```csharp
ForColumn(o => o.Amount)
    .StyleWhen(
        amount => amount < 0,
        style => style
            .WithForegroundColor("#FF0000")
            .Bold())
    .StyleWhen(
        amount => amount > 10000,
        style => style
            .WithBackgroundColor("#00FF00"));
```

## Error Handling

The framework uses the Result pattern for consistent error handling:

```csharp
var result = await exporter.ExportAsync(data, stream, options);

if (result.IsFailure)
{
    foreach (var error in result.Errors)
    {
        if (error is FormatNotSupportedError)
        {
            Console.WriteLine("Format not supported");
        }
        else if (error is ExportError exportError)
        {
            Console.WriteLine($"Export failed: {exportError.Message}");
        }
    }
}
```

## Configuration via appsettings.json

Providers can be configured via configuration:

```json
{
  "DataPorter": {
    "Excel": {
      "UseTableFormatting": true,
      "AutoFitColumns": true
    },
    "Csv": {
      "Delimiter": ";",
      "IncludeHeader": true
    },
    "Pdf": {
      "PageSize": "A4",
      "Orientation": "Landscape"
    }
  }
}
```

```csharp
services.AddDataPorter(configuration)
    .WithExcel()  // Reads from DataPorter:Excel
    .WithCsv()    // Reads from DataPorter:Csv
    .WithPdf();   // Reads from DataPorter:Pdf
```

## Architecture

```
Common.DataPorter/
├── Abstractions/           # Core interfaces
│   ├── IDataExporter.cs
│   ├── IDataImporter.cs
│   ├── IDataPorterProvider.cs
│   ├── IExportProfile.cs
│   ├── IImportProfile.cs
│   └── IValueConverter.cs
├── Attributes/             # Attribute-based configuration
├── Configuration/          # Profile base classes and builders
├── Converters/             # Built-in value converters
├── Errors/                 # Error types for Result pattern
├── Models/                 # Result and option models
├── Providers/              # Format-specific implementations
│   ├── Csv/
│   ├── Excel/
│   ├── Json/
│   ├── Pdf/
│   └── Xml/
└── Services/               # Core service implementations
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| ClosedXML | Latest | Excel file handling |
| CsvHelper | Latest | CSV file handling |
| PDFsharp-MigraDoc | 6.2.0 | PDF generation (MIT licensed) |
| System.Text.Json | Built-in | JSON handling |
| System.Xml.Linq | Built-in | XML handling |

## License

MIT License - See LICENSE file for details.
