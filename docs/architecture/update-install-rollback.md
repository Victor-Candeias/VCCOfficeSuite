# VCC-0841 — Instalação de atualizações com backup e rollback

## Fluxo

1. O utilizador procura uma release oficial através do VCC-0835 e descarrega o ZIP, cuja integridade SHA-256 é confirmada pelo manifesto publicado no GitHub.
2. Escolhe **Instalar ao fechar (com rollback)**, lê o aviso para guardar os documentos e confirma.
3. `SuiteUpdateInstaller.PrepareAsync` volta a verificar o hash do ZIP, valida cada entrada e o manifesto de hashes interno e extrai a versão para um diretório temporário **irmão** da instalação existente.
4. O Launcher cria uma cópia auxiliar da instalação atual fora do diretório a substituir, grava o plano e lança o processo auxiliar sem privilégios elevados.
5. A aplicação fecha; o auxiliar aguarda até 120 segundos pelo processo original. Se continuar ativo, cancela sem modificar a instalação.
6. O auxiliar volta a verificar os ficheiros preparados, move a instalação anterior para `.vcc-backup-<id>` e move a nova versão para o local original.
7. O auxiliar executa uma verificação de integridade e carregamento básico do novo executável. Se falhar, restaura a instalação anterior. Se passar, reinicia a suite. O backup anterior permanece disponível.

## Segurança

- **Nunca** substitui um executável carregado: o auxiliar é uma cópia isolada da própria versão instalada, não código executado a partir do ZIP descarregado.
- O ZIP é verificado duas vezes por SHA-256 (exterior e entradas internas), com proteção contra path traversal, duplicações, links simbólicos e expansão excessiva.
- Não executa scripts da release e não solicita elevação; instalações em diretórios protegidos falham sem modificar a instalação existente.
- Dados de utilizador, sessões, base SQLite e preferências são guardados nos diretórios de perfil existentes, fora da pasta da aplicação; não são migrados nem eliminados pelo instalador.
- Não há instalação silenciosa. O utilizador confirma explicitamente que guardou os documentos antes da saída.
- A verificação pós-instalação é um **smoke test de carregamento das dependências e do executável**, não um teste visual integral de todos os editores.
- O backup da versão anterior é preservado; a limpeza de backups antigos e o restauro manual por interface ficam para evolução futura.
- Os hashes apenas garantem integridade relativamente ao manifesto descarregado; não substituem uma assinatura de editor confiável.
- O mecanismo só funciona para instalações portáteis com permissões de escrita; instaladores geridos pelo sistema e pastas de sistema exigem mecanismo de atualização próprio.

## Testes automatizados

- Preparação sem modificar a versão instalada.
- Validação externa do checksum.
- Bloqueio de ZIPs com `../`.
- Aplicação offline com backup.
- Rollback automático após falha da verificação de arranque.
- Preservação dos ficheiros da versão anterior.

## Limitações

Não há recuperação automática de documentos não guardados, assinatura adicional dos pacotes pelo atualizador nem um mecanismo de atualização privilegiada para pastas protegidas. O teste pós-instalação cobre arranque técnico mínimo, não desempenho ou navegação real da interface. Testes reais de instalação em máquinas limpas continuam necessários antes de uma release pública.
