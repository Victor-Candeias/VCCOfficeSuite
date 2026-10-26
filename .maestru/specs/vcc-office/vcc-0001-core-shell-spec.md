---
maestru: "0.4"
type: work-spec
id: vcc-0001-core-shell-spec
title: "VCC-0001 Core Shell — implementação inicial"
template: implementation-plan-v1
work-item: vcc-office/VCC-0001
owner: developer
created: 2026-10-26
---

# VCC-0001 Core Shell — implementação inicial

## Overview

Entregar o primeiro shell executável offline da suite em WPF/.NET 10. A fundação cobre o Launcher visual, contratos tipados para configuração do shell, estado de janela, tema, atalhos e acessibilidade, além de persistência JSON local com escrita temporária, backup e recuperação de estado corrompido. A integração WebView2 e os editores Writer/Sheets/Slides permanecem nos work-items próprios.

## Implementation

1. Criar a solução `VCC.Office.sln` com biblioteca de domínio, aplicação WPF e smoke tests sem dependências externas.
2. Definir `ShellState`, `LauncherItem`, `WindowLayout`, `ThemeMode` e validação de limites para rejeitar configurações inválidas.
3. Implementar `ShellStateStore` com JSON versionado, persistência atómica (`.tmp` + replace) e fallback para `.bak`.
4. Implementar `CoreShellService` para inicialização, atualização validada e persistida do estado.
5. Entregar Launcher WPF em PT-PT com cartões de módulos, tema claro/escuro, estado offline e persistência do layout.
6. Validar estado predefinido, rejeição de entrada inválida, round-trip de persistência e recuperação após corrupção.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `VCC.Office.sln` | Added | Solução da entrega inicial. |
| `src/Vcc.Office.CoreShell` | Added | Contratos, validação, persistência e serviço do Core Shell. |
| `src/Vcc.Office.Shell` | Added | Launcher WPF executável offline. |
| `tests/Vcc.Office.CoreShell.Tests` | Added | Smoke tests automatizados sem pacote de teste externo. |
