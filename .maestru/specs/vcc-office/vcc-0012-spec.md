---
maestru: "0.4"
type: work-spec
id: vcc-0012-spec
title: "Plano de implementação de navegação"
template: implementation-plan-v1
work-item: vcc-office/VCC-0012
owner: developer
created: 2026-10-27
---

# Plano de implementação de navegação

## Overview

Disponibilizar uma configuração de navegação previsível no Core Shell, permitindo escolher o foco inicial e se a navegação por Tab percorre os controlos em ciclo.

## Implementation

1. Introduzir contrato e validação de `NavigationSettings`.
2. Persistir a configuração no estado do Shell através de uma operação validada.
3. Expor diálogo acessível por teclado e aplicar o modo de navegação na janela.
4. Cobrir configuração, persistência e rejeição de valores inválidos.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `src/Vcc.Office.CoreShell/NavigationPolicy.cs` | Add | Contrato e validação das opções de navegação. |
| `src/Vcc.Office.CoreShell/ShellModels.cs` | Modify | Persistência das opções no estado. |
| `src/Vcc.Office.CoreShell/CoreShellService.cs` | Modify | Atualização transacional da configuração. |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Acesso ao diálogo e nomes de automação. |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Aplicação do foco e do ciclo de Tab. |
| `tests/Vcc.Office.CoreShell.Tests/Program.cs` | Modify | Testes de navegação. |
