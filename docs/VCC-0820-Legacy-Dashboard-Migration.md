# VCC-0820 — Compatibilidade de preferências de dashboard WPF

## Diferença identificada
A versão WPF grava `PreferredDashboardId`, `LastDashboardId`, `StartupMode` e `Theme` em
`%LOCALAPPDATA%/VCC Office/dashboards.sqlite`, tabela `DashboardSettings`,
chave `UserId = Environment.UserName`.

A versão Avalonia usava apenas o último dashboard e o tema na chave
`avalonia.launcher.session.v1` da base `CoreDatabase`. Isto perderia a regra
«Abrir sempre o preferido» para utilizadores que atualizassem da versão WPF.

## Tratamento nesta fase
1. Adiciona `PreferredDashboardId` e `StartupMode` ao estado serializado Avalonia,
   preservando retrocompatibilidade do JSON existente.
2. Se **não houver preferências Avalonia**, importa as do WPF em modo SQLite
   `ReadOnly`, sem escrever nem apagar a origem.
3. Se houver preferências Avalonia, estas prevalecem para respeitar escolhas mais recentes.
4. A interface de definições Avalonia passa a permitir escolher o dashboard preferido
   e a regra de arranque (último ou preferido).
5. Ao arrancar, a aplicação usa `DashboardPolicy.ResolveStartup`, tal como o WPF.
6. Os testes criam um ficheiro SQLite WPF de exemplo, verificam o arranque e confirmam
   que a base legada mantém os valores originais.

## Limites
- Esta importação é realizada apenas quando não existe estado Avalonia.
- Não migra ainda histórico de janelas, preferências de outras ferramentas ou
  configurações avançadas do WPF que estejam noutros locais.
- A remoção de `Vcc.Office.Shell` só será feita depois da validação completa de
  paridade WPF/Avalonia e dos testes de instalação/rollback.

Estado da VCC-0820: em curso.
