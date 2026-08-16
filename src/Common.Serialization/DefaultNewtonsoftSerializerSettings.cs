// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

// TODO: get rid of Newtonsoft dependency

/// <summary>
///     Creates the standard Newtonsoft.Json settings used by DevKit serializers.
/// </summary>
public static class DefaultNewtonsoftSerializerSettings
{
    /// <summary>
    ///     Creates serializer settings with camel-case member names, UTC date handling, and DevKit converters.
    /// </summary>
    /// <returns>A new configured serializer-settings instance.</returns>
    public static JsonSerializerSettings Create()
    {
        return new JsonSerializerSettings
        {
            ContractResolver =
                new PropertyBackingFieldContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ",
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            //DateParseHandling = DateParseHandling.DateTimeOffset,
            //DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            Converters =
            [
                //new GuidConverter(),
                new EnumConverter(),
                new FilterCriteriaConverter(),
                new StringEnumConverter { AllowIntegerValues = true },
                new IsoDateTimeConverter
                {
                    DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ" // utc, no timezone offset (+0:00)
                }
            ]
        };
    }
}
