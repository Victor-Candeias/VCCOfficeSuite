# VCC-0820 — Conclusão funcional WPF → Avalonia (diferenças concretas)

## Alterações deste PR

1. **Geometria WPF**: lê `ShellState.Window` após recuperar preferências, adapta a área visível do monitor, dimensão, posição e maximização da janela Avalonia. Ao fechar, grava o estado no `shell-state.json` partilhado, preservando a dimensão anterior quando maximizada. A conversão usa `Screen.Scaling` para pixels físicos vs. DIPs; testes reais multi-monitor continuam obrigatórios.
2. **Pesquisa global**: `Ctrl+K` coloca foco em `Pesquisar aplicações` tal como no WPF.
3. **Recentes por módulo**: lista os módulos dos documentos guardados; filtra a lista sem perder os favoritos e permite voltar a `Todos`.
4. **Responsividade**: Launcher compacto a menos de 1080 DIPs, barra lateral mais estreita, cartões e painéis mais pequenos, ações rápidas quebram linha.
5. **Regressão**: smoke tests do filtro por módulo, dimensões de layout compacto e integração da persistência de geometria.

## O que NÃO é necessário reimplementar para remover WPF

- VCC Writer, Sheets e Slides avançados **não eram interfaces WPF completas**; já existem interfaces iniciais Avalonia que são evoluções novas. O `OfficeApplicationRegistry` WPF marcava apenas Data como disponível.
- O Ribbon WPF apenas registava seleções de comandos e apresentava feedback; não era um editor de ficheiros.
- Notificações WPF apenas mostravam `Ainda não existem notificações`, não um serviço funcional de notificações.
- Recentes WPF também abria documentos com o programa associado do sistema operativo.

## Validação ainda necessária antes da retirada física

- Executar o artefacto Avalonia em Windows/Linux/macOS, testar janela com segundo monitor e diferentes escalas DPI.
- Confirmar `Ctrl+K`, filtro por módulo, favoritos, recuperação de sessões, temas e pesquisa.
- Executar fluxos VCC Data (tabelas, SQL, CSV, formulários e relatórios) com uma base de teste e comparar resultados.
- Verificar documentação, dados e rollback sem perda de ficheiros JSON e SQLite.
- Validar contraste e navegação com teclado/leitores de ecrã.
- Registar as evidências nos 13 critérios de `docs/VCC-0820-10-retirement-readiness.json` e aprovação expressa.

**O WPF não é removido neste PR enquanto faltar evidência de equivalência.**
