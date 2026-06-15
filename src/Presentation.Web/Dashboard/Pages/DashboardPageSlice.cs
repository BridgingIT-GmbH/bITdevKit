// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;

using RazorSlices;

/// <summary>
/// Base RazorSlice for dashboard pages that use the shared dashboard layout.
/// </summary>
/// <example>
/// <code>
/// @inherits DashboardPageSlice
/// @functions { public override string PageTitle =&gt; "Overview"; }
/// </code>
/// </example>
public abstract class DashboardPageSlice : RazorSlice, IUsesLayout<_DashboardLayout, DashboardLayoutModel>
{
    /// <summary>
    /// Gets the page title used by the dashboard layout.
    /// </summary>
    public virtual string PageTitle => string.Empty;

    /// <summary>
    /// Gets the optional product name shown by the dashboard layout.
    /// </summary>
    public virtual string ProductName => null;

    /// <summary>
    /// Gets whether the dashboard sidebar should be hidden.
    /// </summary>
    public virtual bool HideSideBar => false;

    /// <summary>
    /// Gets convenience helpers for dashboard pages.
    /// </summary>
    public DashboardSliceHelper Dashboard => new(this.HttpContext);

    /// <summary>
    /// Gets the dashboard layout model.
    /// </summary>
    public DashboardLayoutModel LayoutModel => new()
    {
        ProductName = this.ProductName,
        Title = this.PageTitle,
        HideSideBar = this.HideSideBar
    };
}

/// <summary>
/// Base RazorSlice for dashboard content fragments.
/// </summary>
/// <example>
/// <code>
/// @inherits DashboardContentSlice
/// </code>
/// </example>
public abstract class DashboardContentSlice : RazorSlice
{
    /// <summary>
    /// Gets convenience helpers for dashboard content fragments.
    /// </summary>
    public DashboardSliceHelper Dashboard => new(this.HttpContext);
}

