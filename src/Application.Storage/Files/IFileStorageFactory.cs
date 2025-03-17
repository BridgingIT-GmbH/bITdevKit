// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;

public interface IFileStorageFactory
{
    IFileStorageProvider CreateProvider(string name);

    IFileStorageProvider CreateProvider<TImplementation>() where TImplementation : IFileStorageProvider;

    IFileStorageFactory RegisterProvider(string name, Action<FileStorageFactory.FileStorageBuilder> configure);

    IFileStorageFactory WithBehavior(string providerName, Func<IFileStorageProvider, IServiceProvider, IFileStorageBehavior> behaviorFactory);
}