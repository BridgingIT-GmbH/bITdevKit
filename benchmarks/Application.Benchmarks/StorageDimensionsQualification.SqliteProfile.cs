// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Globalization;
using Microsoft.Data.Sqlite;
using SQLitePCL;

public static partial class StorageDimensionsQualification
{
    private static object ProfileSqliteQuery(SqliteConnection connection, QualificationQuery query)
    {
        var database = connection.Handle;
        Check(raw.sqlite3_prepare_v2(database, query.Sql, out var statement));
        using (statement)
        {
            if (raw.sqlite3_stmt_readonly(statement) != 1) throw new InvalidOperationException("Native profiling accepts only read-only queries.");
            foreach (var parameter in query.Parameters)
            {
                var index = raw.sqlite3_bind_parameter_index(statement, parameter.Name);
                if (index == 0) throw new InvalidOperationException("Native profile parameter was not found.");
                Check(parameter.Value switch
                {
                    null or DBNull => raw.sqlite3_bind_null(statement, index),
                    byte[] bytes => raw.sqlite3_bind_blob(statement, index, bytes),
                    string text => raw.sqlite3_bind_text(statement, index, text),
                    decimal number => raw.sqlite3_bind_text(statement, index, number.ToString(CultureInfo.InvariantCulture)),
                    DateTimeOffset date => raw.sqlite3_bind_text(statement, index, date.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture)),
                    DateTime date => raw.sqlite3_bind_text(statement, index, date.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)),
                    Guid guid => raw.sqlite3_bind_text(statement, index, guid.ToString("D").ToUpperInvariant()),
                    bool flag => raw.sqlite3_bind_int64(statement, index, flag ? 1 : 0),
                    int or long or short or byte or Enum => raw.sqlite3_bind_int64(statement, index, Convert.ToInt64(parameter.Value, CultureInfo.InvariantCulture)),
                    _ => throw new InvalidOperationException("Unsupported native profile parameter kind.")
                });
            }

            var before = Counters();
            var rows = 0;
            int status;
            while ((status = raw.sqlite3_step(statement)) == raw.SQLITE_ROW) rows++;
            if (status != raw.SQLITE_DONE) Check(status);
            var after = Counters();
            return new
            {
                query.CaseName, ReturnedRows = rows,
                FullScanSteps = raw.sqlite3_stmt_status(statement, raw.SQLITE_STMTSTATUS_FULLSCAN_STEP, 0),
                SortOperations = raw.sqlite3_stmt_status(statement, raw.SQLITE_STMTSTATUS_SORT, 0),
                AutomaticIndexRows = raw.sqlite3_stmt_status(statement, raw.SQLITE_STMTSTATUS_AUTOINDEX, 0),
                VirtualMachineSteps = raw.sqlite3_stmt_status(statement, raw.SQLITE_STMTSTATUS_VM_STEP, 0),
                StatementHeapBytes = raw.sqlite3_stmt_status(statement, 99, 0),
                PagerCacheHits = Delta(7), PagerCacheMisses = Delta(8), WrittenPages = Delta(9),
                MidTransactionCacheSpills = Delta(12), TemporarySpillBytes = Delta(13),
                Note = "Separate profiling execution through the exact Microsoft.Data.Sqlite native handle; no APSW execution or timed-interval profiling. Null counters mean this native build does not expose the verb."
            };

            long? Delta(int verb) => before[verb] is { } first && after[verb] is { } last ? (long)last - first : null;
        }

        Dictionary<int, int?> Counters() => new[] { 7, 8, 9, 12, 13 }.ToDictionary(verb => verb,
            verb => raw.sqlite3_db_status(database, verb, out var current, out _, 0) == raw.SQLITE_OK ? (int?)current : null);

        void Check(int result)
        {
            if (result != raw.SQLITE_OK) throw new InvalidOperationException("Native SQLite profiling failed: " + raw.sqlite3_errmsg(database).utf8_to_string());
        }
    }
}
