# VCC-0820.10 — Retirada do WPF: critérios e bloqueios

## Resultado da auditoria (17-11-2026)

**A remoção física do WPF não está aprovada.** O código de migração existe, mas a equivalência funcional completa não foi demonstrada em ambiente real. Os testes automatizados recentes provam compilação, contratos e smoke tests; não validam integralmente experiência de utilizador, apresentação, leitores de ecrã nem rollback. A intenção explícita da VCC-0820 é **não perder nenhuma funcionalidade WPF**.

### Lacunas identificadas diretamente no código

| Funcionalidade WPF | Estado Avalonia | Ação antes de eliminar |
|---|---|---|
| `MainWindow.RestoreWindow/SaveWindow` e DPI/multi-monitor | Ações pontuais de janela, sem restauro completo equivalente no arranque | Guardar/restaurar posição e tamanho e testar monitores/DPI |
| `MainWindow`: notificações, popup de mais aplicações, pesquisa global, atalhos e redimensionamento adaptativo | Launcher e dashboards próprios mas nem todos os fluxos WPF equivalentes | Inventariar eventos e critérios de aceitação |
| `WorkspaceWindow`: Data queries, formulários e relatórios | `DataWindow` Avalonia existente | Exercitar com dados reais e comparar operações, resultados e erros |
| `WorkspaceWindow`: Ribbon/Launcher/atalhos | Janelas Avalonia iniciais e CoreShell partilhado | Validar execução efetiva de comandos e feedback |
| Acessibilidade/foco cíclico | VCC-0820.9 implementa política inicial, mas não há testes reais de leitor de ecrã | Testes de teclado, leitores de ecrã e alto contraste |
| Writer, Sheets, Slides | Editores Avalonia iniciais | Confrontar funções WPF realmente existentes; testar ficheiros, compatibilidade, dados e perdas |
| Recentes, favoritos e filtros | Serviços partilhados; UX distinta | Validar fluxos equivalentes |
| Migração e rollback | O ficheiro de sessão Avalonia é separado e importa o legado em leitura | Verificar todos os ficheiros SQLite/JSON, atualização e rollback |
| Linux/macOS | CI verde em Debug e Release | Testes reais de interface |

### Proteção implementada

- `scripts/check-wpf-retirement.py` executa em CI e impede a retirada de `Vcc.Office.Shell` sem **13 confirmações explícitas**, cada uma com prova associada.
- O manifesto `docs/VCC-0820-10-retirement-readiness.json` começa com tudo por aprovar. Não são fabricados resultados.
- Após aprovação da retirada, o gate exige também a remoção do workflow WPF, a solução canónica Avalonia e ausência de referências a `Vcc.Office.Shell` em outros projetos.
- O pipeline legado publica artefactos como **`VCCOfficeSuite-Legacy-WPF-...`**, distinguindo-os dos de Avalonia.
- Estes controlos **não substituem revisão de código e aprovação humana**. O manifesto sozinho não prova a qualidade das evidências.

### Sequência de conclusão

1. Completar as lacunas com PRs específicos e cobertura automática.
2. Executar checklist manual Windows 11, Linux e macOS e testes de dados e rollback, anexando links/relatórios ao manifesto.
3. Obter aprovação explícita e preencher o manifesto com evidências verificáveis.
4. Só então criar PR de retirada: remover `src/Vcc.Office.Shell`, `.github/workflows/build.yml`, promover `VCC.Office.Avalonia.sln` para `VCC.Office.sln`, atualizar documentação e scripts.
5. Executar os pipelines canónicos e validar o artefacto de distribuição e uma instalação limpa.

**O projeto e o workflow WPF permanecem enquanto qualquer item não estiver comprovado.**
