---
maestru: "0.4"
type: work-spec
id: vcc-0013-spec
title: "Plano de implementação de consulta e navegação do Launcher"
template: implementation-plan-v1
work-item: vcc-office/VCC-0013
owner: developer
created: 2026-10-27
---

# Plano de implementação de consulta e navegação do Launcher

## Overview

Permitir consultar o Launcher com informação clara, seleção por teclado e feedback persistente da aplicação escolhida, mantendo validação e funcionamento offline.

## Implementation

1. Adicionar um índice de seleção e estado de consulta do Launcher no Shell.
2. Expor filtro de pesquisa, contagem de resultados e limpeza rápida na interface.
3. Garantir que os módulos filtrados continuam acessíveis por Tab e leitores de ecrã.
4. Cobrir pesquisa, seleção, resultados vazios e persistência do último módulo consultado.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `src/Vcc.Office.CoreShell/LauncherPolicy.cs` | Modify | Validar consulta e seleção de módulos. |
| `src/Vcc.Office.CoreShell/CoreShellService.cs` | Modify | Persistir o módulo consultado. |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Campo de pesquisa e estado acessível do Launcher. |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Filtragem, seleção e feedback da consulta. |
| `tests/Vcc.Office.CoreShell.Tests/Program.cs` | Modify | Testes de consulta e seleção. |
