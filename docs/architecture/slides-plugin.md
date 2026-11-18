# VCC-0823 — Slides as an autonomous DLL plugin

Vcc.Office.Slides now owns SlidesWindow, SlidesEngine, slide models, plugin icon and user-facing metadata. Plugin discovery uses `Plugins/Slides/plugin.json` and IOfficePlugin. The host has no reference to Vcc.Office.Slides and does not instantiate its windows directly.

Existing presentation persistence, notes, transitions and exports remain in the migrated service. Tests continue to exercise the editor and export contracts under the new assembly.
