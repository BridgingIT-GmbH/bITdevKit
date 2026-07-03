// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Holds business calendar registrations.
/// </summary>
/// <remarks><example><code>var options = new BusinessCalendarOptions();</code></example></remarks>
public sealed class BusinessCalendarOptions
{
    private readonly List<BusinessCalendarRegistration> registrations = [];

    /// <summary>Gets the registered calendars.</summary>
    public IReadOnlyList<BusinessCalendarRegistration> Registrations => this.registrations;

    /// <summary>Gets or sets the default calendar instance.</summary>
    public IBusinessCalendar DefaultCalendar { get; set; } = new BusinessCalendar();

    /// <summary>Gets or sets the service-backed default calendar factory.</summary>
    public Func<IServiceProvider, IBusinessCalendar> DefaultCalendarFactory { get; set; }

    internal void Add(BusinessCalendarRegistration registration) => this.registrations.Add(registration);
}
