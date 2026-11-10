---
maestru: "0.4"
type: work-spec
id: vcc-0811-main-menu-spec
title: "VCC-0811 — Ecrã inicial e menu principal"
template: implementation-plan-v1
work-item: vcc-office/VCC-0811
owner: developer
created: 2026-11-10
---

# VCC-0811 — Ecrã inicial e menu principal

## Overview

Construir o ecrã inicial WPF da issue #3 segundo `Documents/MenuPrincipal.jpg`, com todas as opções disponíveis visualmente. O pedido atual adia as operações de negócio: os atalhos dos módulos não criam nem abrem documentos, e os recentes apresentam um estado vazio real em vez dos dados exemplificativos da imagem.

## Implementation

### Phase 1: Ecrã inicial

#### Step 1.1: Compor a interface e o catálogo

| Action | File | Details |
|--------|------|---------|
| Create | `src/Vcc.Office.Shell/HomeScreenModel.cs` | Catálogo das 26 aplicações, 12 cartões e leitura do armazenamento local sem dados de demonstração. |
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml` | Cabeçalho, navegação lateral, banner vetorial, cartões, recentes e painéis laterais. |
| Modify | `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Estado de janela, Ctrl+K, expansão de Mais aplicações e adaptação da grelha à largura. |
| Create | `src/Vcc.Office.Shell/WorkspaceWindow.xaml` | Preservar o anterior espaço de trabalho para a integração futura dos módulos. |
| Create | `src/Vcc.Office.Shell/WorkspaceWindow.xaml.cs` | Preservar os handlers e fluxos já desenvolvidos, sem os ligar aos novos atalhos nesta fase. |

- [x] Todos os elementos da referência e as 26 aplicações estão representados.
- [x] Atalhos de negócio não executam operações; controlos de janela e interação visual implementados.

### Phase 2: Validação e entrega

#### Step 2.1: Verificar layout e regressões

| Action | File | Details |
|--------|------|---------|
| Run | `VCC.Office.sln` | Compilar Debug/Release e executar os testes existentes. |
| Run | `artifacts/` | Renderizar o WPF em tamanhos desktop e compacto, inspecionar capturas e verificar overflow e binding errors. |
| Modify | `README.md` | Documentar o ecrã inicial e o âmbito visual desta fase. |
| Run | `.maestru/` | Atualizar evidência e executar `maestru check`. |

- [x] Capturas inspecionadas em 1600×940, 1280×800 e 1024×768; menu adicional com 11 aplicações também renderizado.
- [x] Builds Debug/Release e 43 testes por configuração aprovados; sem erros de bindings WPF. Publicação Release Windows x64 self-contained aprovada.
- [x] `git diff --check` e `maestru check` aprovados.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `src/Vcc.Office.Shell/HomeScreenModel.cs` | Create | Dados da interface e catálogo completo. |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Novo ecrã inicial. |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Comportamento visual e estado da janela. |
| `src/Vcc.Office.Shell/WorkspaceWindow.xaml` | Create | Preservar a interface anterior. |
| `src/Vcc.Office.Shell/WorkspaceWindow.xaml.cs` | Create | Preservar a lógica anterior. |
| `README.md` | Modify | Documentar a entrega. |
| `.maestru/tracks/vcc-office/vcc-0811.md` | Create | Rastrear a issue #3. |
| `.maestru/specs/vcc-office/vcc-0811-main-menu-spec.md` | Create | Plano e validação. |
| `.maestru/tracks/vcc-office.md` | Modify | Resumo do work item e ligação à spec. |
