// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Reflection;
using Application.Storage;

[UnitTest("Application")]
public sealed class BlobStorageSourceGuardTests
{
    [Theory]
    [InlineData(typeof(BlobUpload))]
    [InlineData(typeof(BlobInfo))]
    [InlineData(typeof(BlobPropertiesUpdate))]
    public void PublicBlobModels_DoNotExposeTransformMetadata(Type model)
    {
        model.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ShouldNotContain(property => property.Name == "TransformMetadata");
    }
}
