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
                Status = PersonStatus.Active
            },
            new PersonEntity
            {
                Id = Guid.NewGuid(),
                FirstName = "Grace",
                LastName = "Hopper",
                Age = 85,
                ManagerId = null,
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

        imported[1].Id.ShouldBe(expected[1].Id);
        imported[1].FirstName.ShouldBe(expected[1].FirstName);
        imported[1].LastName.ShouldBe(expected[1].LastName);
        imported[1].Age.ShouldBe(expected[1].Age);
        imported[1].ManagerId.ShouldBeNull();
        imported[1].Status.ShouldBe(expected[1].Status);
    }

    private void WriteExportToOutput(Format format, MemoryStream stream)
    {
        switch (format)
        {
            case Format.Csv:
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

public class PersonEntity : AggregateRoot<Guid>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }

    public Guid? ManagerId { get; set; }

    public PersonStatus Status { get; set; }
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

    public IReadOnlyList<ColumnConfiguration> Columns { get; } =
    [
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Id), 0),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.FirstName), 1),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.LastName), 2),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Age), 3),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.ManagerId), 4),
        DataPorterServiceRoundtripTests.CreateExportColumn(nameof(PersonEntity.Status), 5, new EnumerationConverter<PersonStatus>())
    ];

    public string SheetName => "Persons";

    public IReadOnlyList<HeaderRowConfiguration> HeaderRows { get; } = [];

    public IReadOnlyList<FooterRowConfiguration> FooterRows { get; } = [];
}

public class PersonEntityImportProfile : IImportProfile<PersonEntity>
{
    public Type TargetType => typeof(PersonEntity);

    public IReadOnlyList<ImportColumnConfiguration> Columns { get; } =
    [
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Id), 0),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.FirstName), 1),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.LastName), 2),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Age), 3),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.ManagerId), 4),
        DataPorterServiceRoundtripTests.CreateImportColumn(nameof(PersonEntity.Status), 5, new EnumerationConverter<PersonStatus>())
    ];

    public string SheetName => "Persons";

    public int SheetIndex => -1;

    public int HeaderRowIndex => 0;

    public int SkipRows => 0;

    public ImportValidationBehavior ValidationBehavior => ImportValidationBehavior.CollectErrors;

    public Func<PersonEntity> Factory => () => new PersonEntity();

    Func<object> IImportProfile.Factory => () => new PersonEntity();
}
