# VCC-0820 — Inventário de paridade funcional WPF / Avalonia

## Regra de migração
**Preservar todas as funções WPF existentes**, não apenas os dados. A remoção de `src/Vcc.Office.Shell` e do pipeline Windows legado fica bloqueada até os fluxos abaixo serem implementados e validados em Avalonia.

## Inventário comparativo

| Área | Implementação WPF | Avalonia | Situação |
| --- | --- | --- | --- |
| Dashboard moderno, clássico, grelha | `MainWindow` | `MainWindow` | Implementado; validação manual por plataforma necessária |
| Recentes, favoritos, seleção de ficheiros | `MainWindow` | `MainWindow.Documents` | Implementado; abertura externa e recuperação a rever |
| Tema e dashboard inicial | Janelas de settings WPF | `MainWindow.Settings` | Implementado |
| Ribbon: separadores, comandos e pesquisa | `WorkspaceWindow` | Editor de configuração em `WorkspaceSettingsWindow` | **Parcial**: falta ribbon funcional visível, pesquisa e despacho de comandos |
| Launcher personalizável e pesquisa | `WorkspaceWindow` | Editor de configuração e launcher Avalonia | **Parcial**: módulos configurados no shell-state não alimentam ainda o catálogo visual de 26 módulos |
| Atalhos configuráveis | `WorkspaceWindow` | Editor e despacho de open-settings/open-launcher | **Parcial**: comandos arbitrários não são executados, têm de ter handlers reais |
| Acessibilidade: alto contraste, movimento reduzido | `WorkspaceWindow` | Preferências guardadas | **Parcial**: aplicação efetiva do contraste/movimento em toda a UI ainda por validar |
| Navegação: foco inicial e wrap | `WorkspaceWindow` | Preferências guardadas | **Parcial**: política de foco e ciclo ainda não está aplicada no UI |
| Layout da janela e ações centrar/restaurar/maximizar | `WorkspaceWindow` | Preferências de dimensões | **Parcial**: ações e recuperação geométrica ainda por implementar |
| Data: tabelas, SQL, importação/exportação CSV | `WorkspaceWindow` | `DataWindow` | Implementado, validar resultados e erros |
| Data: formulários e relatórios | `WorkspaceWindow` | `DataWindow` | Implementado, validar paridade e dados existentes |
| Writer: TXT/DOCX inicial | CoreShell + implementação inicial Avalonia | `WriterWindow` | Editável em Avalonia; fidelidade de DOCX e edição rica incompletas |
| Sheets: fórmulas, importação XLSX | CoreShell | `SheetsWindow` | Inicial; edição de grelha avançada incompleta |
| Slides: PPTX e objetos | CoreShell | `SlidesWindow` | Inicial; editor visual e apresentação incompletos |

## Fase atual: migração do Workspace

- O painel de opções avançadas usa o mesmo caminho de estado WPF: `%LOCALAPPDATA%/VCC Office/shell-state.json`.
- Não substitui nem apaga ficheiros existentes; usa `ShellStateStore` e `CoreShellService` com validação e backup atómico.
- Foram migrados os ecrãs de configuração de Ribbon, atalhos, Launcher, acessibilidade, navegação e geometria da janela.
- Foi adicionado despacho real dos atalhos conhecidos, sem afirmar execução de comandos desconhecidos.
- Testes de recuperação de dados e smoke tests headless cobrem a estrutura inicial; testes manuais continuam necessários.

## Critérios indispensáveis para a remoção definitiva

1. Mapear cada handler WPF (`WorkspaceWindow.xaml.cs`, `MainWindow.xaml.cs`, dashboards) para equivalente Avalonia e teste de comportamento.
2. Exercitar presencialmente cada fluxo necessário em Windows 11, Ubuntu e macOS, incluindo temas, atalhos e navegação por teclado.
3. Garantir retenção de `shell-state.json`, preferências SQLite, históricos, favoritos, ficheiros e sessões de recuperação; validar rollback.
4. Aprovar a equivalência do Ribbon, ações de janela, acessibilidade e restantes itens atualmente marcados `Parcial`.
5. Só então retirar `src/Vcc.Office.Shell`, `.github/workflows/build.yml` e o antigo `VCC.Office.sln` ou substituí-lo pelo equivalente Avalonia.
6. Executar CI Debug e Release nas três plataformas e distribuição autónoma Windows; merge apenas com testes a verde.

**Estado da VCC-0820: em curso. O WPF permanece nesta fase, sem remoção prematura.**
