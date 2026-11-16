# VCC-0818 — Migração multiplataforma (incremental)

## Entregue neste incremento
- Novo projeto `src/Vcc.Office.Desktop` em .NET 10 e Avalonia 12, compilável de forma independente da aplicação WPF existente.
- Primeiro Launcher multiplataforma que consome os 26 módulos do `OfficeApplicationRegistry` no CoreShell e apresenta três modos de dashboard (moderno, clássico e grelha). Trata-se de estrutura funcional inicial, não de paridade visual com a interface WPF.
- Alternância de tema claro/escuro na sessão. Não representa ainda persistência de preferências.
- Verificação da disponibilidade dos editores: nenhum módulo Avalonia é dado como implementado nesta fase, mesmo quando existe uma versão WPF.
- Pipeline dedicado nas três plataformas Windows, Linux e macOS (Debug/Release, CoreShell tests e publish).
- A solução e pipeline WPF Windows permanecem inalterados, permitindo migração progressiva.

## Como executar
```sh
dotnet run --project src/Vcc.Office.Desktop/Vcc.Office.Desktop.csproj
```

## Próximos incrementos do VCC-0818
1. Migrar o splash screen, a identidade visual e o Chrome da janela, respeitando janelas nativas.
2. Migrar os layouts completos dos três dashboards e respetivos temas, depois de validação visual por plataforma.
3. Criar adaptadores multiplataforma de `StorageProvider`/seletores, abertura de documentos, navegação e persistência.
4. Migrar a interface do VCC Data e, só depois, assinalar o módulo como disponível em Avalonia.
5. Empacotar aplicações por runtime (Windows .exe/MSI, Linux DEB/AppImage, macOS .app/DMG) e configurar assinatura/notarização quando necessário.
6. Validar testes de interface em máquinas com Linux desktop e macOS, além de build automatizado.

## Segurança e honestidade de estado
A migração está em curso. O executável WPF não funciona em Linux/macOS. Build/publish não garante arranque da interface gráfica nem a distribuição instalável. Não foi migrada nenhuma interface de edição de documentos. O CoreShell continua partilhado.
