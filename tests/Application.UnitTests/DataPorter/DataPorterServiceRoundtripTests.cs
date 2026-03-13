// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Reflection;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Domain.Model;
using ClosedXML.Excel;
using Dumpify;
using Xunit.Abstractions;

[UnitTest("Common")]
public class DataPorterServiceRoundtripTests
{
    public static TheoryData<IDataPorterProvider, Format> RoundtripProviders => new()
    {
        { new CsvDataPorterProvider(), Format.Csv },
        { new CsvTypedDataPorterProvider(), Format.CsvTyped },
        { new ExcelDataPorterProvider(), Format.Excel },
        { new JsonDataPorterProvider(), Format.Json },
        { new XmlDataPorterProvider(), Format.Xml }
    };

    private readonly ProfileRegistry profileRegistry;
    private readonly AttributeConfigurationReader attributeReader;
    private readonly ConfigurationMerger configurationMerger;
    private readonly ITestOutputHelper output;

    public DataPorterServiceRoundtripTests(ITestOutputHelper output)
    {
        this.output = output;
        this.profileRegistry = new ProfileRegistry(
            [new PersonEntityExportProfile()],
            [new PersonEntityImportProfile()]);
        this.attributeReader = new AttributeConfigurationReader();
        this.configurationMerger = new ConfigurationMerger(this.profileRegistry, this.attributeReader);
    }

    [Theory]
    [MemberData(nameof(RoundtripProviders))]
    public async Task ExportAndImportAsync_WithSupportedProvider_RoundTripsAggregateRootEntity(
        IDataPorterProvider provider,
        Format format)
    {
        // Arrange
        var sut = new DataPorterService([provider], this.configurationMerger);
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = format, UseAttributes = false };
        var importOptions = new ImportOptions { Format = format, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(format, stream);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);

        // Assert
        exportResult.ShouldBeSuccess();
        importResult.ShouldBeSuccess();
        importResult.Value.Data.Count.ShouldBe(data.Length);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        AssertPersons([.. importResult.Value.Data], data);
    }

    [Fact]
    public async Task ExportAndImportAsync_WithCsvProviderAndChildEntityColumn_DoesNotRoundTripChildEntity()
    {
        // Arrange
        var sut = new DataPorterService(
            [new CsvDataPorterProvider(new CsvConfiguration { UseNesting = false })],
            this.CreateChildEntityConfigurationMerger());
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = Format.Csv, UseAttributes = false };
        var importOptions = new ImportOptions { Format = Format.Csv, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(Format.Csv, stream);
        var exportedContent = ReadTextContent(stream);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        // Assert
        exportResult.ShouldBeSuccess();
        exportedContent.ShouldNotContain($",{nameof(PersonEntity.Address)},");
        exportedContent.ShouldNotContain($",{nameof(PersonEntity.PreviousAddresses)},");
        exportedContent.ShouldContain(nameof(PersonEntity.BillingAddress));
        AssertChildEntityIsNotImported(importResult, data.Length);
    }

    [Fact]
    public async Task ExportAndImportAsync_WithCsvProviderAndNestingEnabled_RoundTripsChildEntityAndCollection()
    {
        // Arrange
        var sut = new DataPorterService(
            [new CsvDataPorterProvider(new CsvConfiguration { UseNesting = true })],
            this.CreateChildEntityConfigurationMerger());
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = Format.Csv, UseAttributes = false };
        var importOptions = new ImportOptions { Format = Format.Csv, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(Format.Csv, stream);
        var exportedContent = ReadTextContent(stream);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        // Assert
        exportResult.ShouldBeSuccess();
        exportedContent.ShouldContain("Address_Street");
        exportedContent.ShouldContain("Address_City");
        exportedContent.ShouldContain("PreviousAddresses_Street");
        importResult.ShouldBeSuccess();
        AssertPersons([.. importResult.Value.Data], data);
        AssertNestedData([.. importResult.Value.Data], data);
    }

    [Fact]
    public async Task ExportAndImportAsync_WithCsvTypedProvider_RoundTripsTypedHierarchy()
    {
        // Arrange
        var sut = new DataPorterService([new CsvTypedDataPorterProvider()], this.CreateChildEntityConfigurationMerger());
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = Format.CsvTyped, UseAttributes = false };
        var importOptions = new ImportOptions { Format = Format.CsvTyped, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(Format.CsvTyped, stream);
        var exportedContent = ReadTextContent(stream);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        // Assert
        exportResult.ShouldBeSuccess();
        exportedContent.ShouldContain("RecordType,RootId,RecordId,ParentId,Collection,Index");
        exportedContent.ShouldContain("Person,");
        exportedContent.ShouldContain("Address,");
        exportedContent.ShouldContain("BillingAddress,");
        exportedContent.ShouldContain("PreviousAddress,");
        importResult.ShouldBeSuccess();
        AssertPersons([.. importResult.Value.Data], data);
        AssertNestedData([.. importResult.Value.Data], data);
    }

    [Fact]
    public async Task ExportAndImportAsync_WithCsvProviderAndNestingDisabled_IgnoresNestedColumns()
    {
        // Arrange
        var sut = new DataPorterService(
            [new CsvDataPorterProvider(new CsvConfiguration { UseNesting = false })],
            this.CreateChildEntityConfigurationMerger());
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = Format.Csv, UseAttributes = false };
        var importOptions = new ImportOptions { Format = Format.Csv, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(Format.Csv, stream);
        var exportedContent = ReadTextContent(stream);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        // Assert
        exportResult.ShouldBeSuccess();
        exportedContent.ShouldNotContain($",{nameof(PersonEntity.Address)},");
        exportedContent.ShouldNotContain($",{nameof(PersonEntity.PreviousAddresses)},");
        exportedContent.ShouldNotStartWith($"{nameof(PersonEntity.Address)},");
        exportedContent.ShouldNotStartWith($"{nameof(PersonEntity.PreviousAddresses)},");
        exportedContent.ShouldContain(nameof(PersonEntity.BillingAddress));
        importResult.ShouldBeSuccess();
        importResult.Value.Errors.ShouldBeEmpty();
        importResult.Value.Data.Count.ShouldBe(data.Length);
        importResult.Value.Data.All(e => e.Address is null).ShouldBeTrue();
        importResult.Value.Data.All(e => e.PreviousAddresses.Count == 0).ShouldBeTrue();
        importResult.Value.Data[0].BillingAddress.ShouldBe(data[0].BillingAddress);
        importResult.Value.Data[1].BillingAddress.ShouldBe(data[1].BillingAddress);
    }

    [Fact]
    public async Task ExportAndImportAsync_WithExcelProviderAndChildEntityColumn_DoesNotRoundTripChildEntity()
    {
        // Arrange
        var sut = new DataPorterService([new ExcelDataPorterProvider()], this.CreateChildEntityConfigurationMerger());
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = Format.Excel, UseAttributes = false };
        var importOptions = new ImportOptions { Format = Format.Excel, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(Format.Excel, stream);
        var addressCellValue = ReadExcelCellValue(stream, "Persons", 2, 6);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        // Assert
        exportResult.ShouldBeSuccess();
        addressCellValue.ShouldContain("Ada Lovelace|Analytical Engine Way 1|A1 100|London|UK");
        AssertChildEntityIsNotImported(importResult, data.Length);
    }

    [Fact]
    public async Task ExportAndImportAsync_WithJsonProviderAndNestedColumns_RoundTripsChildEntityAndCollection()
    {
        // Arrange
        var sut = new DataPorterService([new JsonDataPorterProvider()], this.CreateChildEntityConfigurationMerger());
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = Format.Json, UseAttributes = false };
        var importOptions = new ImportOptions { Format = Format.Json, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(Format.Json, stream);
        var exportedContent = ReadTextContent(stream);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        // Assert
        exportResult.ShouldBeSuccess();
        exportedContent.ShouldContain("\"Address\": {");
        exportedContent.ShouldContain("\"Street\": \"Analytical Engine Way 1\"");
        importResult.ShouldBeSuccess();
        AssertPersons([.. importResult.Value.Data], data);
        AssertNestedData([.. importResult.Value.Data], data);
    }

    [Fact]
    public async Task ExportAndImportAsync_WithXmlProviderAndNestedColumns_RoundTripsChildEntityAndCollection()
    {
        // Arrange
        var sut = new DataPorterService([new XmlDataPorterProvider()], this.CreateChildEntityConfigurationMerger());
        var data = CreatePersons();
        await using var stream = new MemoryStream();
        var exportOptions = new ExportOptions { Format = Format.Xml, UseAttributes = false };
        var importOptions = new ImportOptions { Format = Format.Xml, UseAttributes = false };

        // Act
        var exportResult = await sut.ExportAsync(data, stream, exportOptions);
        this.WriteExportToOutput(Format.Xml, stream);
        var exportedContent = ReadTextContent(stream);
        stream.Position = 0;
        var importResult = await sut.ImportAsync<PersonEntity>(stream, importOptions);
        this.output.WriteLine("Imported data:");
        this.output.WriteLine(importResult.Value.Data.DumpText());

        // Assert
        exportResult.ShouldBeSuccess();
        exportedContent.ShouldContain("<Address>");
        exportedContent.ShouldContain("<Street>Analytical Engine Way 1</Street>");
        exportedContent.ShouldContain("<PreviousAddresses>");
        importResult.ShouldBeSuccess();
        AssertPersons([.. importResult.Value.Data], data);
        AssertNestedData([.. importResult.Value.Data], data);
    }

    private static PersonEntity[] CreatePersons()
    {
        return
        [
            new PersonEntity
            {
                Id = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Lovelace",
                Age = 36,
                ManagerId = Guid.NewGuid(),
                Address = new AddressEntity
                {
                    Id = Guid.NewGuid(),
                    Street = "Analytical Engine Way 1",
                    City = "London"
                },
                BillingAddress = BillingAddressValueObject.Create("Ada Lovelace", "Analytical Engine Way 1", "A1 100", "London", "UK"),
                PreviousAddresses =
                [
                    new AddressEntity
                    {
                        Id = Guid.NewGuid(),
                        Street = "Byron Avenue 5",
                        City = "London"
                    },
                    new AddressEntity
                    {
                        Id = Guid.NewGuid(),
                        Street = "Countess Road 9",
                        City = "Oxford"
                    }
                ],
                Status = PersonStatus.Active
            },
            new PersonEntity
            {
                Id = Guid.NewGuid(),
                FirstName = "Grace",
                LastName = "Hopper",
                Age = 85,
                ManagerId = null,
                Address = new AddressEntity
                {
                    Id = Guid.NewGuid(),
                    Street = "Compiler Street 42",
                    City = "Arlington"
                },
                BillingAddress = BillingAddressValueObject.Create("Grace Hopper", "Compiler Street 42", "C0 200", "Arlington", "US"),
                PreviousAddresses =
                [
                    new AddressEntity
                    {
                        Id = Guid.NewGuid(),
                        Street = "Cobol Lane 1",
                        City = "New York"
                    }
                ],
                Status = PersonStatus.Inactive
            }
        ];
    }

    private static void AssertPersons(PersonEntity[] imported, PersonEntity[] expected)
    {
        imported.Length.ShouldBe(expected.Length);

        imported[0].Id.ShouldBe(expected[0].Id);
        imported[0].FirstName.ShouldBe(expected[0].FirstName);
        imported[0].LastName.ShouldBe(expected[0].LastName);
        imported[0].Age.ShouldBe(expected[0].Age);
        imported[0].ManagerId.ShouldBe(expected[0].ManagerId);
        imported[0].Status.ShouldBe(expected[0].Status);
        imported[0].BillingAddress.ShouldBe(expected[0].BillingAddress);

        imported[1].Id.ShouldBe(expected[1].Id);
        imported[1].FirstName.ShouldBe(expected[1].FirstName);
        imported[1].LastName.ShouldBe(expected[1].LastName);
        imported[1].Age.ShouldBe(expected[1].Age);
        imported[1].ManagerId.ShouldBeNull();
        imported[1].Status.ShouldBe(expected[1].Status);
        imported[1].BillingAddress.ShouldBe(expected[1].BillingAddress);
    }

    private static void AssertChildEntityIsNotImported(Result<ImportResult<PersonEntity>> importResult, int expectedCount)
    {
        importResult.ShouldBeSuccess();
        importResult.Value.Data.Count.ShouldBe(expectedCount);
        importResult.Value.Data.All(e => e.Address is null).ShouldBeTrue();
        importResult.Value.Data.All(e => e.PreviousAddresses is null || e.PreviousAddresses.Count == 0).ShouldBeTrue();
    }

    private static void AssertNestedData(PersonEntity[] imported, PersonEntity[] expected)
    {
        imported[0].Address.ShouldNotBeNull();
        imported[0].Address.Id.ShouldBe(expected[0].Address.Id);
        imported[0].Address.Street.ShouldBe(expected[0].Address.Street);
        imported[0].Address.City.ShouldBe(expected[0].Address.City);
        imported[0].PreviousAddresses.Count.ShouldBe(expected[0].PreviousAddresses.Count);
        imported[0].PreviousAddresses[0].Street.ShouldBe(expected[0].PreviousAddresses[0].Street);
        imported[0].PreviousAddresses[1].City.ShouldBe(expected[0].PreviousAddresses[1].City);

        imported[1].Address.ShouldNotBeNull();
        imported[1].Address.Id.ShouldBe(expected[1].Address.Id);
        imported[1].Address.Street.ShouldBe(expected[1].Address.Street);
        imported[1].Address.City.ShouldBe(expected[1].Address.City);
        imported[1].PreviousAddresses.Count.ShouldBe(expected[1].PreviousAddresses.Count);
        imported[1].PreviousAddresses[0].Street.ShouldBe(expected[1].PreviousAddresses[0].Street);
    }

    private ConfigurationMerger CreateChildEntityConfigurationMerger()
    {
        var profileRegistry = new ProfileRegistry(
            [new PersonEntityWithChildExportProfile()],
            [new PersonEntityWithChildImportProfile()]);

        return new ConfigurationMerger(profileRegistry, new AttributeConfigurationReader());
    }

    private static string ReadTextContent(MemoryStream stream)
    {
        stream.Position = 0;

        using var reader = new StreamReader(stream, leaveOpen: true);
        var content = reader.ReadToEnd();

        stream.Position = 0;
        return content;
    }

    private static string ReadExcelCellValue(MemoryStream stream, string worksheetName, int row, int column)
    {
        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);
        var value = workbook.Worksheet(worksheetName).Cell(row, column).GetValue<string>();

        stream.Position = 0;
        return value;
    }

    private void WriteExportToOutput(Format format, MemoryStream stream)
    {
        switch (format)
        {
            case Format.Csv:
            case Format.CsvTyped:
            case Format.Json:
            case Format.Xml:
                this.WriteTextContentToOutput(format, stream);
                break;
            case Format.Excel:
                this.WriteExcelContentToOutput(stream);
                break;
        }

        stream.Position = 0;
    }

    private void WriteTextContentToOutput(Format format, MemoryStream stream)
    {
        stream.Position = 0;

        using var reader = new StreamReader(stream, leaveOpen: true);
        var content = reader.ReadToEnd();

        this.output.WriteLine($"{format} export:");
        this.output.WriteLine(content);
    }

    private void WriteExcelContentToOutput(MemoryStream stream)
    {
        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);

        this.output.WriteLine("Excel export:");

        foreach (var worksheet in workbook.Worksheets)
        {
            this.output.WriteLine($"[{worksheet.Name}]");

            foreach (var row in worksheet.RowsUsed())
            {
                var values = row.CellsUsed()
                    .Select(cell => cell.GetValue<string>());

                this.output.WriteLine(string.Join(" | ", values));
            }
        }
    }

    internal static ColumnConfiguration CreateExportColumn(string propertyName, int order, IValueConverter converter = null)
    {
        var column = new ColumnConfiguration
        {
            PropertyName = propertyName,
            HeaderName = propertyName,
            Order = order,
            Converter = converter
        };

        typeof(ColumnConfiguration)
            .GetProperty(nameof(ColumnConfiguration.PropertyInfo), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(column, typeof(PersonEntity).GetProperty(propertyName));

        return column;
    }

    internal static ImportColumnConfiguration CreateImportColumn(string propertyName, int order, IValueConverter converter = null)
    {
        var column = new ImportColumnConfiguration
        {
            PropertyName = propertyName,
            SourceName = propertyName,
            Order = order,
            Converter = converter
        };

        typeof(ImportColumnConfiguration)
            .GetProperty("PropertyInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(column, typeof(PersonEntity).GetProperty(propertyName));

        return column;
    }
}

public class PersonEntity : Entity<Guid>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }

    public Guid? ManagerId { get; set; }

    public AddressEntity Address { get; set; }

    public BillingAddressValueObject BillingAddress { get; set; }

    public List<AddressEntity> PreviousAddresses { get; set; } = [];

    public PersonStatus Status { get; set; }
}

public class AddressEntity : Entity<Guid>
{
    public string Street { get; set; }

    public string City { get; set; }
}

public class BillingAddressValueObject : ValueObject
{
    private BillingAddressValueObject()
    {
    }

    private BillingAddressValueObject(string name, string line1, string postalCode, string city, string country)
    {
        this.Name = name;
        this.Line1 = line1;
        this.PostalCode = postalCode;
        this.City = city;
        this.Country = country;
    }

    public string Name { get; }

    public string Line1 { get; }

    public string PostalCode { get; }

    public string City { get; }

    public string Country { get; }

    public static BillingAddressValueObject Create(string name, string line1, string postalCode, string city, string country)
    {
        return new BillingAddressValueObject(name, line1, postalCode, city, country);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Name;
        yield return this.Line1;
        yield return this.PostalCode;
        yield return this.City;
        yield return this.Country;
    }
}

public class BillingAddressValueObjectConverter : IValueConverter<BillingAddressValueObject>
{
    private const char Separator = '|';

    public object ConvertToExport(BillingAddressValueObject value, ValueConversionContext context)
    {
        if (value is null)
        {
            return null;
        }

        return string.Join(Separator, [value.Name, value.Line1, value.PostalCode, value.City, value.Country]);
    }

    public BillingAddressValueObject ConvertFromImport(object value, ValueConversionContext context)
    {
        if (value is BillingAddressValueObject billingAddress)
        {
            return billingAddress;
        }

        var stringValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        var parts = stringValue.Split(Separator);
        return parts.Length != 5
            ? null
            : BillingAddressValueObject.Create(parts[0], parts[1], parts[2], parts[3], parts[4]);
    }

    object IValueConverter.ConvertToExport(object value, ValueConversionContext context)
    {
        return this.ConvertToExport(value as BillingAddressValueObject, context);
    }

    object IValueConverter.ConvertFromImport(object value, ValueConversionContext context)
    {
        return this.ConvertFromImport(value, context);
    }
}

public class PersonStatus(int id, string value) : Enumeration(id, value)
{
    public static readonly PersonStatus Pending = new(1, "Pending");
    public static readonly PersonStatus Active = new(2, "Active");
    public static readonly PersonStatus Inactive = new(3, "Inactive");

    private PersonStatus() : this(default, default)
    {
    }
}

public class PersonEntityExportProfile : IExportProfile<PersonEntity>
{
    public Type SourceType => typeof(PersonEntity);

    private static readonly BillingAddressValueObjectConverter billingAddressConverter = new();

    public IReadOnlyList<ColumnConfiguration> Columns { get; } =
    [
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Id), 0),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.FirstName), 1),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.LastName), 2),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Age), 3),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.ManagerId), 4),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.BillingAddress), 5, billingAddressConverter),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Status), 6, new EnumerationConverter<PersonStatus>())
    ];

    public string SheetName => "Persons";

    public IReadOnlyList<HeaderRowConfiguration> HeaderRows { get; } = [];

    public IReadOnlyList<FooterRowConfiguration> FooterRows { get; } = [];
}

public class PersonEntityImportProfile : IImportProfile<PersonEntity>
{
    public Type TargetType => typeof(PersonEntity);

    private static readonly BillingAddressValueObjectConverter billingAddressConverter = new();

    public IReadOnlyList<ImportColumnConfiguration> Columns { get; } =
    [
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Id), 0),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.FirstName), 1),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.LastName), 2),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Age), 3),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.ManagerId), 4),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.BillingAddress), 5, billingAddressConverter),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Status), 6, new EnumerationConverter<PersonStatus>())
    ];

    public string SheetName => "Persons";

    public int SheetIndex => -1;

    public int HeaderRowIndex => 0;

    public int SkipRows => 0;

    public ImportValidationBehavior ValidationBehavior => ImportValidationBehavior.CollectErrors;

    public Func<PersonEntity> Factory => () => new PersonEntity();

    Func<object> IImportProfile.Factory => () => new PersonEntity();
}

public class PersonEntityWithChildExportProfile : IExportProfile<PersonEntity>
{
    public Type SourceType => typeof(PersonEntity);

    private static readonly EnumerationConverter<PersonStatus> statusConverter = new();
    private static readonly BillingAddressValueObjectConverter billingAddressConverter = new();

    public IReadOnlyList<ColumnConfiguration> Columns { get; } =
    [
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Id), 0),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.FirstName), 1),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.LastName), 2),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Age), 3),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.ManagerId), 4),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.BillingAddress), 5, billingAddressConverter),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Address), 6),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.PreviousAddresses), 7),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Status), 8, statusConverter)
    ];

    public string SheetName => "Persons";

    public IReadOnlyList<HeaderRowConfiguration> HeaderRows { get; } = [];

    public IReadOnlyList<FooterRowConfiguration> FooterRows { get; } = [];
}

public class PersonEntityWithChildImportProfile : IImportProfile<PersonEntity>
{
    public Type TargetType => typeof(PersonEntity);

    private static readonly EnumerationConverter<PersonStatus> statusConverter = new();
    private static readonly BillingAddressValueObjectConverter billingAddressConverter = new();

    public IReadOnlyList<ImportColumnConfiguration> Columns { get; } =
    [
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Id), 0),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.FirstName), 1),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.LastName), 2),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Age), 3),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.ManagerId), 4),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.BillingAddress), 5, billingAddressConverter),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Address), 6),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.PreviousAddresses), 7),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Status), 8, statusConverter)
    ];

    public string SheetName => "Persons";

    public int SheetIndex => -1;

    public int HeaderRowIndex => 0;

    public int SkipRows => 0;

    public ImportValidationBehavior ValidationBehavior => ImportValidationBehavior.CollectErrors;

    public Func<PersonEntity> Factory => () => new PersonEntity();

    Func<object> IImportProfile.Factory => () => new PersonEntity();
}
