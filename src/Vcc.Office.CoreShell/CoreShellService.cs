namespace Vcc.Office.CoreShell;

public sealed class CoreShellService
{
    public CoreShellService(ShellStateStore stateStore) => StateStore = stateStore;

    public ShellStateStore StateStore { get; }
    public ShellState State { get; private set; } = ShellState.Default;

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
        => State = await StateStore.LoadAsync(cancellationToken);

    public async Task UpdateAsync(Func<ShellState, ShellState> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var next = update(State);
        var validation = ShellStateValidator.Validate(next);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors));
        await StateStore.SaveAsync(next, cancellationToken);
        State = next;
    }
}
