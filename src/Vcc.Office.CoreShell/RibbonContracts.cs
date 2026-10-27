namespace Vcc.Office.CoreShell;

/// <summary>DTO de comando usado na fronteira pública do Ribbon.</summary>
public sealed record RibbonCommandDto(string Id, string Title, string Description, string Glyph)
{
    public RibbonCommand ToModel() => new(Id, Title, Description, Glyph);

    public static RibbonCommandDto FromModel(RibbonCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new(command.Id, command.Title, command.Description, command.Glyph);
    }
}

/// <summary>DTO de separador e respetivos comandos do Ribbon.</summary>
public sealed record RibbonTabDto(string Id, string Title, IReadOnlyList<RibbonCommandDto> Commands)
{
    public RibbonTab ToModel() => new(Id, Title, Commands.Select(command => command.ToModel()).ToArray());

    public static RibbonTabDto FromModel(RibbonTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        return new(tab.Id, tab.Title, tab.Commands.Select(RibbonCommandDto.FromModel).ToArray());
    }
}

/// <summary>Limites públicos do contrato de dados do Ribbon.</summary>
public static class RibbonContract
{
    public const int MaxIdLength = 64;
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 500;
    public const int MaxGlyphLength = 8;
}
