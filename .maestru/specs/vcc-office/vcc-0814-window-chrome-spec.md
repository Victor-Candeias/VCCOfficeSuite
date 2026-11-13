---
maestru: "0.4"
type: work-spec
id: vcc-0814-window-chrome-spec
title: "VCC-0814 — Janela, monitores e cabeçalho"
template: implementation-plan-v1
work-item: vcc-office/VCC-0814
owner: developer
created: 2026-11-13
---

# VCC-0814 — Janela, monitores e cabeçalho

## Overview

A interface WPF usa WindowStyle=None e WindowChrome CaptionHeight=0; apenas o logótipo tem handler de arrasto. A reposição do layout limita coordenadas ao monitor principal. O seletor e botões do cabeçalho ainda usam estilos padrão inconsistentes.

## Implementation

### Phase 1: Janela e monitores

| Action | File | Details |
|---|---|---|
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml` | CaptionHeight=64, resize border=8 e marcar controlos interativos como hit-test do chrome. |
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Remover arrasto exclusivo do logo, selecionar monitor para repor coordenadas e atualizar glifo maximizar/restaurar. |
| Modify | `src/Vcc.Office.Shell/Vcc.Office.Shell.csproj` | Ativar Windows Forms apenas para Screen.AllScreens. |

- [x] A janela usa hit testing do WindowChrome em toda a legenda livre.
- [x] Restauro verifica monitores secundários.
- [ ] Validar arrasto maximizado/restauro, Snap e resize em Windows.
- [ ] Validar monitores com escalas DPI diferentes.

### Phase 2: Cabeçalho

| Action | File | Details |
|---|---|---|
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml` | Criar template do seletor pill e estilos dos controlos superiores e da legenda. |
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Atualizar tooltip e iconografia de maximização; resposta de notificações sem dados fictícios. |

- [x] Design consistente com a pesquisa.
- [x] Hover diferenciado do botão fechar e glifos de legenda reconhecíveis.
- [ ] Inspeção em temas claro/escuro e navegação por teclado.

### Phase 3: Validação

| Action | File | Details |
|---|---|---|
| Run | `VCC.Office.sln` | Build Debug e Release. |
| Run | `tests/Vcc.Office.CoreShell.Tests` | Smoke tests. |
| Run | `.maestru` | maestru check, caso CLI disponível. |

- [ ] GitHub Actions aprovado.
- [ ] Validação manual Windows em duas configurações de monitores.
- [ ] maestru check aprovado.

## Impacted Files

| File | Action | Purpose |
|---|---|---|
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Janela e controlos |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Comportamento e monitores |
| `src/Vcc.Office.Shell/Vcc.Office.Shell.csproj` | Modify | Enumeração de monitores |
| `.maestru/tracks/vcc-office/vcc-0814.md` | Create | Work item |
| `.maestru/specs/vcc-office/vcc-0814-window-chrome-spec.md` | Create | Work Spec |
