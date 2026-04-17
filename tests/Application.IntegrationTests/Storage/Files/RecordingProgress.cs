// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

internal sealed class RecordingProgress<T> : IProgress<T>
{
    private readonly List<T> items = [];

    public IReadOnlyList<T> Items
    {
        get
        {
            lock (this.items)
            {
                return [.. this.items];
            }
        }
    }

    public void Report(T value)
    {
        lock (this.items)
        {
            this.items.Add(value);
        }
    }
}
