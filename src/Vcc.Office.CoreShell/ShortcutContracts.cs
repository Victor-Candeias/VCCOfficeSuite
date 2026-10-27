namespace Vcc.Office.CoreShell;

/// <summary>DTO estável para uma associação entre comando e atalho.</summary>
public sealed record ShortcutBindingDto(string CommandId, string Gesture)
{
    public static ShortcutBindingDto FromPair(KeyValuePair<string, string> pair)
        => new(pair.Key, pair.Value);

    public KeyValuePair<string, string> ToPair() => new(CommandId, Gesture);
}

/// <summary>Limites públicos do contrato de atalhos.</summary>
public static class ShortcutContract
{
    public const int MaxCommandIdLength = 100;
    public const int MaxGestureLength = 64;
}
