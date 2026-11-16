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

## Incremento seguinte — Splash e dashboards
- Splash Avalonia nativo com identidade visual e indicador de progresso, mostrado antes da construção da janela principal.
- Janela principal Avalonia com decorações nativas (evita APIs Win32), cabeçalho, barra lateral e pesquisa.
- Três dashboards com disposições diferenciadas: moderno (banner, ações, painéis), clássico (criação e catálogo) e grelha (26 módulos).
- Modo claro/escuro aplicado à interface Avalonia. Os editores e o seletor de ficheiros continuam por migrar.
- Os painéis de documentos recentes e favoritos apresentam estados de migração, não os dados SQLite; ainda não foi alcançada paridade com WPF.
- Validação de compilação Windows/Linux/macOS Debug/Release não é validação visual em ambiente gráfico.

## Incremento — Documentos recentes, favoritos e sessão do Launcher
- Integração do `RecentDocumentsService` SQLite existente na nova interface Avalonia, sem reimplementar as tabelas ou copiar documentos.
- Seletor nativo Avalonia (`StorageProvider`), com aceitação apenas de caminhos locais e de formatos já reconhecidos pelo CoreShell.
- Abertura com a aplicação predefinida do sistema operativo (`ProcessStartInfo.UseShellExecute`). Não representa edição dentro do VCC Office.
- Listas de recentes, favoritos, indicadores de ficheiro em falta, remoção de entradas, pesquisa por nome/caminho e ações nos três dashboards.
- Novo `DesktopLauncherStateStore` sobre as definições da base `office.db` existente, que preserva o último dashboard, o tema, o módulo escolhido e o último caminho de documento, sem marcar módulos não migrados como abertos.
- O Windows WPF mantém os seus formatos de preferências e o seu `OfficeSessionStore` independentes; a nova sessão é específica do Launcher Avalonia.
- Testes automatizados de recuperação, normalização, estado corrompido e coexistência com documentos/favoritos.

**Ainda por migrar:** reabertura de janelas reais de módulos Avalonia, editores, drag & drop, documentos remotos, instaladores e validação visual interativa nos três sistemas.

## Próximos incrementos do VCC-0818
1. Migrar o splash screen, a identidade visual e o Chrome da janela, respeitando janelas nativas.
2. Migrar os layouts completos dos três dashboards e respetivos temas, depois de validação visual por plataforma.
3. Criar adaptadores multiplataforma de `StorageProvider`/seletores, abertura de documentos, navegação e persistência.
4. Migrar a interface do VCC Data e, só depois, assinalar o módulo como disponível em Avalonia.
5. Empacotar aplicações por runtime (Windows .exe/MSI, Linux DEB/AppImage, macOS .app/DMG) e configurar assinatura/notarização quando necessário.
6. Validar testes de interface em máquinas com Linux desktop e macOS, além de build automatizado.

## Segurança e honestidade de estado
A migração está em curso. O executável WPF não funciona em Linux/macOS. Build/publish não garante arranque da interface gráfica nem a distribuição instalável. Não foi migrada nenhuma interface de edição de documentos. O CoreShell continua partilhado.
