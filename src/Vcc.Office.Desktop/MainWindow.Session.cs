using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Recuperação de sessões dos editores Avalonia. O ficheiro WPF legado é
/// importado apenas em leitura, para que seja possível fazer rollback.
/// </summary>
public sealed partial class MainWindow
{
    private OfficeApplicationRegistry SessionRegistry => AvaloniaModuleSession.AvailableModules(_registry.GetAll());
    private readonly OfficeSessionStore _sessionStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VCC Office", "avalonia-application-session.json"));
    private readonly OfficeSessionStore _legacySessionStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VCC Office", "application-session.json"));

    private OfficeSession _moduleSession = OfficeSession.Empty;
    private bool _restoringModules;
    private bool _closingSuite;

    private void ConfigureModuleSession()
    {
        // Prevent the automatic Closed events of child windows from erasing
        // the snapshot when the whole suite is being closed.
        Closing += (_, _) =>
        {
            _closingSuite = true;
            try { SaveModuleSession(); }
            catch (IOException) { /* Do not block window shutdown. */ }
            catch (UnauthorizedAccessException) { /* A read-only profile can still close. */ }
        };
    }

    private void RestoreModuleSession()
    {
        try
        {
            _moduleSession = AvaloniaModuleSession.Load(
                _sessionStore, _legacySessionStore, SessionRegistry);
            var toOpen = _moduleSession.OpenModuleIds.ToArray();

            _restoringModules = true;
            try
            {
                foreach (var id in toOpen)
                {
                    var module = _registry.Find(id);
                    if (module is null) continue;
                    OpenModule(module);
                }
            }
            finally
            {
                _restoringModules = false;
            }

            // Save the imported filtered snapshot to Avalonia's own file,
            // without changing the user's WPF session.
            SaveModuleSession();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException)
        {
            ShowNotice("Não foi possível recuperar a sessão", ex.Message);
        }
    }

    private void TrackModuleOpened(string moduleId)
    {
        if (_closingSuite) return;
        try
        {
            _moduleSession = OfficeSessionPolicy.Open(_moduleSession, moduleId, SessionRegistry);
            if (!_restoringModules) SaveModuleSession();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException)
        {
            ShowNotice("Não foi possível guardar a sessão", ex.Message);
        }
    }

    private void TrackModuleClosed(string moduleId)
    {
        if (_closingSuite || _restoringModules) return;
        try
        {
            _moduleSession = OfficeSessionPolicy.Close(_moduleSession, moduleId, SessionRegistry);
            SaveModuleSession();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException)
        {
            ShowNotice("Não foi possível atualizar a sessão", ex.Message);
        }
    }

    private void SaveModuleSession()
    {
        if (_restoringModules) return;
        _sessionStore.Save(_moduleSession, SessionRegistry);
    }
}
