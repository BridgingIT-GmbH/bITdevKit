// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

internal static class NdjsonDiagnostics
{
    internal static void Report(ILogger logger, string reason)
    {
        try { logger?.LogWarning("[Ndjson] stream reporting failure (reason={ReasonCode})", reason); }
        catch (Exception) { /* Diagnostics cannot change a finalized transport outcome. */ }
    }

    internal static void Finished(ILogger logger, NdjsonStreamOutcome outcome)
    {
        try
        {
            logger?.Log(outcome.Status == NdjsonStreamStatus.Failed ? LogLevel.Warning : LogLevel.Debug,
                "[Ndjson] stream finished (status={Status}, reason={ReasonCode}, stage={FailureStage}, sourceCompleted={SourceCompleted}, cleanupCompleted={CleanupCompleted}, writtenRecords={WrittenRecordCount}, writtenBytes={WrittenFramedByteCount}, flushedRecords={FlushedRecordCount}, flushedBytes={FlushedFramedByteCount}, partialWritePossible={PartialWritePossible})",
                outcome.Status, outcome.ReasonCode, outcome.FailureStage, outcome.SourceCompleted, outcome.CleanupCompleted,
                outcome.WrittenRecordCount, outcome.WrittenFramedByteCount, outcome.FlushedRecordCount, outcome.FlushedFramedByteCount, outcome.PartialWritePossible);
        }
        catch (Exception) { /* A logger failure is never an output failure. */ }
    }
}
