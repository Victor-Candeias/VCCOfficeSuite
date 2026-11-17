# VCC-0820.8 — Recuperação automática de sessões em Avalonia

## Objetivo

Reabrir automaticamente os módulos Writer, Sheets, Slides e Data que estavam ativos quando a VCC Office Suite foi fechada. A integração usa os mesmos modelos `OfficeSession`, `OfficeSessionPolicy` e `OfficeSessionStore` do projeto WPF.

## Persistência sem perda de dados

- **Ficheiro WPF original:** `%LOCALAPPDATA%/VCC Office/application-session.json`
- **Sessão Avalonia:** `%LOCALAPPDATA%/VCC Office/avalonia-application-session.json`
- No primeiro arranque Avalonia, se não existir sessão Avalonia (nem `.bak`), a sessão WPF é **lida mas nunca reescrita**.
- Os quatro módulos com interface Avalonia são filtrados de forma explícita: `writer`, `sheets`, `slides`, `data`. Módulos ainda não implementados não abrem janelas fictícias.
- Após a importação, as alterações são guardadas somente na sessão Avalonia. Isto preserva a possibilidade de rollback para WPF.
- Gravação atómica e recuperação de ficheiros corrompidos via `.bak` continuam fornecidas por `OfficeSessionStore`.

## Fluxos implementados

1. O Launcher recupera as preferências e o tema; depois recupera a sessão dos editores.
2. Cada módulo ativo é aberto uma única vez através do método `OpenModule` existente.
3. Ao abrir manualmente um módulo, o ID é acrescentado à sessão sem duplicados.
4. Ao fechar só a janela do módulo, o ID é retirado e guardado imediatamente.
5. Ao fechar a suite, o snapshot é preservado. Os eventos `Closed` das janelas-filhas não o apagam durante o fecho conjunto.
6. Um erro de permissões ao gravar a sessão não impede fechar a aplicação.

## Validação automática

- Importação da sessão WPF com quatro módulos e filtragem de IDs ainda sem editor.
- Ficheiro original WPF permanece byte a byte inalterado.
- Abrir duas vezes o mesmo módulo não o duplica no contrato de sessão.
- Fecho individual e recuperação em novo arranque.
- Recuperação do backup Avalonia quando o JSON principal está danificado.
- Smoke test headless comprova que os quatro editores estão registados, que existem os hooks da UI e que o ficheiro de sessão é separado.
- CI Windows/Linux/macOS Debug/Release, standalone Windows e regressão WPF.

## Limites / trabalhos seguintes

A sessão guarda **quais módulos estão abertos**, não o conteúdo não guardado dos editores, posição individual das janelas nem seleção interna de documentos. Não reabre automaticamente módulos que ainda não têm interface Avalonia. A equivalência total desses comportamentos, acessibilidade e migração do resto do Workspace pertence às fases seguintes da VCC-0820.

A remoção definitiva do WPF continua bloqueada até concluir os restantes requisitos de paridade.
