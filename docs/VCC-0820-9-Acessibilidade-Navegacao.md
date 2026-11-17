# VCC-0820.9 — Paridade de navegação e acessibilidade (Avalonia)

## Objetivo

Aplicar à interface Avalonia as opções WPF que já eram persistidas pelo `CoreShellService`, incluindo alvo de foco inicial, ciclo de Tab, alto contraste e preferência de movimento reduzido. As preferências usam o ficheiro partilhado `%LOCALAPPDATA%/VCC Office/shell-state.json`.

## Comportamentos implementados nesta fase

| Requisito | Implementação | Verificação |
| --- | --- | --- |
| Foco inicial Launcher/Ribbon | Botões concretos identificados na barra lateral; após carregar o ShellState, foco é aplicado com prioridade Input | Smoke de presença e nomes dos alvos; validação de foco real ainda manual |
| Navegação Tab: ciclo ligado/desligado | `KeyboardNavigation.SetTabNavigation` no contentor principal (Cycle/Continue) | Smoke do comportamento predefinido e método de aplicação |
| Acessibilidade do Launcher | Nomes para pesquisa, entradas de navegação e módulos, via `AutomationProperties.SetName` | Smoke de metadados |
| Configuração avançada | Campos, ações, preferências de foco, contraste e movimento com nomes acessíveis | Inspeção de UI / teste de leitor de ecrã ainda manual |
| Movimento reduzido | Remove transições locais dos controlos da árvore do Launcher, quando a preferência está ativa | Teste de compilação e UI; efeitos nativos e módulos ainda não cobertos |
| Alto contraste | Cores do Launcher já eram adaptadas pelo `HighContrast` da shell | Testes de contraste visual e componentes externos ainda pendentes |
| Persistência | Reutilização do `ShellStateStore` entre WPF e Avalonia | Teste de roundtrip e de desativação |

## Cenários manuais necessários para fechar paridade

- [ ] Com opção **Foco inicial: Launcher**, iniciar a aplicação e observar o botão de Launcher com foco.
- [ ] Com opção **Foco inicial: Ribbon**, iniciar e observar o botão Ribbon com foco.
- [ ] Com `WrapFocus` ativo, percorrer todos os controlos do Launcher com Tab/Shift+Tab e verificar ciclo; desativar e confirmar navegação normal.
- [ ] Ativar/desativar alto contraste, reabrir o Launcher e verificar menus, cartões, ícones, diálogos e os quatro editores.
- [ ] Ativar movimento reduzido e verificar temas, seleção de módulos, Splash, Ribbon e todos os restantes módulos.
- [ ] Utilizar leitor de ecrã em Windows e tecnologias equivalentes em macOS/Linux com navegação por teclado.
- [ ] Confirmar que Writer, Sheets, Slides e Data preservam dados, atalhos e navegação sem regressões.
- [ ] Testar janelas com escalamento DPI e monitores múltiplos, recuperação de sessão, ficheiros recentes e favoritos.
- [ ] Validar atualização/rollback entre WPF e Avalonia.

## Limites importantes

Os testes de CI são builds, testes de contratos e smoke tests **headless**; não demonstram comportamento completo de leitores de ecrã ou equivalência visual. A preferência de movimento reduzido é aplicada à árvore atual do Launcher, não a todas as janelas independentes nem a transições da plataforma. O alto contraste está implementado principalmente na shell; ainda precisa de cobertura por todos os módulos.

**O WPF e os pipelines legados não devem ser removidos até os critérios manuais e funcionais restantes estarem concluídos.**
