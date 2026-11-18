# VCC-0833 — Centro de notificações

## Âmbito

A aplicação base disponibiliza um centro de notificações local, persistente em SQLite (migração 6) com categorias Sistema, Documento, Plugin e Operação, e gravidades Informação, Sucesso, Aviso e Erro. A faixa superior mostra a contagem de notificações não lidas.

O utilizador pode pesquisar, filtrar por categoria/gravidade/não lidas, marcar uma como lida ou não lida, marcar todas como lidas, eliminar individualmente e limpar as lidas. O histórico persiste após reiniciar o Launcher.

## Integrações

- Avisos e erros já apresentados por `ShowNotice` são também registados como notificações persistentes.
- Abre-se um documento com sucesso → notificação de documento.
- Cria-se um documento a partir de um modelo → notificação de operação.
- Operações no gestor de plugins (ativação, desativação, instalação/atualização agendada, remoção agendada e falhas) → notificações da categoria Plugin.
- Problemas de carregamento de plugins detetados no arranque são reportados uma vez por sessão.

Não são enviados dados para a cloud. Não guarda conteúdos de documentos, chaves privadas ou tokens. Mensagens e títulos são limitados em comprimento. O número de notificações lidas guardadas é limitado para controlar crescimento do SQLite; entradas não lidas nunca são apagadas pela retenção automática.

## Limites

Esta fase não inclui notificações push do sistema operativo, sincronização entre dispositivos ou eventos de progresso emitidos por DLLs durante operações internas (necessitam de um contrato adicional). Se a persistência do centro falhar, não deve bloquear a utilização dos documentos ou plugins.

## Testes

Cobertura de persistência SQLite, contagens, filtros, marcar lido/não lido, limpeza seletiva, limites, categorias, e smoke tests da vista e do cabeçalho em Avalonia.
