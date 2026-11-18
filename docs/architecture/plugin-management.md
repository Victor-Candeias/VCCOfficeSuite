# VCC-0826 — Plugin inventory and activation

The Launcher exposes a **Plugins** management view containing loaded plugin names, IDs, assembly versions, direct assembly dependencies, compatibility level (manifest API v1), and loading errors. Plugins can be activated or deactivated without changing the host executable. A disabled plugin is hidden and cannot create new windows. Open plugin windows are closed when disabled.

Disabled IDs persist as a JSON array at LocalApplicationData/VCC Office/plugins-state.json. Test roots use a root-local .plugins-state.json for isolation. The plugin DLL remains loaded until the suite exits; this is an intentional .NET AssemblyLoadContext limitation and is shown to the user. Re-enabling restores the plugin without restarting.

Only trusted plugins should be installed, since plugin code runs with the user's permissions.
