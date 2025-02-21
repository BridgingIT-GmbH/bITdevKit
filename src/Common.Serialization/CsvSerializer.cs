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

public class CsvSerializer : ISerializer
{
    private readonly CsvConfiguration config;
    private readonly string dateTimeFormat;
    private readonly CultureInfo culture;
    private readonly List<Type> classMaps = new();

    public CsvSerializer(CsvSerializerSettings settings = null)
    {
        settings ??= new CsvSerializerSettings();
        this.dateTimeFormat = settings.DateTimeFormat;
        this.culture = settings.Culture;
        this.config = this.CreateConfiguration(settings);
    }

    public CsvSerializer(CsvConfiguration configuration)
    {
        this.config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        this.culture = configuration.CultureInfo;
    }

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

    protected virtual void ConfigureReader(CsvReader csv)
    {
        csv.Context.TypeConverterCache.AddConverter<DateTime>(
            new CustomDateTimeConverter(this.dateTimeFormat, this.culture));

        foreach (var mapType in this.classMaps)
        {
            csv.Context.RegisterClassMap(mapType);
        }
    }

    protected virtual void ConfigureWriter(CsvWriter csv)
    {
        csv.Context.TypeConverterCache.AddConverter<DateTime>(
            new CustomDateTimeConverter(this.dateTimeFormat, this.culture));

        foreach (var mapType in this.classMaps)
        {
            csv.Context.RegisterClassMap(mapType);
        }
    }

    public virtual void RegisterClassMap<T>() where T : ClassMap
    {
        this.classMaps.Add(typeof(T));
    }
}

public sealed class CsvSerializerSettings
{
    public string Delimiter { get; init; } = ";";
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    public string DateTimeFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";
    public Dictionary<string, string> HeaderMappings { get; init; }
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);
    public List<Type> ClassMaps { get; init; } = [];
}

public sealed class CustomDateTimeConverter : DefaultTypeConverter
{
    private readonly string format;
    private readonly CultureInfo culture;

    public CustomDateTimeConverter(string format, CultureInfo culture)
    {
        this.format = format;
        this.culture = culture;
    }

    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return DateTime.ParseExact(text, this.format, this.culture);
    }

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString(this.format, this.culture);
        }

        return base.ConvertToString(value, row, memberMapData);
    }
}