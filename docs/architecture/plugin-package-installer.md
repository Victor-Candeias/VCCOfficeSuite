# VCC-0827 — Independent plugin packages and updates

The release's existing OS artifacts include `PluginPackages/{Plugin}.zip` alongside `Plugins/{Plugin}/`. Each ZIP has `plugin.json` and the entry assembly at the ZIP root, plus required private dependencies.

The **Plugins** manager can stage installation, upgrade or removal independently of the VCC Office Suite executable. User-selected ZIPs are checked for API compatibility, manifest identity, entry DLL, unsafe paths, symlinks, file count and expanded-size limits. Only trusted plugins should be installed: **packages are not digitally signed or sandboxed**.

An operation is recorded under `Plugins/.vcc-updates/queue.json` and applied on the next startup **before DLL discovery**. Updates use a same-volume staged folder, an atomic rename and a rollback copy. Existing loaded DLLs are never replaced while the process is running; uninstall and upgrades require restarting the suite. Failures remain queued and surface in the plugin manager. The ZIPs are portable per OS and must match the user's platform.

Automated smoke tests check installation, upgrade, removal and rejection of traversal ZIP entries. This initial installer is intended for writable, standalone distributions; a read-only system-level installation needs its own privileged update service in future work.
