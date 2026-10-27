---
maestru: "0.4"
type: work-spec
id: vcc-0011-spec
title: "Plano de implementação de acessibilidade"
template: implementation-plan-v1
work-item: vcc-office/VCC-0011
owner: developer
created: 2026-10-27
---

# Plano de implementação de acessibilidade

## Overview

Persistir as preferências de alto contraste e movimento reduzido no Core Shell e expor um diálogo acessível por teclado, com nomes/descrições de automação nos controlos principais.

## Implementation

1. Adicionar operação validada de atualização de acessibilidade no `CoreShellService`.
2. Criar diálogo WPF com `CheckBox` nomeados, foco por teclado e mensagens de erro acionáveis.
3. Aplicar uma paleta de alto contraste e melhorar a árvore de automação dos controlos dinâmicos.
4. Cobrir persistência, recuperação e rejeição de estado inválido nos testes existentes.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `src/Vcc.Office.CoreShell/CoreShellService.cs` | Modify | Persistência validada das preferências. |
| `src/Vcc.Office.Shell/MainWindow.xaml` | Modify | Acessibilidade visual e UI Automation. |
| `src/Vcc.Office.Shell/MainWindow.xaml.cs` | Modify | Diálogo e aplicação das preferências. |
| `tests/Vcc.Office.CoreShell.Tests/Program.cs` | Modify | Testes de configuração e persistência. |
