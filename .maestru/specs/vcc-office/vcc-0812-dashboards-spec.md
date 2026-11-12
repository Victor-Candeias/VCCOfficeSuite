---
maestru: "0.4"
type: work-spec
id: vcc-0812-dashboards-spec
title: "VCC-0812 — Dashboards múltiplos"
template: implementation-plan-v1
work-item: vcc-office/VCC-0812
owner: developer
created: 2026-11-12
---

# VCC-0812 — Dashboards múltiplos

## Overview

Introduzir três dashboards selecionáveis no VCC Office sem substituir o ecrã principal já implementado. A aplicação existente é WPF .NET 10, pelo que esta implementação reutiliza o shell nativo, sem instalar React/WebView2 apenas para dashboards. Preferências locais em SQLite e identificação estável de vista.

## Implementation

### Phase 1: Contratos e persistência

| Action | File | Details |
|---|---|---|
| Create | `src/Vcc.Office.CoreShell/DashboardDefinitions.cs` | Registry, IDs, política de arranque e validação. |
| Create | `src/Vcc.Office.Shell/DashboardSqliteRepository.cs` | Guardar por utilizador último dashboard, preferido, startup e tema. |
| Modify | `tests/Vcc.Office.CoreShell.Tests/Program.cs` | Testar arranque, fallback e IDs inválidos. |

- [x] Definições por omissão: modern/last/system.
- [x] SQLite separado do shell-state.json para preservar configurações anteriores.
- [ ] Testes em CI aprovados.

### Phase 2: Interface e integração

| Action | File | Details |
|---|---|---|
| Create | `src/Vcc.Office.Shell/DashboardView.cs` | Moderno com estados vazios reais; grelha e pesquisa das 26 aplicações. |
| Create | `src/Vcc.Office.Shell/DashboardSettingsWindow.cs` | Diálogo para dashboard preferido, modo de arranque e tema. |
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml` | Seletor no cabeçalho e host para vistas alternativas. |
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Arranque, alternância sem reinício e mudança do tema do SO. |

- [x] Dashboard original clássico preservado.
- [x] Novos dashboards com navegação de seleção.
- [x] Módulos ainda não disponíveis não executam ações fictícias.
- [ ] Inspeção visual e teste de teclado em Windows.

### Phase 3: Validação

| Action | File | Details |
|---|---|---|
| Run | `VCC.Office.sln` | dotnet build Debug e Release, warnings as errors. |
| Run | `tests/Vcc.Office.CoreShell.Tests` | Executar smoke tests. |
| Run | `.maestru/` | maestru check para validar Work Item e Work Spec. |

- [ ] Builds e testes executados.
- [ ] Maestru check executado.
- [ ] Testes de alternância, restart e tema em Windows concluídos.

## Critérios de aceitação

1. O utilizador alterna entre `modern`, `classic` e `grid` sem encerrar a aplicação.
2. Primeiro arranque usa modern. `last` recorda o último dashboard válido.
3. O selector e as definições guardam as escolhas por utilizador em SQLite.
4. O tema system aplica o modo Windows aos dashboards novos.
5. O dashboard clássico e WorkspaceWindow são preservados.

## Impacted Files

| File | Action | Purpose |
|---|---|---|
| `src/Vcc.Office.CoreShell/DashboardDefinitions.cs` | Create | Contratos e políticas |
| `src/Vcc.Office.Shell/DashboardSqliteRepository.cs` | Create | SQLite |
| `src/Vcc.Office.Shell/DashboardView.cs` | Create | Moderno e grelha |
| `src/Vcc.Office.Shell/DashboardSettingsWindow.cs` | Create | Preferências |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Layout e seletor |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Navegação |
| `tests/Vcc.Office.CoreShell.Tests/Program.cs` | Modify | Testes |
