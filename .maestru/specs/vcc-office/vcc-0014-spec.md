---
maestru: "0.4"
type: work-spec
id: vcc-0014-spec
title: "Plano de consulta e navegação de janelas"
template: implementation-plan-v1
work-item: vcc-office/VCC-0014
owner: developer
created: 2026-10-27
---

# Plano de consulta e navegação de janelas

## Overview

Permitir consultar o estado atual da janela e executar ações de navegação previsíveis — maximizar, restaurar e centrar — com normalização segura e persistência offline.

## Implementation

1. Criar ações tipadas e regras de transformação do layout.
2. Validar e persistir as ações através do Core Shell.
3. Expor uma janela de consulta com estado atual e ações acessíveis por teclado.
4. Cobrir layouts válidos, limites e ações de navegação nos testes.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `src/Vcc.Office.CoreShell/WindowNavigationPolicy.cs` | Add | Ações e regras de navegação de janelas. |
| `src/Vcc.Office.CoreShell/CoreShellService.cs` | Modify | Persistência das ações de janela. |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Acesso ao estado e ações da janela. |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Consulta e aplicação do layout. |
| `tests/Vcc.Office.CoreShell.Tests/Program.cs` | Modify | Testes de navegação de janelas. |
