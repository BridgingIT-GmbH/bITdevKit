"""Inspect only run-owned SQLite files, read-only, with an isolated APSW dbstat build."""

import json
from pathlib import Path
import sys

module_directory, database = map(Path, sys.argv[1:])
sys.path.insert(0, str(module_directory.resolve()))
import apsw

connection = apsw.Connection(str(database.resolve()), flags=apsw.SQLITE_OPEN_READONLY)
try:
    relations = [
        {"Relation": name, "Bytes": size}
        for name, size in connection.execute(
            "SELECT name,SUM(pgsize) FROM dbstat GROUP BY name ORDER BY name"
        )
    ]
    print(json.dumps({
        "Inspector": "APSW read-only file-format inspection; not the benchmark execution engine",
        "ApswVersion": apsw.apswversion(),
        "InspectorSQLiteVersion": apsw.sqlitelibversion(),
        "Relations": relations,
        "UsedBtreeBytes": sum(row["Bytes"] for row in relations),
        "AllocatedPageBytes": next(connection.execute("PRAGMA page_count"))[0]
        * next(connection.execute("PRAGMA page_size"))[0],
    }))
finally:
    connection.close()
