# VCC-0824 — Data as an autonomous DLL plugin

Vcc.Office.Data owns DataWindow, DataEngine, Data models, and the self-describing IOfficePlugin icon/tooltip. The launcher locates `Plugins/Data/plugin.json` without a project reference to Data.

DataEngine continues using shared CoreShell CoreDatabase and StoragePaths primitives for the same persistent SQLite database path. Forms, reports, imports and read-only query validation are preserved. Tests execute those services and the dynamically loaded window.
