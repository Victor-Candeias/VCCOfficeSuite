namespace Vcc.Office.CoreShell;

/// <summary>
/// 
/// </summary>
public sealed class CoreShellService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="stateStore"></param>
    public CoreShellService(ShellStateStore stateStore) => StateStore = stateStore;

    /// <summary>
    /// 
    /// </summary>
    public ShellStateStore StateStore { get; }

    /// <summary>
    /// 
    /// </summary>
    public ShellState State { get; private set; } = ShellState.Default;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
        => State = await StateStore.LoadAsync(cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="update"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
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
