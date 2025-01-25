// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

// TODO: get rid of Newtonsoft dependency

public static class DefaultJsonNetSerializerSettings
{
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