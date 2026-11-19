# VCC-0835 — Sistema de atualização da VCC Office Suite

## Comportamento nesta fase

A janela **Atualizações** no Launcher permite verificar a versão estável mais recente nas releases oficiais de `Victor-Candeias/VCCOfficeSuite`. A procura é sempre iniciada pelo utilizador, nunca silenciosamente. Em cada arquitetura suportada seleciona o ZIP específico e obtém o ficheiro `SHA256SUMS.txt`.

Se o utilizador autorizar o download, a aplicação descarrega para uma área de staging local, calcula SHA-256 e só disponibiliza o ZIP após comparação com o manifesto. Downloads interrompidos ou com hash incorreto são eliminados; o pacote anterior preserva-se enquanto a verificação falha. **O sistema não descompacta, executa, instala, substitui executáveis em uso, nem eleva privilégios.** O utilizador fecha a aplicação e segue a instalação manual da release.

## Segurança e limites

- API fixa em `https://api.github.com/repos/Victor-Candeias/VCCOfficeSuite/releases/latest`.
- URLs dos assets obrigatoriamente HTTPS, no domínio e pasta de releases oficiais desse repositório.
- Canal estável: releases marcadas `draft` ou `prerelease` não são oferecidas.
- Tamanho máximo 1 GiB por ZIP e 128 KiB no manifesto; nome de ficheiro sem subdiretórios.
- SHA-256 verifica **integridade**, mas como o manifesto e o ficheiro têm a mesma origem, não constitui uma assinatura independente de autenticidade. Conferir assinatura Authenticode quando disponível.
- Não existe auto-update, execução automática de scripts, nem atualizações silenciosas.
- Se o GitHub estiver indisponível, a aplicação continua funcional; o utilizador pode repetir a consulta.
- Nesta fase não existe política de rollback de binários ou atualização individual de plugins.

## Versões

Nas builds oficiais por tag, o workflow configura `VCC_RELEASE_VERSION`, usado pelo projeto Desktop para incorporar a versão da tag no assembly. Builds de desenvolvimento não são publicadas como releases oficiais.

## Testes

Testes CoreShell com respostas HTTP simuladas para a política de versões estáveis, consulta à release, validação de hashes e recusa/limpeza de pacote adulterado; smoke Avalonia para a entrada de Atualizações. O pipeline existente cobre Windows, Linux e macOS.
