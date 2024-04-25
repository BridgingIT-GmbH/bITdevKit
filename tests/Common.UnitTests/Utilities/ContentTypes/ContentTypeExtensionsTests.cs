// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using BridgingIT.DevKit.Common;
using Xunit;

[UnitTest("Common")]
public class ContentTypeExtensionsTests
{
    [Fact]
    public void CanResolveEnum()
    {
        ContentTypeExtensions.FromMimeType("text/csv").ShouldBe(ContentType.CSV);
        ContentTypeExtensions.FromFileName("filename.csV").ShouldBe(ContentType.CSV);
        ContentTypeExtensions.FromFileName("./path/filename.csv").ShouldBe(ContentType.CSV);
        ContentTypeExtensions.FromExtension("cSv").ShouldBe(ContentType.CSV);
        ContentTypeExtensions.FromExtension("xLsx").ShouldBe(ContentType.XLSX); // no FileExtension defined
        ContentTypeExtensions.FromExtension("abcdefg").ShouldBe(ContentType.TXT); // not defined, defaults to TEXT
        ContentTypeExtensions.FromFileName("readme.txt").ShouldBe(ContentType.TXT); // no FileExtension defined
        ContentTypeExtensions.FromFileName("README.md").ShouldBe(ContentType.MD); // no FileExtension defined
    }

    [Fact]
    public void MimeTypeTests()
    {
        ContentType.CSV.MimeType().ShouldBe("text/csv");
        ContentType.TXT.MimeType().ShouldBe("text/plain");
        ContentType.MD.MimeType().ShouldBe("text/markdown");
    }

    [Fact]
    public void FileExtensionTests()
    {
        ContentType.CSV.FileExtension().ShouldBe("csv");
        ContentType.MD.FileExtension().ShouldBe("md");
        ContentType.TXT.FileExtension().ShouldBe("txt");
        ContentType.TEXT.FileExtension().ShouldBe("text");
    }
}