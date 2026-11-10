using System.Globalization;
using System.IO;
using System.Windows.Media;

namespace Vcc.Office.Shell;

public sealed record HomeApplication(string Id, string Name, string Symbol, string Color, Geometry Icon);
public sealed record HomeCreation(string Title, HomeApplication Application);

public sealed class HomeScreenModel
{
    private const string Document = "M5,2 L14,2 L20,8 L20,22 L5,22 Z M14,2 L14,8 L20,8 M8,12 L16,12 M8,16 L16,16";
    private const string Calendar = "M3,5 L21,5 L21,21 L3,21 Z M3,10 L21,10 M7,2 L7,7 M17,2 L17,7 M7,14 L10,14 M14,14 L17,14 M7,18 L10,18";
    private const string Nodes = "M12,4 L4,17 L20,17 Z M12,1 A3,3 0 1 0 12,7 A3,3 0 1 0 12,1 M4,14 A3,3 0 1 0 4,20 A3,3 0 1 0 4,14 M20,14 A3,3 0 1 0 20,20 A3,3 0 1 0 20,14";
    private const string Bars = "M4,20 L4,12 L8,12 L8,20 Z M10,20 L10,5 L14,5 L14,20 Z M16,20 L16,9 L20,9 L20,20 Z";
    private const string List = "M8,5 L21,5 M8,12 L21,12 M8,19 L21,19 M3,4 L4,4 L4,6 L3,6 Z M3,11 L4,11 L4,13 L3,13 Z M3,18 L4,18 L4,20 L3,20 Z";
    private static readonly IReadOnlyList<HomeApplication> Applications =
    [
        App("writer", "Writer", "#0869E8", "W"),
        App("sheets", "Sheets", "#00A465", "S"),
        App("slides", "Slides", "#F28706", "P"),
        App("mail", "Mail", "#128CEB", icon: "M2,5 L22,5 L22,20 L2,20 Z M2,5 L12,13 L22,5 M2,20 L8,13 M22,20 L16,13"),
        App("calendar", "Calendar", "#804BE4", icon: Calendar),
        App("notes", "Notes", "#E3AC00", icon: "M4,3 L20,3 L20,21 L4,21 Z M8,8 L16,8 M8,12 L16,12 M8,16 L13,16"),
        App("pdf", "PDF", "#E31D39", icon: "M8,3 C16,2 7,21 3,19 C0,16 23,11 21,16 C18,22 6,4 8,3 Z"),
        App("draw", "Draw", "#068DEE", icon: Nodes),
        App("projects", "Projects", "#096ACC", icon: "M4,5 L4,20 M2,9 L20,9 M7,4 L15,4 L15,7 L7,7 Z M10,12 L21,12 L21,15 L10,15 Z M7,17 L16,17 L16,20 L7,20 Z"),
        App("publisher", "Publisher", "#DC42A1", icon: "M3,3 L21,3 L21,21 L3,21 Z M3,8 L21,8 M10,8 L10,21 M13,12 L18,12 M13,16 L18,16 M6,12 L7,12 M6,16 L7,16"),
        App("planner", "Planner", "#7455DB", icon: "M3,7 L9,13 L19,3 M4,17 L20,17 M13,9 L21,9 M4,21 L17,21"),
        App("images", "Images", "#0896C4", icon: "M3,3 L21,3 L21,21 L3,21 Z M3,19 L10,11 L15,16 L18,13 L21,17 M17,6 A2,2 0 1 0 17,10 A2,2 0 1 0 17,6"),
        App("analytics", "Analytics", "#6B3BE5", icon: Bars),
        App("forms", "Forms", "#A35FE8", icon: List),
        App("contacts", "Contacts", "#126FDB", icon: "M12,3 A4,4 0 1 0 12,11 A4,4 0 1 0 12,3 M4,21 L4,18 C4,11 20,11 20,18 L20,21 Z"),
        App("data", "Data", "#9346DE", icon: "M3,6 C3,1 21,1 21,6 C21,11 3,11 3,6 Z M3,6 L3,18 C3,23 21,23 21,18 L21,6 M3,12 C3,17 21,17 21,12"),
        App("mindmap", "MindMap", "#0064DE", icon: "M12,8 L12,2 M8,12 L2,12 M16,12 L22,12 M12,16 L12,22 M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M10,1 L14,1 L14,4 L10,4 Z M1,10 L4,10 L4,14 L1,14 Z M20,10 L23,10 L23,14 L20,14 Z M10,20 L14,20 L14,23 L10,23 Z"),
        App("whiteboard", "Whiteboard", "#00AEB4", icon: "M3,18 L4,12 L16,2 L22,8 L10,20 L3,21 Z M13,5 L19,11 M4,13 L10,19 M14,21 L22,21"),
        App("scan", "Scan", "#168AA8", icon: "M6,10 L6,2 L18,2 L18,10 M3,10 L21,10 L22,20 L2,20 Z M6,16 L18,16 M8,5 L16,5"),
        App("convert", "Convert", "#E07724", icon: "M3,7 L21,7 M16,2 L21,7 L16,12 M21,17 L3,17 M8,12 L3,17 L8,22"),
        App("backup", "Backup", "#307D9B", icon: "M5,8 C0,13 3,21 10,21 C17,22 23,17 21,11 C20,5 13,2 8,5 M3,2 L3,9 L10,9 M12,9 L12,14 L16,16"),
        App("automate", "Automate", "#E0A218", icon: "M13,1 L4,14 L11,14 L10,23 L21,9 L14,9 Z"),
        App("templates", "Templates", "#5D7FE4", icon: "M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z"),
        App("math", "Math", "#945CD2", "∑"),
        App("archive", "Archive", "#71829D", icon: "M2,3 L22,3 L22,8 L2,8 Z M4,8 L20,8 L20,22 L4,22 Z M9,12 L15,12"),
        App("time", "Time", "#0B9996", icon: "M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M12,6 L12,12 L17,15")
    ];

    public IReadOnlyList<HomeApplication> MainApplications { get; } = Applications.Take(15).ToArray();
    public IReadOnlyList<HomeApplication> MoreApplications { get; } = Applications.Skip(15).ToArray();
    public IReadOnlyList<HomeCreation> CreationActions { get; } =
    [
        Create("Documento em branco", "writer"), Create("Folha de cálculo", "sheets"),
        Create("Apresentação", "slides"), Create("Base de dados", "data"),
        Create("PDF", "pdf"), Create("Desenho / Diagrama", "draw"),
        Create("Projeto (Gantt)", "projects"), Create("Publicação", "publisher"),
        Create("Tarefas (Kanban)", "planner"), Create("Formulário", "forms"),
        Create("Mapa mental", "mindmap"), Create("Quadro branco", "whiteboard")
    ];

    public double StoragePercent { get; }
    public string StorageDescription { get; } = "Armazenamento indisponível";
    public string StoragePercentage { get; } = "—";

    public HomeScreenModel()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))!);
            if (!drive.IsReady || drive.TotalSize <= 0) return;
            var used = drive.TotalSize - drive.TotalFreeSpace;
            StoragePercent = Math.Clamp(100.0 * used / drive.TotalSize, 0, 100);
            var culture = CultureInfo.GetCultureInfo("pt-PT");
            StorageDescription = $"{(used / 1073741824.0).ToString("N1", culture)} GB de {(drive.TotalSize / 1073741824.0).ToString("N0", culture)} GB";
            StoragePercentage = $"{StoragePercent.ToString("N0", culture)}% utilizado";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The home screen remains usable when storage information is unavailable.
        }
    }

    private static HomeApplication App(string id, string name, string color, string symbol = "", string icon = "")
    {
        var geometry = string.IsNullOrEmpty(icon) ? Geometry.Empty : Geometry.Parse(icon);
        geometry.Freeze();
        return new(id, "VCC " + name, symbol, color, geometry);
    }

    private static HomeCreation Create(string title, string id) => new(title, Applications.Single(app => app.Id == id));
}
