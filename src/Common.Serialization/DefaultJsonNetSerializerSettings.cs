// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

public static class DefaultJsonNetSerializerSettings
{
    public static JsonSerializerSettings Create() => new()
    {
        ContractResolver = new PropertyBackingFieldContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        },
        NullValueHandling = NullValueHandling.Ignore,
        TypeNameHandling = TypeNameHandling.Auto,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        DateFormatString = "o",
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        //DateParseHandling = DateParseHandling.DateTimeOffset,
        //DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        Converters = new List<JsonConverter>
        {
            //new GuidConverter(),
            new StringEnumConverter()
            {
                AllowIntegerValues = true
            },
            new IsoDateTimeConverter
            {
                DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ" // utc, no timezone offset (+0:00)
            }
        }
    };
}