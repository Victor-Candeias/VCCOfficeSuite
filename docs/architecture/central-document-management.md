# VCC-0831 — Gestão centralizada de documentos

A vista **Ficheiros recentes / Favoritos** do Launcher passa a ser um gestor documental dedicado, sem manipular diretamente o conteúdo dos ficheiros.

- Pesquisa manual por nome e caminho, com filtros de módulo, extensão, disponibilidade (presente/em falta), período (7/30 dias), favoritos, fixados e ordenação (data ascendente/descendente, nome e formato).
- Documentos fixados aparecem no topo, independentemente da ordenação; a preferência `is_pinned` é persistida na migração SQLite 5 e reabrir um documento não remove favoritos ou fixação.
- Referências a documentos movidos ou eliminados são visíveis com estado «Em falta», não podem ser abertas diretamente e oferecem **Localizar**, para selecionar explicitamente outro ficheiro local da mesma extensão.
- A operação RelinkAsync altera apenas metadados (caminho, nome, associação de módulo) e conserva favorito, fixado e data da última abertura; recusa destinos já catalogados e nunca move nem substitui ficheiros.
- A limpeza das referências em falta elimina apenas as linhas do catálogo, sem apagar documentos locais. A lista retém até 200 entradas normais, preservando favoritos e fixados; consultas são limitadas a 1000 registos.
- A abertura de documentos continua a utilizar o encaminhamento por capacidades do VCC-0830. Os ficheiros são registados apenas após abertura bem-sucedida.
- Testes cobrem migração, persistência, pesquisa, ordenação, recuperação, tratamento de duplicados, limpeza e estrutura da vista no Launcher.

Nota: esta funcionalidade é um catálogo local, não inclui sincronização cloud, indexação do conteúdo dos ficheiros ou uma pesquisa global de pastas no disco.
