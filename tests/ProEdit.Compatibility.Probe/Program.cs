using System.Reflection;

var expected = new[]
{
    "Vcc.Office.Writer",
    "ProEdit.Documents",
    "ProEdit.Editing",
    "ProEdit.Layout",
    "ProEdit.Word.Editor",
    "ProEdit.Word.Avalonia",
    "ProEdit.OpenXml",
    "Avalonia.Controls"
};

var failures = new List<string>();
foreach (var name in expected)
{
    try
    {
        var assembly = Assembly.Load(new AssemblyName(name));
        var publicTypes = assembly.GetExportedTypes();
        Console.WriteLine($"PASS {name}: {assembly.GetName().Version}; {publicTypes.Length} public types");
        if (publicTypes.Length == 0)
            failures.Add($"{name}: no exported public types");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {name}: {ex}");
    }
}
if (failures.Count > 0)
{
    Console.Error.WriteLine($"Compatibility probe failed with {failures.Count} issue(s).");
    return 1;
}
Console.WriteLine("PASS: VCC Writer and ProEdit dependencies resolve together on .NET 10.");
Console.WriteLine("NOTE: Editor behavior, DOCX fidelity and rendering are NOT tested.");
return 0;
