# VCC-0820.7 — Launcher e módulos personalizados

## Objetivo
Fazer com que os itens criados em Configuração avançada → Launcher sejam visíveis e pesquisáveis na interface Avalonia, sem confundir um registo com uma aplicação instalada.

## Comportamento
- A fonte de verdade continua a ser `%LOCALAPPDATA%/VCC Office/shell-state.json`, via `CoreShellService`.
- O Launcher reconhece os 26 módulos internos através de `OfficeApplicationRegistry` e apresenta-os nas secções normais.
- Entradas adicionadas com IDs diferentes dos internos aparecem numa secção **Personalizados** da barra lateral e em cartões dos três dashboards.
- A pesquisa considera ID, nome e descrição. A barra lateral atualiza sem recriar o campo de pesquisa.
- Após guardar a configuração e regressar à janela principal, a ativação carrega o ficheiro e atualiza a interface.
- Selecionar um item personalizado persiste a navegação, mas mostra explicitamente **Sem aplicação associada**. Não cria executáveis, não interpreta IDs como caminhos e não executa comandos externos.
- O botão **Configuração avançada** continua disponível para gerir os itens. A remoção/edição de entradas é trabalho futuro.

## Critérios automáticos
- Importação e persistência de itens personalizados pelo contrato CoreShell.
- Filtragem por descrição, case-insensitive, sem duplicar módulos internos nem settings.
- Smoke test Avalonia de botão personalizado, título e aviso de disponibilidade.
- CI Windows/Linux/macOS Debug e Release, publicação Windows standalone e regressão WPF.

## Bloqueios da remoção WPF ainda ativos
- Ligação de comandos configuráveis a aplicações reais.
- Recuperação de sessões `application-session.json` e das janelas abertas.
- Foco inicial, navegação por Tab, acessibilidade e recuperação de geometria.
- Equivalência funcional e testes manuais por módulo, instalação/rollback.

A VCC-0820.7 **não remove** a versão WPF, o seu workflow ou a solução legada.
