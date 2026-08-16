// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

/// <summary>
///     Serializes objects and object sequences as CSV and deserializes CSV records using CsvHelper.
/// </summary>
public class CsvSerializer : ISerializer
{
    private readonly CsvConfiguration config;
    private readonly string dateTimeFormat;
    private readonly CultureInfo culture;
    private readonly List<Type> classMaps = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="CsvSerializer"/> class with DevKit CSV settings.
    /// </summary>
    /// <param name="settings">The settings to apply, or <see langword="null"/> to use the defaults.</param>
    public CsvSerializer(CsvSerializerSettings settings = null)
    {
        settings ??= new CsvSerializerSettings();
        this.dateTimeFormat = settings.DateTimeFormat;
        this.culture = settings.Culture;
        this.config = this.CreateConfiguration(settings);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CsvSerializer"/> class with an existing CsvHelper configuration.
    /// </summary>
    /// <param name="configuration">The CsvHelper configuration to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    public CsvSerializer(CsvConfiguration configuration)
    {
        this.config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        this.culture = configuration.CultureInfo;
    }

    /// <summary>
    ///     Writes a value, or each item in an object sequence, as CSV while leaving the output stream open.
    /// </summary>
    /// <param name="value">The value or sequence to serialize. A <see langword="null"/> value is ignored.</param>
    /// <param name="output">The destination stream. A <see langword="null"/> stream is ignored.</param>
    /// <exception cref="SerializationException">CsvHelper cannot serialize the value.</exception>
    public void Serialize(object value, Stream output)
    {
        if (value is null || output is null)
        {
            return;
        }

        try
        {
            using var writer = new StreamWriter(output, this.config.Encoding, 1024, true);
            using var csv = new CsvWriter(writer, this.config);

            this.ConfigureWriter(csv);

            if (value is IEnumerable<object> collection)
            {
                csv.WriteRecords(collection);
            }
            else
            {
                csv.WriteRecords([value]);
            }

            writer.Flush();
        }
        catch (CsvHelperException ex)
        {
            throw new SerializationException("Failed to serialize to CSV.", ex);
        }
    }

    /// <summary>
    ///     Reads all CSV records as instances of the specified runtime type while leaving the input stream open.
    /// </summary>
    /// <param name="input">The CSV input stream, or <see langword="null"/> to return <see langword="null"/>.</param>
    /// <param name="type">The type to create for each CSV record.</param>
    /// <returns>A list containing the deserialized records, or <see langword="null"/> when <paramref name="input"/> is <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="SerializationException">The CSV data cannot be deserialized.</exception>
    public object Deserialize(Stream input, Type type)
    {
        if (type is null)
        {
            throw new ArgumentNullException("Type cannot be null when deserializing", nameof(type));
        }

        if (input is null)
        {
            return null;
        }

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        try
        {
            using var reader = new StreamReader(input, this.config.Encoding, true, 1024, true);
            using var csv = new CsvReader(reader, this.config);

            this.ConfigureReader(csv);

            csv.Read();
            csv.ReadHeader();
            var records = csv.GetRecords(type).ToList();
            return records;
        }
        catch (CsvHelperException ex)
        {
            throw new SerializationException($"Failed to deserialize CSV to type {type.Name}.", ex);
        }
        catch (Exception ex)
        {
            throw new SerializationException("An unexpected error occurred during CSV deserialization.", ex);
        }
    }

    /// <summary>
    ///     Reads the first CSV record as an instance of <typeparamref name="T"/> while leaving the input stream open.
    /// </summary>
    /// <typeparam name="T">The record type to create.</typeparam>
    /// <param name="input">The CSV input stream, or <see langword="null"/> to return the default value.</param>
    /// <returns>The first deserialized record, or the default value when no record is available.</returns>
    /// <exception cref="SerializationException">The CSV data cannot be deserialized.</exception>
    public T Deserialize<T>(Stream input)
    {
        if (input is null)
        {
            return default;
        }

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        try
        {
            using var reader = new StreamReader(input, this.config.Encoding, true, 1024, true);
            using var csv = new CsvReader(reader, this.config);

            this.ConfigureReader(csv);

            csv.Read();
            csv.ReadHeader();
            return csv.GetRecords<T>().FirstOrDefault();
        }
        catch (CsvHelperException ex)
        {
            throw new SerializationException($"Failed to deserialize CSV to type {typeof(T).Name}.", ex);
        }
        catch (Exception ex)
        {
            throw new SerializationException("An unexpected error occurred during CSV deserialization.", ex);
        }
    }

    private CsvConfiguration CreateConfiguration(CsvSerializerSettings options)
    {
        var config = new CsvConfiguration(options.Culture)
        {
            Delimiter = options.Delimiter,
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            Encoding = options.Encoding
        };

        if (options.HeaderMappings?.Count > 0)
        {
            config.PrepareHeaderForMatch = args =>
                options.HeaderMappings.TryGetValue(args.Header, out var mapped)
                    ? mapped
                    : args.Header;
        }

        return config;
    }

    /// <summary>
    ///     Registers the configured date-time converter and class maps with a CSV reader.
    /// </summary>
    /// <param name="csv">The reader to configure.</param>
    protected virtual void ConfigureReader(CsvReader csv)
    {
        csv.Context.TypeConverterCache.AddConverter<DateTime>(
            new CustomDateTimeConverter(this.dateTimeFormat, this.culture));

        foreach (var mapType in this.classMaps)
        {
            csv.Context.RegisterClassMap(mapType);
        }
    }

    /// <summary>
    ///     Registers the configured date-time converter and class maps with a CSV writer.
    /// </summary>
    /// <param name="csv">The writer to configure.</param>
    protected virtual void ConfigureWriter(CsvWriter csv)
    {
        csv.Context.TypeConverterCache.AddConverter<DateTime>(
            new CustomDateTimeConverter(this.dateTimeFormat, this.culture));

        foreach (var mapType in this.classMaps)
        {
            csv.Context.RegisterClassMap(mapType);
        }
    }

    /// <summary>
    ///     Registers a CsvHelper class map for subsequent serialization and deserialization operations.
    /// </summary>
    /// <typeparam name="T">The class-map type to register.</typeparam>
    public virtual void RegisterClassMap<T>() where T : ClassMap
    {
        this.classMaps.Add(typeof(T));
    }
}

/// <summary>
///     Defines delimiter, culture, encoding, date-time, header, and class-map settings for <see cref="CsvSerializer"/>.
/// </summary>
public sealed class CsvSerializerSettings
{
    /// <summary>
    ///     Gets the field delimiter. The default is a semicolon.
    /// </summary>
    public string Delimiter { get; init; } = ";";

    /// <summary>
    ///     Gets the culture used to parse and format CSV values.
    /// </summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    ///     Gets the exact format used to parse and format <see cref="DateTime"/> values.
    /// </summary>
    public string DateTimeFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    ///     Gets optional mappings from incoming header names to member names.
    /// </summary>
    public Dictionary<string, string> HeaderMappings { get; init; }

    /// <summary>
    ///     Gets the text encoding used for input and output streams.
    /// </summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);

    /// <summary>
    ///     Gets the CsvHelper class-map types associated with these settings.
    /// </summary>
    public List<Type> ClassMaps { get; init; } = [];
}

/// <summary>
///     Converts CSV date-time fields using one exact format and culture.
/// </summary>
public sealed class CustomDateTimeConverter : DefaultTypeConverter
{
    private readonly string format;
    private readonly CultureInfo culture;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CustomDateTimeConverter"/> class.
    /// </summary>
    /// <param name="format">The exact date-time format to use.</param>
    /// <param name="culture">The culture used for parsing and formatting.</param>
    public CustomDateTimeConverter(string format, CultureInfo culture)
    {
        this.format = format;
        this.culture = culture;
    }

    /// <inheritdoc/>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return DateTime.ParseExact(text, this.format, this.culture);
    }

    /// <inheritdoc/>
    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString(this.format, this.culture);
        }

        return base.ConvertToString(value, row, memberMapData);
    }
}
