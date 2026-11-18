# VCC-0828 — Segurança e distribuição oficial

## Objetivo e limites

O ZIP do GitHub Actions não é assinado, não tem reputação estabelecida e pode gerar avisos no Chrome e SmartScreen. Um build com sucesso não é uma análise antimalware nem uma garantia de segurança. Os checksums SHA-256 provam integridade **quando comparados com uma referência confiável**; não autenticam por si só o editor.

## Pipeline

- A CI existente executa testes de integridade e empacotamento do VCC-0828 em Windows, Linux e macOS.
- A release oficial é gerada exclusivamente por **tags existentes** `vN.N.N` associadas ao `main`, usando `.github/workflows/release.yml`. Nunca é criada apenas por fazer push para o `main`.
- O workflow recria e testa os pacotes nas três plataformas, consulta vulnerabilidades NuGet transitivas, valida DLLs de plugins e gera ZIPs por plataforma com manifestos SHA-256 internos.
- O job final publica três arquivos ZIP e `SHA256SUMS.txt` no **GitHub Releases**. Verificação local: `sha256sum -c SHA256SUMS.txt` no diretório dos três ZIPs. Cada ZIP também contém hashes de todos os ficheiros.
- `workflow_dispatch` permite testar a criação de pacotes **sem publicar uma release**.

## Assinatura digital Windows — requisito para releases públicas

As releases por tag **falham fechadas** se não houver certificado. Configurar os GitHub Actions secrets:

- `WINDOWS_SIGNING_PFX_BASE64`: certificado PFX com chave privada, codificado em base64
- `WINDOWS_SIGNING_PFX_PASSWORD`: password do PFX

O runner Windows assina `VCC Office Suite.exe` via Authenticode/SHA-256, aplica timestamp RFC 3161 e valida a assinatura antes de gerar o ZIP. O PFX fica apenas num ficheiro temporário eliminado no mesmo job. Nunca guardar o certificado nem password no repositório.

Para novas distribuições, **Azure Artifact Signing** é uma alternativa preferível que requer integração específica e credenciais próprias (não incluída neste desenvolvimento). Mesmo um executável assinado pode continuar a mostrar alertas SmartScreen até adquirir reputação; a assinatura não elimina automaticamente avisos do Chrome.

## Publicação de uma versão

1. Confirmar que `main` tem a CI aprovada e os dois secrets estão configurados.
2. Criar uma tag de versão semântica no commit aprovado do `main` (ex.: `v0.8.28`) e enviá-la para `origin`.
3. Verificar o workflow **Official Release**. Sem certificado, o job Windows falha e **nenhuma release é publicada**.
4. Descarregar da página **Releases** da versão, verificar o ficheiro de hashes e, no Windows, confirmar a assinatura em Propriedades > Assinaturas digitais.

## Pendências de segurança

Este workflow não faz análise dinâmica de malware, sandboxing de plugins, nem prova que o código está isento de ameaças. A assinatura verifica a identidade do editor e a integridade do executável, não o seu comportamento. Para distribuição pública consolidada, recomendar também proteção de tags, revisão obrigatória de PR, ferramentas de análise de código, digitalização antimalware complementar e proteção da chave de assinatura.
