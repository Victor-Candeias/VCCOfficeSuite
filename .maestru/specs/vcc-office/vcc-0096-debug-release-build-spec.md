---
maestru: "0.4"
type: work-spec
id: vcc-0096-debug-release-build-spec
title: "VCC-0096 — Pipeline de build Debug e Release"
template: implementation-plan-v1
work-item: vcc-office/VCC-0096
owner: developer
created: 2026-11-10
---

# VCC-0096 — Pipeline de build Debug e Release

## Overview

Criar um workflow GitHub Actions para compilar a aplicação WPF em Debug e Release, executar o programa de testes existente e disponibilizar publicações autónomas para Windows x64. O workflow deve funcionar em pushes, pull requests e execução manual, com artefactos separados por configuração.

## Implementation

### Phase 1: Pipeline e documentação

#### Step 1.1: Configurar as duas builds

| Action | File | Details |
|--------|------|---------|
| Create | `.github/workflows/build.yml` | Runner Windows 2025, SDK .NET 10, matriz Debug/Release, restore, build, testes através de `dotnet run`, publicação self-contained win-x64 e upload dos artefactos. |
| Modify | `README.md` | Documentar os eventos do workflow, execução manual e utilização dos artefactos de cada configuração. |

- [x] Workflow válido, sem segredos e com permissão de leitura do repositório.
- [x] As duas configurações executam os mesmos passos, sem cancelar a outra em caso de falha.

### Phase 2: Validação

#### Step 2.1: Executar os comandos da pipeline localmente

| Action | File | Details |
|--------|------|---------|
| Run | `VCC.Office.sln` | Restaurar e compilar em Debug e Release, mantendo warnings como erros. |
| Run | `tests/Vcc.Office.CoreShell.Tests/Vcc.Office.CoreShell.Tests.csproj` | Executar o programa de testes existente nas duas configurações com `--no-build --no-restore`. |
| Run | `src/Vcc.Office.Shell/Vcc.Office.Shell.csproj` | Publicar as duas configurações como self-contained win-x64 e confirmar executável, runtime e dependência nativa SQLite. |
| Run | `.github/workflows/build.yml` | Validar o YAML e rever os eventos, matriz e caminhos de artefactos. |
| Run | `.maestru/` | Executar `maestru check` depois de atualizar o estado e a evidência. |

- [x] Builds e testes aprovados em Debug e Release: zero avisos/erros e 43 testes aprovados por configuração.
- [x] As duas publicações contêm a aplicação e as dependências necessárias: 251 ficheiros por pacote, runtime self-contained, WPF, SQLite e símbolos verificados.
- [x] YAML válido, `git diff --check` e `maestru check` aprovados.

## Impacted Files

| File | Action | Purpose |
|------|--------|---------|
| `.github/workflows/build.yml` | Create | Compilar, validar e publicar Debug e Release no GitHub Actions. |
| `README.md` | Modify | Explicar a execução e os downloads do pipeline. |
| `.maestru/tracks/vcc-office/vcc-0096.md` | Modify | Rastrear o trabalho e a evidência de validação. |
| `.maestru/specs/vcc-office/vcc-0096-debug-release-build-spec.md` | Create | Registar o plano e os resultados da implementação. |
