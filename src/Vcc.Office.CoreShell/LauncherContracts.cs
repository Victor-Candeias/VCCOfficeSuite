namespace Vcc.Office.CoreShell;

/// <summary>
/// DTO estável usado na fronteira do Launcher. A camada de transporte não
/// precisa conhecer o estado completo do Core Shell.
/// </summary>
public sealed record LauncherItemDto(string Id, string Title, string Description, string Glyph)
{
    public LauncherItem ToModel() => new(Id, Title, Description, Glyph);

    public static LauncherItemDto FromModel(LauncherItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new(item.Id, item.Title, item.Description, item.Glyph);
    }
}

/// <summary>Limites públicos do contrato de módulos do Launcher.</summary>
public static class LauncherContract
{
    public const int MaxIdLength = 64;
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 500;
    public const int MaxGlyphLength = 8;
}
