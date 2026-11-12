---
maestru: "0.4"
type: work-spec
id: vcc-0813-dashboard-polish-spec
title: "VCC-0813 — Aperfeiçoamento visual dos dashboards"
template: implementation-plan-v1
work-item: vcc-office/VCC-0813
owner: developer
created: 2026-11-12
---

# VCC-0813 — Aperfeiçoamento visual dos dashboards

## Overview

Refinar o dashboard moderno no projeto WPF existente, tendo por referência o menu escuro da imagem fornecida. Manter o dashboard clássico e o Launcher em grelha. Evitar dependências online ou falsas funcionalidades de negócio.

## Implementation

### Phase 1: Reestruturação do layout

| Action | File | Details |
|---|---|---|
| Modify | `src/Vcc.Office.Shell/DashboardView.cs` | Layout com sidebar rolável, banner com arte vetorial, cartões de ações, três painéis de ficheiros e quatro atalhos. |
| Modify | `src/Vcc.Office.Shell/DashboardView.cs` | Grelha com catálogo das 26 apps, filtro de pesquisa e adaptação de colunas. |

- [x] Estrutura semelhante à referência.
- [x] Não apresentar ficheiros ou partilhas fictícias.

### Phase 2: Navegação e tema

| Action | File | Details |
|---|---|---|
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml` | Nomear superfícies de cabeçalho para adaptação visual. |
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Aplicar cores do header em modern/grid, manter classic e conectar navegação. |

- [x] Navegação entre dashboards sem reinício.
- [x] Respeitar configuração system/dark/light no novo layout.

### Phase 3: Revisão

| Action | File | Details |
|---|---|---|
| Run | `VCC.Office.sln` | Build Debug e Release. |
| Run | `tests/Vcc.Office.CoreShell.Tests` | Testes e publicação Windows x64. |
| Run | `.maestru` | Executar maestru check na instalação de desenvolvimento. |

- [ ] Build e testes aprovados em CI.
- [ ] Verificação visual em Windows 1600x940, 1280x800, 1024x768.
- [ ] Maestru check aprovado.

## Riscos e limitações

- Os editores das 26 apps ainda não estão conectados ao Launcher: os cartões informam, mas não executam operações fictícias.
- A aproximação à referência não inclui ficheiros recentes simulados nem serviços de colaboração inexistentes.
- A validação automatizada de compilação não substitui a inspeção visual manual em Windows.

## Impacted Files

| File | Action | Purpose |
|---|---|---|
| `src/Vcc.Office.Shell/DashboardView.cs` | Modify | Dashboard moderno e grelha |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Cabeçalho adaptável |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Alternância e tema |
| `.maestru/tracks/vcc-office/vcc-0813.md` | Create | Work Item |
| `.maestru/specs/vcc-office/vcc-0813-dashboard-polish-spec.md` | Create | Work Spec |
