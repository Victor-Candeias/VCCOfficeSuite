# VCC-0834 — Diagnóstico e recuperação

## Objetivo

Adicionar ao Launcher Avalonia uma área de diagnóstico local para identificar problemas de integridade e oferecer uma recuperação explícita e não destrutiva do estado da interface.

## Verificações

- Diretório local de dados (existência).
- Base SQLite, aberta apenas em modo de leitura e verificada com `PRAGMA quick_check`.
- Ficheiro JSON do estado do Launcher e existência de backup quando está inválido.
- Ficheiro JSON da sessão Avalonia.
- Número de snapshots de recuperação pendentes na base SQLite.

As verificações não criam nem corrigem ficheiros. Os estados são Healthy, Warning e Error. Avisos iniciais (por exemplo, ainda não existir base SQLite) não são tratados como corrupção.

## Recuperação

O botão **Recuperar estado da interface a partir do backup** exige confirmação. Valida o JSON e as invariantes do estado contido no ficheiro `shell-state.json.bak`. Antes de restaurar, preserva o ficheiro atual com sufixo `.before-recovery-<timestamp>`. A cópia do backup é colocada atomicamente em `shell-state.json`; o backup original não é sobrescrito. É necessário reiniciar a suite para aplicar o estado recuperado.

Não modifica documentos, sessões de plugins nem a base SQLite. Não executa comandos externos nem inclui telemetria/cloud.

## Testes

- Primeiro arranque: diagnóstico apenas de leitura, sem criar pastas.
- SQLite válida e JSON válido.
- JSON corrompido e recuperação bem-sucedida com cópia de segurança.
- Backup corrompido rejeitado sem modificar o estado atual.
- Smoke Avalonia: navegação, apresentação e saída da página.
- CI Windows, Linux e macOS conforme workflows existentes.

## Limitações desta fase

O diagnóstico de sessão verifica a estrutura JSON, não o catálogo de plugins instalado. Os snapshots são apenas contabilizados (não são restaurados automaticamente). A recuperação de configurações exige confirmação explícita e reinício.
