# VCC-0828.1 — Testes isolados de assinaturas autoassinadas

## Fronteira de confiança

Um certificado Code Signing autoassinado não é confiável para o público por defeito. O modo SelfSignedTest serve exclusivamente para testes isolados.

- TrustedRelease exige Get-AuthenticodeSignature com estado Valid. Uma cadeia autoassinada não confiável bloqueia a publicação.
- SelfSignedTest exige identidade do signatário idêntica ao certificado PFX (thumbprint), certificado ainda válido, EKU Code Signing, cadeia de um único elemento validada por CustomRootTrust em memória e estado Authenticode Valid ou NotTrusted.
- Binários adulterados (HashMismatch), não assinados, assinados por terceiros ou com cadeia inválida falham.
- Não instala o certificado no armazenamento de autoridades de confiança do Windows.
- Os GitHub Actions secrets apenas chegam ao job Windows numa tag ou num workflow_dispatch explícito; nunca aos PRs.
- A CI Windows cria certificado temporário de curta duração, assina uma cópia do executável e testa rejeição de adulteração e de uso em TrustedRelease.

## Como testar os secrets reais

1. No GitHub → Actions → Official Release → Run workflow, selecionar main.
2. Ativar o parâmetro Test repository self-signed PFX on Windows (no public release).
3. Confirmar que os dois secrets WINDOWS_SIGNING_PFX_BASE64 e WINDOWS_SIGNING_PFX_PASSWORD estão preenchidos.
4. Verificar a etapa Sign and verify Windows binaries do job Package win-x64.
5. Os ZIPs são produzidos para teste, sem criar GitHub Release pública.

Os testes automáticos do PR usam certificados temporários; não comprovam o conteúdo dos secrets reais. O teste real protegido requer execução manual.

## Releases públicas

A publicação por tag continua a exigir TrustedRelease, com cadeia confiável. Um certificado autoassinado não confiável faz falhar o job Windows e impede a GitHub Release.
Um certificado autoassinado não elimina os avisos Chrome/SmartScreen para o público.
