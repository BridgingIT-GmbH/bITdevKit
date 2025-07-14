// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using System.Reflection;

public class TraceActivityDecorator<TDecorated> : DispatchProxy
    where TDecorated : class
{
    private ActivitySource activitySources;
    private bool decorateAllMethods = true;
    private TDecorated ínner;
    private IActivityNamingSchema namingSchema = new MethodFullNameSchema();

    /// <summary>
    ///     Creates a new ActivityDecorator instance wrapping the specific instance (inner) and implementing the TDecorated
    ///     interface
    /// </summary>
    /// <returns></returns>
    public static TDecorated Create(
        TDecorated inner,
        IActivityNamingSchema activityNamingSchema = null,
        bool decorateAllMethods = true)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        object proxy = Create<TDecorated, TraceActivityDecorator<TDecorated>>()!;
        ((TraceActivityDecorator<TDecorated>)proxy).SetParameters(inner, activityNamingSchema, decorateAllMethods);

        return (TDecorated)proxy;
    }

    protected override object Invoke(MethodInfo method, object[] args)
    {
        var noActivityAttribute = method.GetCustomAttribute<NoTraceActivityAttribute>(false);
        var traceActivityAttribute = method.GetCustomAttribute<TraceActivityAttribute>(false);

        if (noActivityAttribute is null && (this.decorateAllMethods || traceActivityAttribute != null))
        {
            var name = this.namingSchema.GetName(this.ínner.GetType(), method);
            using var activity = this.activitySources.StartActivity(traceActivityAttribute?.Name ?? name);

            TraceActivityHelper.AddMethodTags(activity, method);
            TraceActivityHelper.AddAttributeTags(activity, method, this.ínner.GetType());

            if (traceActivityAttribute?.RecordExceptions == false)
            {
                return this.InvokeMethod(method, args);
            }

            return this.WrapWithRecordException(activity, () => this.InvokeMethod(method, args));
        }

        return this.InvokeMethod(method, args);
    }

    private object InvokeMethod(MethodInfo method, object[] args)
    {
        return method.Invoke(this.ínner, args);
    }

    private object WrapWithRecordException(Activity activity, Func<object> invocation)
    {
        try
        {
            return invocation();
        }
        catch (Exception e)
        {
            activity.AddException(e);

            throw;
        }
    }

    private void SetParameters(TDecorated decorated, IActivityNamingSchema spanNamingSchema, bool decorateAllMethods)
    {
        this.ínner = decorated;
        this.activitySources = new ActivitySource(this.ínner!.GetType().FullName!);
        this.decorateAllMethods = decorateAllMethods;

        if (spanNamingSchema != null)
        {
            this.namingSchema = spanNamingSchema;
        }
    }
}