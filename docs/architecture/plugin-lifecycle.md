# VCC-0842 — Ciclo de vida dos plugins

## Objetivo e implementação

O Launcher continua a carregar plugins Avalonia como DLLs independentes, com ícones, nomes, tooltips e janelas fornecidos pelo próprio plugin. Esta evolução acrescenta validação declarativa de compatibilidade, dependências entre módulos, assinatura dos ZIPs, staging selado, backup de versão anterior, rollback, cancelamento de operações pendentes e identificação de plugins que não conseguiram carregar.

## Manifesto v1 (campos adicionais opcionais)

```json
{
  "id": "example",
  "assembly": "Vcc.Office.Example.dll",
  "entryType": "Vcc.Office.Example.ExamplePlugin",
  "apiVersion": 1,
  "minHostVersion": "1.0",
  "maxHostVersion": "3.0",
  "requires": [
    { "id": "writer", "minVersion": "1.0" }
  ]
}
```

Os manifestos existentes mantêm a compatibilidade. O Host resolve primeiro os plugins exigidos, bloqueia dependências ausentes/circulares ou com versão insuficiente e não permite desativar ou remover plugins ainda utilizados por outros. Cada plugin inválido ou que não carregue aparece no gestor como indisponível e pode ter a remoção agendada, sem executar a sua DLL.

## Verificação de assinatura

O gesto **Instalar / atualizar plugin** exige, por predefinição, um ZIP assinado:

- O ficheiro `plugin.zip.sig`, na mesma pasta que `plugin.zip`, contém uma assinatura binária RSA-PSS com SHA-256 codificada em Base64.
- A assinatura é feita sobre o hash SHA-256 **dos bytes integrais do ZIP**.
- A chave pública do editor, no formato PEM SubjectPublicKeyInfo, deve estar previamente instalada em `<Plugins>/.vcc-updates/trusted-signers/<editor>.pem`. É possível configurar até 32 chaves públicas.
- Sem assinatura válida ou sem chave de confiança, a instalação é recusada. No gestor existe uma opção explícita para **permitir instalar ZIP sem assinatura confiável**; essa exceção só deve ser usada para plugins criados localmente ou totalmente confiáveis. Uma assinatura existente mas inválida nunca é ignorada.
- O pacote é verificado de novo após a extração e os ficheiros preparados são selados com hashes para detetar adulteração antes de serem ativados no arranque seguinte.

A lista de confiança é gerida localmente. Não é uma PKI central nem verifica revogação/certificados X.509. O editor e a chave pública precisam de ser verificados por canal independente; hashes e assinaturas não garantem ausência de código malicioso.

## Instalação e recuperação

1. O ZIP é inspecionado (limites de entradas e tamanho, manifesto, ficheiros DLL, caminhos e symlinks) e preparado em `Plugins/.vcc-updates/staged`.
2. A operação é colocada em fila; pode ser cancelada antes do próximo arranque.
3. Na inicialização seguinte o Host valida os hashes do staging, a versão do Host e as dependências já instaladas.
4. A pasta anterior do plugin é preservada como `backup-<uuid>`, sem ficar carregada como plugin.
5. O gestor oferece **Recuperar versão anterior**, agendado para o arranque seguinte. Retém-se uma versão anterior por plugin e o rollback pode ser invertido por nova recuperação.
6. Operações que falhem permanecem pendentes para diagnóstico; o Launcher continua a arrancar com os restantes plugins.
7. Falhas de carregamento são apresentadas no inventário, com opção de remover o plugin avariado.

## Limites de segurança e operação

- Os plugins continuam a executar **no mesmo processo e com as permissões da VCC Office Suite**. Um `AssemblyLoadContext` não é uma sandbox. Isolamento real por processo requer um futuro modelo IPC e UI remoto.
- O rollback das DLLs apenas é aplicado antes do carregamento no arranque seguinte. Não interrompe janelas ou documentos abertos.
- Não há marketplace remoto, instalação de dependências a partir da internet, atualização automática de terceiros nem revogação online de chaves de assinatura.
- Uma pasta de instalação sem permissões de escrita pode impedir a instalação, a remoção ou a recuperação; as operações não elevam privilégios.
- As dependências de plugins são especificadas por ID e versão mínima do assembly; a compatibilidade funcional continua a exigir testes.
- A fila e os backups são locais. A gestão de armazenamento dos backups antigos e a sua limpeza podem ser evoluídas separadamente.

## Validação

Os testes automatizados verificam assinatura RSA-PSS válida e inválida, regras de compatibilidade, dependência que bloqueia remoção, deteção de falhas de carregamento, rejeição de staging adulterado e recuperação de um plugin removido. A CI existente cobre Windows, Linux, macOS, empacotamento e CodeQL.
