# VCC-0843 — Personalização avançada do Launcher

## VCC-0843.1 — Organização das aplicações

- Os cartões das aplicações instaladas e os botões dos módulos no menu lateral suportam arrastar e largar, usando um identificador interno `vcc-office/application-id`; não são aceites caminhos nem comandos externos.
- A ordenação é comum aos três dashboards e ao menu lateral. Plugins acabados de instalar são acrescentados depois dos IDs conhecidos, sem alterar as preferências anteriores.
- O menu de contexto (botão direito) permite **Fixar nos favoritos**, **Retirar dos favoritos**, **Ocultar aplicação** e **Mostrar aplicação**. Os favoritos surgem numa área dedicada do menu lateral.
- Aplicações ocultas deixam de ser listadas nos dashboards e no menu lateral, mas continuam instaladas e podem ser encontradas através da pesquisa do catálogo.
- Remover um plugin não elimina as suas preferências; quando voltar a ser instalado, a ordem e o estado podem ser recuperados.
- A ordenação e os favoritos não afetam as próprias DLLs, descrições ou ícones dos plugins.

## VCC-0843.2 — Personalização visual

- Tamanhos de cartões: pequeno, médio e grande.
- Densidade visual: confortável ou compacta.
- Estilo: normal ou minimalista, ocultando informação secundária.
- Menu lateral expandido ou recolhido para ícones com tooltips e nomes acessíveis. Há um botão no topo para alternar rapidamente.
- As preferências são configuradas na janela **Definições** e persistem entre arranques.

## VCC-0843.3 — Perfis e persistência

A opção **Perfis do Launcher**, também acessível nas Definições, permite criar, substituir, selecionar, aplicar e eliminar perfis nomeados sem reiniciar a aplicação. A troca aplica o dashboard, tema, organização, favoritos, visibilidade, densidade e tamanho de cartões.

- Perfis gravados em SQLite na tabela `app_settings`, chave `avalonia.launcher.profiles.v1`. O perfil ativo é referenciado no estado do Launcher, chave `avalonia.launcher.session.v1`.
- No máximo 24 perfis e nome de 1 a 48 caracteres válidos. IDs de aplicações são filtrados, desduplicados e limitados.
- Exportação/importação JSON com validação de versão do dashboard e opções conhecidas. Não são exportados documentos, caminhos de ficheiros, tokens, sessões ou configurações internas dos plugins.
- O perfil guarda uma **fotografia das preferências**. Para atualizar um perfil já existente, utilizar **Guardar perfil** com o mesmo nome.

## VCC-0843.4 — Estabilização e recuperação

- O estado SQLite anterior é preservado como `avalonia.launcher.session.backup.v1` antes de alterações; se o estado principal contiver JSON inválido, é lida a última cópia válida.
- A lista de perfis tem backup em `avalonia.launcher.profiles.backup.v1`. Um JSON importado incompatível é rejeitado antes de substituir dados.
- Se não existir nenhum backup válido, o Launcher mantém os valores predefinidos; nunca executa caminhos de documentos a partir de um perfil.
- Testes CoreShell para drag & drop, favoritos, estado SQLite, import/export e recuperação; testes headless Avalonia para navegação e menu recolhido.
- Validação obrigatória: pipelines Windows, Linux, macOS, empacotamento e CodeQL antes do merge.

## Limitações

A personalização aplica-se ao Launcher e não ao conteúdo ou às configurações internas de Writer, Sheets ou outros plugins. Os perfis não têm sincronização cloud nem partilha entre utilizadores. A ordenação é persistente no dispositivo local. Não existe personalização de cores arbitrárias fora dos temas já suportados.
