# VCC-0825 — Generic host consolidation

The Desktop Launcher has no direct dependency on Writer, Sheets, Slides or Data and never instantiates application-specific windows. Plugins provide all UI metadata via IOfficePlugin discovery. The CoreShell legacy catalog remains available for backward compatibility but is disabled in Launcher mode.

A Launcher can be constructed against any Plugins root. Smoke tests exercise zero plugins, a single Writer plugin, and the full set. Malformed manifest errors are recorded by the plugin host rather than crashing discovery. The obsolete WPF-migration warning is removed.
