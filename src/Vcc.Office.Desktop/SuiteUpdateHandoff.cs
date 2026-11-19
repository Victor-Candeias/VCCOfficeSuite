using System.Diagnostics;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Runs only on explicit user request. The helper is an isolated shadow copy
/// of the current published application, never a binary loaded from the new ZIP.
/// </summary>
internal static class SuiteUpdateHandoff
{
    public static string PrepareWorker(PreparedSuiteUpdate plan)
    {
        var installed = Path.GetFullPath(plan.InstallDirectory);
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "VCCOfficeUpdaterWorkers"));
        if (root.StartsWith(installed.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("O auxiliar não pode residir dentro da instalação.");
        Directory.CreateDirectory(root);
        var worker = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(worker);
        try
        {
            foreach (var file in Directory.EnumerateFiles(installed, "*", SearchOption.AllDirectories))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("O auxiliar não copia links simbólicos.");
                var relative = Path.GetRelativePath(installed, file);
                var destination = Path.Combine(worker, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: false);
            }
            if (!File.Exists(Path.Combine(worker, plan.ExpectedExecutable)))
                throw new InvalidDataException("A cópia auxiliar não contém o executável.");
            return worker;
        }
        catch
        {
            Directory.Delete(worker, true);
            throw;
        }
    }

    public static void LaunchWorker(string worker, string planPath, string executableName)
    {
        var binary = Path.Combine(worker, executableName);
        if (!File.Exists(binary)) throw new FileNotFoundException("Auxiliar de atualização em falta.", binary);
        var isDll = binary.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo(isDll ? "dotnet" : binary)
        {
            UseShellExecute = false,
            WorkingDirectory = worker,
            CreateNoWindow = true
        };
        if (isDll) start.ArgumentList.Add(binary);
        start.ArgumentList.Add("--vcc-apply-offline-update");
        start.ArgumentList.Add(planPath);
        if (Process.Start(start) is null)
            throw new InvalidOperationException("Não foi possível iniciar o processo de atualização.");
    }

    public static int RunWorker(string planPath)
    {
        try
        {
            var plan = SuiteUpdateInstaller.ReadPlanAsync(planPath).GetAwaiter().GetResult();
            // Refuse executing in the directory that is being replaced.
            if (string.Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(plan.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return 2;
            var result = SuiteUpdateInstaller.ApplyAfterExit(plan);
            var status = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VCCOffice", "updates", "last-install-result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(status)!);
            File.WriteAllText(status, result.Message);
            if (!result.Succeeded) return 3;
            // Relaunch only after a successful executable readiness probe.
            var binary = Path.Combine(plan.InstallDirectory, plan.ExpectedExecutable);
            var isDll = binary.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var start = new ProcessStartInfo(isDll ? "dotnet" : binary)
            {
                UseShellExecute = false,
                WorkingDirectory = plan.InstallDirectory
            };
            if (isDll) start.ArgumentList.Add(binary);
            Process.Start(start);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or InvalidDataException or System.Text.Json.JsonException or
            System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("Falha no atualizador: " + ex.Message);
            return 1;
        }
    }

    public static bool HealthCheck()
    {
        var binary = OperatingSystem.IsWindows() ? "VCC Office Suite.exe" : "VCC Office Suite.dll";
        return File.Exists(Path.Combine(AppContext.BaseDirectory, binary)) &&
            typeof(MainWindow).Assembly.GetName().Name == "VCC Office Suite" &&
            typeof(Vcc.Office.PluginHost.OfficePluginHost).Assembly.GetName().Name is not null &&
            typeof(CoreDatabase).Assembly.GetName().Name is not null;
    }
}
