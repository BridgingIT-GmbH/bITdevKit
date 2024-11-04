// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class ResultSettings
{
    public IResultLogger Logger { get; set; }

    public Func<string, Exception, ExceptionError> ExceptionErrorFactory { get; set; }
}

public class ResultSettingsBuilder
{
    public IResultLogger Logger { get; set; }

    public Func<string, Exception, ExceptionError> ExceptionErrorFactory { get; set; }

    public ResultSettingsBuilder()
    {
        this.Logger = new NullLogger();
        this.ExceptionErrorFactory = (message, exception) => new ExceptionError(exception, message);
    }

    public ResultSettings Build()
    {
        return new ResultSettings
        {
            Logger = this.Logger ?? new NullLogger(),
            ExceptionErrorFactory = this.ExceptionErrorFactory
        };
    }
}