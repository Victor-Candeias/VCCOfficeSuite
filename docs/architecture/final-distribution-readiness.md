# VCC-0836 a VCC-0839 — Preparação da suite

## VCC-0836 — Gestão avançada de plugins

O gestor mostra ID, versão, dependências, estado e problemas de carregamento. As operações de instalação, atualização e remoção são preparadas para o próximo arranque, sem sobrescrever DLLs carregadas. O utilizador pode cancelar uma operação ainda pendente, removendo o respetivo staging com validação do identificador. O cancelamento não desfaz uma alteração já aplicada.

Plugins de terceiros executam código no processo da suite: a validação de estrutura de ZIP **não prova confiança ou segurança da origem**. Instalar apenas pacotes confiáveis.

## VCC-0837 — Personalização do Launcher

Preferências persistentes em SQLite: dashboard inicial/preferido, tema, densidade do menu lateral (normal/compacta) e apresentação da faixa de boas-vindas. A definição de tema mantém as opções Sistema, Claro e Escuro. A normalização preserva as escolhas guardadas e não altera módulos/plugin DLLs.

## VCC-0838 — Armazenamento cloud

Nesta etapa existe integração com **pastas locais já sincronizadas** por clientes externos (por exemplo, OneDrive ou Dropbox). A seleção da pasta e dos documentos é manual. A VCC Office cria cópias com nomes exclusivos, preservando os documentos de origem, sem guardar tokens nem passwords. A presença local do ficheiro **não confirma que foi enviado à cloud**: o sincronizador externo determina o estado remoto.

Esta integração não implementa login OAuth, listagem de ficheiros remotos, partilha nem sincronização direta pelas APIs dos fornecedores. Essas capacidades necessitam de implementação adicional com autenticação específica.

## VCC-0839 — Preparação para distribuição final

O pipeline Official Release chama `scripts/check-final-release-readiness.py` antes de compilar. A verificação exige ficheiros e serviços essenciais (solução Avalonia, atualização, diagnóstico, cloud, plugins, segurança de release e assinatura), e pode verificar a estrutura de um ZIP. Falhas de pré-requisitos interrompem o pipeline.

Mantêm-se as verificações de dependências NuGet, testes CoreShell e Avalonia, validações de limites entre host e plugins, checksums dos artefactos e assinatura obrigatória para releases públicas Windows.

### Critérios de saída antes de distribuição real

1. Pipelines de Windows, Linux e macOS terminados com sucesso.
2. Release criada por uma tag válida que pertence a `main`.
3. Certificado Code Signing apropriado para o público-alvo e assinatura validada.
4. Testes de arranque, abertura de documentos e plugins em máquinas limpas.
5. Verificação dos hashes publicados, atualizações e instalação manual.
6. Avaliação de segurança dos plugins externos e dos pacotes.
7. Plano de rollback e assistência para erros de atualização.

**Não considerar distribuição final concluída apenas com a passagem da verificação estrutural.** O processo ainda depende de testes reais de instalação e da existência de releases oficiais publicadas.
