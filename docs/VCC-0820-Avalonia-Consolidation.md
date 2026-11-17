# VCC-0820 — Consolidação Avalonia e desativação futura do WPF

## Estado: fase 1 (preparação, sem remoção de funcionalidades)

A eliminação definitiva do WPF **depende da paridade funcional aprovada**.
Este trabalho permite avançar em segurança, sem eliminar antecipadamente a versão Windows funcional.

### Alterações desta fase

- `VCC.Office.Avalonia.sln`: solução multiplataforma que contém apenas o Desktop Avalonia, o CoreShell e os dois projetos de testes.
- Cópia do ícone para `src/Vcc.Office.Desktop/Assets/VccOfficeSuite.ico` e atualização das referências no projeto Avalonia; o projeto desktop deixa de usar recursos na pasta WPF.
- `.github/workflows/avalonia.yml`: restauração e compilação da solução Avalonia completa nos runners Windows, Ubuntu e macOS, em Debug e Release.
- Mantidos `VCC.Office.sln`, `src/Vcc.Office.Shell/` e `.github/workflows/build.yml` para evitar regressões da versão legada.

### Bloqueios da eliminação definitiva

| Módulo ou capacidade | Estado a confirmar |
| --- | --- |
| Writer | PR #522 em evolução: editor básico, sem paridade integral de formatação/tabelas/imagens |
| Sheets | Falta interface Avalonia completa e validação de funções do editor |
| Slides | Falta interface Avalonia completa e validação de funções do editor |
| Data | Há interface Avalonia parcial; é necessária validação de paridade |
| Restantes módulos | Inventariar funções WPF realmente utilizadas e migrá-las |
| Dados existentes | Testar migração não destrutiva de SQLite, sessões, preferências, recentes e favoritos |
| Distribuição | Validar instalação e arranque real em Windows, Linux e macOS |
| UX | Validar visualmente três dashboards, temas, ícones, diálogos e navegação |

### Critérios para a fase final

1. Inventário de funcionalidades da versão WPF aprovado.
2. Todas as capacidades necessárias validadas em Avalonia; testes automáticos de regressão para os fluxos críticos.
3. Publicação multiplataforma pronta e testada com artefactos de Release.
4. Plano de atualização/rollback da versão instalada e preservação de dados aprovado.
5. Apenas então: retirar `Vcc.Office.Shell` e o workflow WPF, promover `VCC.Office.Avalonia.sln` a solução principal e eliminar referências residuais.
6. Merge com CI verde Windows/Linux/macOS Debug e Release, e teste real de arranque.

### Comandos de validação local

```bash
dotnet restore VCC.Office.Avalonia.sln
dotnet build VCC.Office.Avalonia.sln --configuration Debug --no-restore
dotnet build VCC.Office.Avalonia.sln --configuration Release --no-restore
dotnet run --project tests/Vcc.Office.CoreShell.Tests/Vcc.Office.CoreShell.Tests.csproj --configuration Release
dotnet run --project tests/Vcc.Office.Desktop.SmokeTests/Vcc.Office.Desktop.SmokeTests.csproj --configuration Release
```

**Nota:** a fase 1 não deve ser interpretada como remoção do WPF ou conclusão da migração.
