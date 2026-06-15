// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

public sealed class WeatherForecastExportProfile : ExportProfileBase<WeatherForecast>
{
    protected override void Configure()
    {
        this.ToSheet("Weather Forecasts");

        this.ForColumn(forecast => forecast.Id)
            .HasName("Id")
            .HasOrder(0)
            .UseConverter(new EntityIdExportConverter());

        this.Ignore(forecast => forecast.CityId);
        this.Ignore(forecast => forecast.HourlyForecasts);
        this.Ignore(forecast => forecast.DomainEvents);
    }
}

public sealed class EntityIdExportConverter : IValueConverter
{
    public object ConvertToExport(object value, ValueConversionContext context)
    {
        if (value is null)
        {
            return null;
        }

        var rawValue = value.GetType().GetProperty("Value")?.GetValue(value);
        return rawValue?.ToString() ?? value.ToString();
    }

    public object ConvertFromImport(object value, ValueConversionContext context)
    {
        return value;
    }
}
