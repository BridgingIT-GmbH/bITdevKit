// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Errors;

using System.Collections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

/// <summary>Captures allowlisted headers when the native exception middleware clears a response.</summary>
public sealed class ResponseProblemDetailsHeaderSnapshot(IOptions<ProblemDetailsOptions> options) : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // ASP.NET Core clears the response before dispatching to IExceptionHandler.
        app.Use(async (context, downstream) =>
        {
            var feature = context.Features.GetRequiredFeature<IHttpResponseFeature>();
            var tracked = new TrackedHeaders(feature.Headers, options.Value.AllowedHeaderNames);
            feature.Headers = tracked;
            try
            {
                await downstream(context);
            }
            finally
            {
                if (ReferenceEquals(feature.Headers, tracked))
                {
                    feature.Headers = tracked.Original;
                }
            }
        });
        next(app);
    };

    /// <summary>Restores captured headers once after native exception dispatch.</summary>
    public static void Restore(HttpContext context)
    {
        if (context.Features.Get<IExceptionHandlerFeature>() is not null &&
            context.Response.Headers is TrackedHeaders tracked)
        {
            tracked.Restore();
        }
    }

    private sealed class TrackedHeaders(IHeaderDictionary original, ISet<string> allowed) : IHeaderDictionary
    {
        private KeyValuePair<string, StringValues>[] saved;

        public IHeaderDictionary Original { get; } = original;

        public void Restore()
        {
            if (this.saved is { } headers)
            {
                foreach (var header in headers)
                {
                    this.Original.TryAdd(header.Key, header.Value);
                }

                this.saved = null;
            }
        }

        public void Clear()
        {
            this.saved = this.Original.Where(header => allowed.Contains(header.Key)).ToArray();
            this.Original.Clear();
        }

        public long? ContentLength { get => this.Original.ContentLength; set => this.Original.ContentLength = value; }

        public StringValues this[string key] { get => this.Original[key]; set => this.Original[key] = value; }

        StringValues IDictionary<string, StringValues>.this[string key]
        {
            get => ((IDictionary<string, StringValues>)this.Original)[key];
            set => this.Original[key] = value;
        }

        public ICollection<string> Keys => this.Original.Keys;

        public ICollection<StringValues> Values => this.Original.Values;

        public int Count => this.Original.Count;

        public bool IsReadOnly => this.Original.IsReadOnly;

        public void Add(string key, StringValues value) => ((IDictionary<string, StringValues>)this.Original).Add(key, value);

        public void Add(KeyValuePair<string, StringValues> item) => this.Original.Add(item);

        public bool ContainsKey(string key) => this.Original.ContainsKey(key);

        public bool Contains(KeyValuePair<string, StringValues> item) => this.Original.Contains(item);

        public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex) => this.Original.CopyTo(array, arrayIndex);

        public bool Remove(string key) => this.Original.Remove(key);

        public bool Remove(KeyValuePair<string, StringValues> item) => this.Original.Remove(item);

        public bool TryGetValue(string key, out StringValues value) => this.Original.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => this.Original.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
