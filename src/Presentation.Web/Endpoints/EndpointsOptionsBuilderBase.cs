// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;










public abstract class EndpointsOptionsBuilderBase<TOptions, TBuilder> : OptionsBuilder<TOptions>
    where TOptions : EndpointsOptionsBase, new()
    where TBuilder : EndpointsOptionsBuilderBase<TOptions, TBuilder>
{








    public TBuilder Enabled(bool enabled = true)
    {
        this.Target.Enabled = enabled;

        return (TBuilder)this;
    }










    public TBuilder GroupPath(string path)
    {
        this.Target.GroupPath = path;

        return (TBuilder)this;
    }









    public TBuilder GroupTag(string tag)
    {
        this.Target.GroupTag = tag;

        return (TBuilder)this;
    }










    public TBuilder RequireAuthorization(bool enabled = true)
    {
        this.Target.RequireAuthorization = enabled;

        return (TBuilder)this;
    }









    public TBuilder ExcludeFromDescription(bool excluded = true)
    {
        this.Target.ExcludeFromDescription = excluded;

        return (TBuilder)this;
    }










    public TBuilder RequireRoles(params string[] roles)
    {
        this.Target.RequireRoles = roles ?? [];

        return (TBuilder)this;
    }









    public TBuilder RequirePolicy(string policy)
    {
        this.Target.RequirePolicy = policy;

        return (TBuilder)this;
    }
}
