using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.PluginHost;

namespace Vcc.Office.Desktop;

/// <summary>Installed plugin inventory and controlled activation without reinstalling the suite.</summary>
public sealed class PluginManagerWindow : Window
{
    private readonly OfficePluginHost _host;
    private readonly OfficePluginPackageManager _packages;
    private readonly Action<string, bool> _onChanged;
    private readonly Action<string, string, bool>? _onOperation;
    private readonly StackPanel _items = new() { Spacing = 12, Margin = new Thickness(18) };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };

    public PluginManagerWindow(OfficePluginHost host, Action<string, bool> onChanged,
        Action<string, string, bool>? onOperation = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _packages = new OfficePluginPackageManager(_host.RootDirectory);
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _onOperation = onOperation;
        Title = "VCC Office — Gestão de plugins";
        Width = 740; Height = 650; MinWidth = 560; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var header = new StackPanel { Margin = new Thickness(18), Spacing = 7 };
        header.Children.Add(new TextBlock { Text = "Plugins instalados", FontSize = 25, FontWeight = FontWeight.Bold });
        header.Children.Add(new TextBlock { Text = "A desativação impede a abertura e retira a aplicação dos menus. Os assemblies carregados só são libertados quando a suite é reiniciada.", TextWrapping = TextWrapping.Wrap });
        header.Children.Add(new TextBlock { Text = "Instale apenas plugins de origem confiável. Os pacotes ZIP são aplicados no próximo arranque, antes de carregar as DLLs.", TextWrapping = TextWrapping.Wrap });
        var install = new Button { Content = "Instalar / atualizar plugin (.zip)", HorizontalAlignment = HorizontalAlignment.Left };
        install.Click += async (_, _) =>
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                { Title = "Instalar plugin", AllowMultiple = false,
                  FileTypeFilter = [new FilePickerFileType("Pacotes de plugins ZIP") { Patterns = ["*.zip"] }] });
                if (files.Count == 0) return;
                var path = files[0].TryGetLocalPath() ?? throw new InvalidOperationException("Apenas ficheiros locais.");
                var result = _packages.StageInstall(path);
                _message.Text = result.Id + ": " + (result.Action == "install" ? "instalação" : "atualização") + " preparada para o próximo arranque.";
                _onOperation?.Invoke("Instalação/atualização preparada", _message.Text ?? "", true);
                Refresh();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
            {
                _message.Text = "Não foi possível preparar o plugin: " + ex.Message;
                _onOperation?.Invoke("Falha na instalação de plugin", ex.Message, false);
            }
        };
        header.Children.Add(install);
        header.Children.Add(_message);
        DockPanel.SetDock(header, Dock.Top);
        var root = new DockPanel();
        root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = _items });
        Content = root;
        Refresh();
    }

    private void Refresh()
    {
        _items.Children.Clear();
        var entries = _host.GetInventory();
        if (entries.Count == 0)
            _items.Children.Add(new TextBlock { Text = "Nenhum plugin instalado." });
        foreach (var entry in entries)
        {
            var details = new StackPanel { Spacing = 5 };
            details.Children.Add(new TextBlock { Text = entry.Name + " · v" + entry.Version,
                FontWeight = FontWeight.SemiBold, FontSize = 16 });
            details.Children.Add(new TextBlock { Text = "ID: " + entry.Id + " · API v1 (compatível) · " +
                (entry.Enabled ? "Ativo" : "Desativado") });
            details.Children.Add(new TextBlock { Text = "Dependências: " +
                (entry.Dependencies.Count == 0 ? "Nenhuma" : string.Join(", ", entry.Dependencies)),
                TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            var toggle = new Button { Content = entry.Enabled ? "Desativar" : "Ativar", MinWidth = 110,
                HorizontalAlignment = HorizontalAlignment.Right };
            toggle.Click += (_, _) =>
            {
                try
                {
                    _host.SetEnabled(entry.Id, !entry.Enabled);
                    _onChanged(entry.Id, !entry.Enabled);
                    _message.Text = "Estado atualizado: " + entry.Name;
                    Refresh();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _message.Text = "Não foi possível alterar o plugin: " + ex.Message;
                    _onOperation?.Invoke("Falha ao alterar plugin", ex.Message, false);
                }
            };
            details.Children.Add(toggle);
            var remove = new Button { Content = "Remover no próximo arranque", HorizontalAlignment = HorizontalAlignment.Right };
            remove.Click += (_, _) =>
            {
                try
                {
                    _packages.StageRemove(entry.Id);
                    _host.SetEnabled(entry.Id, false);
                    _onChanged(entry.Id, false);
                    _message.Text = entry.Name + ": remoção agendada para o próximo arranque.";
                    _onOperation?.Invoke("Remoção de plugin agendada", _message.Text, true);
                    Refresh();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _message.Text = "Não foi possível remover o plugin: " + ex.Message;
                    _onOperation?.Invoke("Falha ao remover plugin", ex.Message, false);
                }
            };
            details.Children.Add(remove);
            _items.Children.Add(new Border { BorderBrush = Brush.Parse("#8D9DB2"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12), Child = details });
        }
        foreach (var pending in _packages.Pending)
            _items.Children.Add(new TextBlock { Text = "Pendente: " + pending.Id + " · " + pending.Action + " (próximo arranque)",
                TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
        if (_host.Errors.Count > 0)
        {
            _items.Children.Add(new TextBlock { Text = "Problemas de carregamento",
                FontWeight = FontWeight.Bold, FontSize = 16 });
            foreach (var error in _host.Errors)
                _items.Children.Add(new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap,
                    Foreground = Brush.Parse("#B64040") });
        }
    }
}
