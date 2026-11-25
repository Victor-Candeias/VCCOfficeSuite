# VCC Writer: ProEdit integration spike

This is an **isolated dependency compatibility test**, not adoption of ProEdit as the VCC Writer editing engine.

## Scope
- Uses ProEdit 0.1.0 packages: Word.Avalonia, Word.Editor and OpenXml.
- Builds alongside the existing `Vcc.Office.Writer` plugin on .NET 10.
- Loads Writer, ProEdit and Avalonia assemblies together and enumerates their public types.
- Runs Windows, Linux and macOS in a branch-only GitHub Actions workflow.
- Does not alter the production Writer plugin or main solution.

## Local run
```bash
dotnet restore tests/ProEdit.Compatibility.Probe/ProEdit.Compatibility.Probe.csproj
dotnet run --project tests/ProEdit.Compatibility.Probe/ProEdit.Compatibility.Probe.csproj --configuration Release
```

## Acceptance criteria
- Restore ProEdit packages on all three operating systems.
- Build the probe with the existing VCC Writer project.
- Load Writer, editing, layout, DOCX and Avalonia assemblies without conflicts.

## Not tested: necessary before adoption
- Actual A4 editable surface embedded in the Writer plugin.
- Selection, typing, undo/redo, formatting, tables and images.
- DOCX open-save-reopen and fidelity tests.
- Pagination, page breaks, headers/footers, printing and PDF export.
- Headless/desktop behavior and performance on Windows/Linux/macOS.

Do not merge solely based on this probe.
