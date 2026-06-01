// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Enumeration representing temperature units (Celsius, Fahrenheit).
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class TemperatureUnit : Enumeration
{
    /// <summary>Celsius temperature unit.</summary>
    public static readonly TemperatureUnit Celsius = new(0, nameof(Celsius), "°C");

    /// <summary>Fahrenheit temperature unit.</summary>
    public static readonly TemperatureUnit Fahrenheit = new(1, nameof(Fahrenheit), "°F");

    /// <summary>Gets the display symbol for this unit.</summary>
    public string Symbol { get; private set; }
}

/// <summary>
/// Enumeration representing wind speed units.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class WindSpeedUnit : Enumeration
{
    /// <summary>Kilometers per hour.</summary>
    public static readonly WindSpeedUnit Kmh = new(0, nameof(Kmh), "km/h");

    /// <summary>Miles per hour.</summary>
    public static readonly WindSpeedUnit Mph = new(1, nameof(Mph), "mph");

    /// <summary>Meters per second.</summary>
    public static readonly WindSpeedUnit Ms = new(2, nameof(Ms), "m/s");

    /// <summary>Knots.</summary>
    public static readonly WindSpeedUnit Knots = new(3, nameof(Knots), "kn");

    /// <summary>Gets the display symbol for this unit.</summary>
    public string Symbol { get; private set; }
}

/// <summary>
/// Enumeration representing WMO weather condition codes with descriptions and icons.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class WeatherConditionCode : Enumeration
{
    // WMO Weather interpretation codes (WW)
    /// <summary>Clear sky.</summary>
    public static readonly WeatherConditionCode ClearSky = new(0, "Clear sky", "☀️");

    /// <summary>Mainly clear.</summary>
    public static readonly WeatherConditionCode MainlyClear = new(1, "Mainly clear", "🌤️");

    /// <summary>Partly cloudy.</summary>
    public static readonly WeatherConditionCode PartlyCloudy = new(2, "Partly cloudy", "⛅");

    /// <summary>Overcast.</summary>
    public static readonly WeatherConditionCode Overcast = new(3, "Overcast", "☁️");

    /// <summary>Fog.</summary>
    public static readonly WeatherConditionCode Fog = new(45, "Fog", "🌫️");

    /// <summary>Depositing rime fog.</summary>
    public static readonly WeatherConditionCode DepositingRimeFog = new(48, "Depositing rime fog", "🌫️");

    /// <summary>Light drizzle.</summary>
    public static readonly WeatherConditionCode LightDrizzle = new(51, "Light drizzle", "🌦️");

    /// <summary>Moderate drizzle.</summary>
    public static readonly WeatherConditionCode ModerateDrizzle = new(53, "Moderate drizzle", "🌦️");

    /// <summary>Dense drizzle.</summary>
    public static readonly WeatherConditionCode DenseDrizzle = new(55, "Dense drizzle", "🌧️");

    /// <summary>Light freezing drizzle.</summary>
    public static readonly WeatherConditionCode LightFreezingDrizzle = new(56, "Light freezing drizzle", "🌧️❄️");

    /// <summary>Dense freezing drizzle.</summary>
    public static readonly WeatherConditionCode DenseFreezingDrizzle = new(57, "Dense freezing drizzle", "🌧️❄️");

    /// <summary>Slight rain.</summary>
    public static readonly WeatherConditionCode SlightRain = new(61, "Slight rain", "🌧️");

    /// <summary>Moderate rain.</summary>
    public static readonly WeatherConditionCode ModerateRain = new(63, "Moderate rain", "🌧️");

    /// <summary>Heavy rain.</summary>
    public static readonly WeatherConditionCode HeavyRain = new(65, "Heavy rain", "🌧️");

    /// <summary>Light freezing rain.</summary>
    public static readonly WeatherConditionCode LightFreezingRain = new(66, "Light freezing rain", "🌧️❄️");

    /// <summary>Heavy freezing rain.</summary>
    public static readonly WeatherConditionCode HeavyFreezingRain = new(67, "Heavy freezing rain", "🌧️❄️");

    /// <summary>Slight snow fall.</summary>
    public static readonly WeatherConditionCode SlightSnowFall = new(71, "Slight snow fall", "🌨️");

    /// <summary>Moderate snow fall.</summary>
    public static readonly WeatherConditionCode ModerateSnowFall = new(73, "Moderate snow fall", "🌨️");

    /// <summary>Heavy snow fall.</summary>
    public static readonly WeatherConditionCode HeavySnowFall = new(75, "Heavy snow fall", "🌨️");

    /// <summary>Snow grains.</summary>
    public static readonly WeatherConditionCode SnowGrains = new(77, "Snow grains", "🌨️");

    /// <summary>Slight rain showers.</summary>
    public static readonly WeatherConditionCode SlightRainShowers = new(80, "Slight rain showers", "🌦️");

    /// <summary>Moderate rain showers.</summary>
    public static readonly WeatherConditionCode ModerateRainShowers = new(81, "Moderate rain showers", "🌧️");

    /// <summary>Violent rain showers.</summary>
    public static readonly WeatherConditionCode ViolentRainShowers = new(82, "Violent rain showers", "🌧️");

    /// <summary>Slight snow showers.</summary>
    public static readonly WeatherConditionCode SlightSnowShowers = new(85, "Slight snow showers", "🌨️");

    /// <summary>Heavy snow showers.</summary>
    public static readonly WeatherConditionCode HeavySnowShowers = new(86, "Heavy snow showers", "🌨️");

    /// <summary>Thunderstorm.</summary>
    public static readonly WeatherConditionCode ThunderstormSlight = new(95, "Thunderstorm", "⛈️");

    /// <summary>Thunderstorm with slight hail.</summary>
    public static readonly WeatherConditionCode ThunderstormModerateHail = new(96, "Thunderstorm with slight hail", "⛈️🧊");

    /// <summary>Thunderstorm with heavy hail.</summary>
    public static readonly WeatherConditionCode ThunderstormHeavyHail = new(99, "Thunderstorm with heavy hail", "⛈️🧊");

    /// <summary>Gets the human-readable description of this weather condition.</summary>
    public string Description { get; private set; }

    /// <summary>Gets the emoji icon for this weather condition.</summary>
    public string Icon { get; private set; }
}

/// <summary>
/// Enumeration representing weather alert types.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class AlertType : Enumeration
{
    /// <summary>Thunderstorm alert (WMO codes 95, 96, 99).</summary>
    public static readonly AlertType Thunderstorm = new(1, nameof(Thunderstorm), "Thunderstorm detected (WMO codes 95, 96, 99)");

    /// <summary>Hail alert (WMO codes 96, 99).</summary>
    public static readonly AlertType Hail = new(2, nameof(Hail), "Hail detected (WMO codes 96, 99)");

    /// <summary>Severe wind alert (greater than 80 km/h).</summary>
    public static readonly AlertType SevereWind = new(3, nameof(SevereWind), "Severe wind (>80 km/h)");

    /// <summary>Extreme heat alert (greater than 40°C).</summary>
    public static readonly AlertType ExtremeHeat = new(4, nameof(ExtremeHeat), "Extreme heat (>40°C)");

    /// <summary>Blizzard conditions (WMO 71-77 with wind greater than 50 km/h).</summary>
    public static readonly AlertType Blizzard = new(5, nameof(Blizzard), "Blizzard conditions (WMO 71-77 + wind >50 km/h)");

    /// <summary>Hurricane force winds (greater than 118 km/h).</summary>
    public static readonly AlertType Hurricane = new(6, nameof(Hurricane), "Hurricane force winds (>118 km/h)");

    /// <summary>Gets the human-readable description of this alert type.</summary>
    public string Description { get; private set; }
}

/// <summary>
/// Enumeration representing weather alert severity levels.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class AlertSeverity : Enumeration
{
    /// <summary>Warning severity level.</summary>
    public static readonly AlertSeverity Warning = new(1, nameof(Warning));

    /// <summary>Severe severity level.</summary>
    public static readonly AlertSeverity Severe = new(2, nameof(Severe));

    /// <summary>Extreme severity level.</summary>
    public static readonly AlertSeverity Extreme = new(3, nameof(Extreme));
}

/// <summary>
/// Enumeration representing weather recommendation categories.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class RecommendationCategory : Enumeration
{
    /// <summary>Precipitation-related recommendation.</summary>
    public static readonly RecommendationCategory Precipitation = new(1, nameof(Precipitation));

    /// <summary>UV-related recommendation.</summary>
    public static readonly RecommendationCategory UV = new(2, nameof(UV));

    /// <summary>Temperature-related recommendation.</summary>
    public static readonly RecommendationCategory Temperature = new(3, nameof(Temperature));

    /// <summary>Wind-related recommendation.</summary>
    public static readonly RecommendationCategory Wind = new(4, nameof(Wind));

    /// <summary>Storm-related recommendation.</summary>
    public static readonly RecommendationCategory Storm = new(5, nameof(Storm));

    /// <summary>General recommendation.</summary>
    public static readonly RecommendationCategory General = new(6, nameof(General));
}

/// <summary>
/// Enumeration representing recommendation severity levels.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class RecommendationSeverity : Enumeration
{
    /// <summary>Informational severity.</summary>
    public static readonly RecommendationSeverity Info = new(1, nameof(Info));

    /// <summary>Caution severity.</summary>
    public static readonly RecommendationSeverity Caution = new(2, nameof(Caution));

    /// <summary>Warning severity.</summary>
    public static readonly RecommendationSeverity Warning = new(3, nameof(Warning));
}

/// <summary>
/// Enumeration representing subscription plans with feature details.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class SubscriptionPlan : Enumeration
{
    /// <summary>Free plan with basic features.</summary>
    public static readonly SubscriptionPlan Free = new(0, nameof(Free), "Basic free plan",
        new SubscriptionPlanDetails(3, 7, false, false));

    /// <summary>Basic plan for individuals.</summary>
    public static readonly SubscriptionPlan Basic = new(1, nameof(Basic), "Standard plan for individuals",
        new SubscriptionPlanDetails(10, 16, true, true));

    /// <summary>Professional plan.</summary>
    public static readonly SubscriptionPlan Pro = new(2, nameof(Pro), "Professional plan",
        new SubscriptionPlanDetails(25, 16, true, true));

    /// <summary>Enterprise plan with unlimited features.</summary>
    public static readonly SubscriptionPlan Enterprise = new(3, nameof(Enterprise), "Enterprise plan",
        new SubscriptionPlanDetails(-1, 16, true, true));

    /// <summary>Gets the human-readable description of this plan.</summary>
    public string Description { get; private set; }

    /// <summary>Gets the feature details for this plan.</summary>
    public SubscriptionPlanDetails Details { get; private set; }
}

/// <summary>
/// Represents the feature details for a subscription plan.
/// </summary>
/// <param name="maxCities">Maximum number of cities allowed (-1 for unlimited).</param>
/// <param name="maxForecastDays">Maximum number of forecast days allowed.</param>
/// <param name="allowsComparison">Whether city comparison is allowed.</param>
/// <param name="allowsExport">Whether data export is allowed.</param>
public class SubscriptionPlanDetails(int maxCities, int maxForecastDays, bool allowsComparison, bool allowsExport)
{
    /// <summary>Gets the maximum number of cities allowed (-1 for unlimited).</summary>
    public int MaxCities { get; } = maxCities;

    /// <summary>Gets the maximum number of forecast days allowed.</summary>
    public int MaxForecastDays { get; } = maxForecastDays;

    /// <summary>Gets a value indicating whether city comparison is allowed.</summary>
    public bool AllowsComparison { get; } = allowsComparison;

    /// <summary>Gets a value indicating whether data export is allowed.</summary>
    public bool AllowsExport { get; } = allowsExport;
}

/// <summary>
/// Enumeration representing subscription status values.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class SubscriptionStatus : Enumeration
{
    /// <summary>Subscription is awaiting activation.</summary>
    public static readonly SubscriptionStatus Pending = new(0, nameof(Pending), true, "Subscription is awaiting activation");

    /// <summary>Subscription is active.</summary>
    public static readonly SubscriptionStatus Active = new(1, nameof(Active), true, "Subscription is active");

    /// <summary>Subscription has been cancelled.</summary>
    public static readonly SubscriptionStatus Cancelled = new(2, nameof(Cancelled), true, "Subscription has been cancelled");

    /// <summary>Subscription has expired.</summary>
    public static readonly SubscriptionStatus Expired = new(3, nameof(Expired), true, "Subscription has expired");

    /// <summary>Gets a value indicating whether this status is enabled.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Gets the human-readable description of this status.</summary>
    public string Description { get; private set; }
}

/// <summary>
/// Enumeration representing subscription billing cycle options.
/// </summary>
[DebuggerDisplay("Id={Id}, Value={Value}")]
public partial class SubscriptionBillingCycle : Enumeration
{
    /// <summary>No recurring billing.</summary>
    public static readonly SubscriptionBillingCycle Never = new(0, nameof(Never), false, "No recurring billing", false);

    /// <summary>Monthly billing cycle.</summary>
    public static readonly SubscriptionBillingCycle Monthly = new(1, nameof(Monthly), true, "Monthly billing cycle", true);

    /// <summary>Annual billing cycle.</summary>
    public static readonly SubscriptionBillingCycle Yearly = new(2, nameof(Yearly), true, "Annual billing cycle", true);

    /// <summary>Gets a value indicating whether this billing cycle is enabled.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Gets the human-readable description of this billing cycle.</summary>
    public string Description { get; private set; }

    /// <summary>Gets a value indicating whether auto-renewal is enabled.</summary>
    public bool AutoRenew { get; private set; }
}
