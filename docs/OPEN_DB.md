# Opening the Dev SQLite database

This project stores a development SQLite DB at `src/FileService.Api/bin/Debug/net8.0/dev-files.db` when EF persistence is enabled (note: the app uses an in-memory repo in Development by default).

Quick ways to open the DB:

- Recommended (VS Code): Install the "SQLite" extension by alexcvzz or "SQLite Viewer" then open the `.db` file directly from VS Code Explorer.

- Use DB Browser for SQLite (GUI): https://sqlitebrowser.org/ — once installed, run the task in VS Code (Terminal → Run Task → "Open Dev SQLite DB") or double-click the `.db` file.

- Command-line sqlite3 (if installed):

```powershell
# Open DB with sqlite3
sqlite3 "src\FileService.Api\bin\Debug\net8.0\dev-files.db"

# List tables
.tables

# Inspect records
SELECT * FROM FileRecords LIMIT 10;
```

Notes:
- By default the project runs with `ASPNETCORE_ENVIRONMENT=Development` and the app uses an in-memory repository. To have a persisted DB, run the app in `Staging` or `Production` or set `Persistence:UseEf=true` and `EnvironmentMode` appropriately.
- When running in Development the SQLite file may not exist. The helper script `scripts/open-dev-db.ps1` will tell you if the file was not found.
