namespace Vcc.Office.CoreShell;

/// <summary>
/// Partilha o contrato de sessões WPF, mas preserva o ficheiro original ao
/// importar a última sessão para um ficheiro próprio da interface Avalonia.
/// </summary>
public static class AvaloniaModuleSession
{
    public static OfficeApplicationRegistry AvailableModules() => new([
        new OfficeModule("writer", "VCC Writer", true),
        new OfficeModule("sheets", "VCC Sheets", true),
        new OfficeModule("slides", "VCC Slides", true),
        new OfficeModule("data", "VCC Data", true)
    ]);

    /// <summary>
    /// A sessão Avalonia tem precedência. Se ainda não existir, importa a
    /// sessão WPF em modo leitura e nunca reescreve o ficheiro de origem.
    /// </summary>
    public static OfficeSession Load(
        OfficeSessionStore avalonia,
        OfficeSessionStore legacy,
        OfficeApplicationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(avalonia);
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(registry);
        var source = File.Exists(avalonia.FilePath) ||
                     File.Exists(avalonia.FilePath + ".bak") ? avalonia : legacy;
        return source.Load(registry);
    }
}
