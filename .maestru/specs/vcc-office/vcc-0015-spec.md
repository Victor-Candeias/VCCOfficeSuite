---
maestru: "0.4"
type: work-spec
id: vcc-0015-spec
title: "Plano de consulta e navegação do Ribbon"
template: implementation-plan-v1
work-item: vcc-office/VCC-0015
owner: developer
created: 2026-10-27
---

# Plano de consulta e navegação do Ribbon

## Overview

Permitir consultar separadores e comandos do Ribbon por pesquisa, navegar entre resultados com teclado e registar o separador/comando selecionado no estado offline.

## Implementation

1. Adicionar consulta tipada de separadores e comandos no `RibbonPolicy`.
2. Persistir a seleção navegada através do Core Shell.
3. Expor pesquisa, contagem e estados acessíveis na área do Ribbon.
4. Cobrir correspondências, resultados vazios, seleção e valores inválidos.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `src/Vcc.Office.CoreShell/RibbonPolicy.cs` | Modify | Consulta filtrada de separadores e comandos. |
| `src/Vcc.Office.CoreShell/CoreShellService.cs` | Modify | Persistência da navegação no Ribbon. |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Pesquisa e feedback acessíveis. |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Filtragem e seleção de resultados. |
| `tests/Vcc.Office.CoreShell.Tests/Program.cs` | Modify | Testes de consulta e navegação. |
