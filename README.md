# VCC Office Suite

A solução desktop oficial utiliza **Avalonia UI** e **.NET 10** para Windows, Linux e macOS.

## Compilar e testar

```bash
dotnet restore VCC.Office.sln
dotnet build VCC.Office.sln --configuration Release
dotnet run --project tests/Vcc.Office.CoreShell.Tests/Vcc.Office.CoreShell.Tests.csproj --configuration Release
dotnet run --project tests/Vcc.Office.Desktop.SmokeTests/Vcc.Office.Desktop.SmokeTests.csproj --configuration Release
dotnet run --project src/Vcc.Office.Desktop/Vcc.Office.Desktop.csproj
```

A solução `VCC.Office.Avalonia.sln` continua disponível como alias de compatibilidade; `VCC.Office.sln` é agora a solução principal. O projeto WPF `Vcc.Office.Shell` e o seu workflow foram retirados do branch principal; o histórico Git continua disponível.

## CI e artefactos

`.github/workflows/avalonia.yml` compila e testa Debug/Release em Windows, Linux e macOS, produzindo os artefactos `VCCOfficeDesktop-...`. O Windows 11 x64 standalone inclui o runtime .NET.

## Migração de dados

**Não apagar** os ficheiros do utilizador em `%LOCALAPPDATA%/VCC Office/`. As preferências de dashboards e as sessões antigas são importadas em leitura pela aplicação Avalonia, que utiliza estado próprio quando necessário. A eliminação do projeto WPF do Git **não** apaga instalações locais antigas nem dados de utilizador.

## Aceitação

A retirada do WPF foi autorizada explicitamente pelo proprietário em 17-11-2026. A aprovação é uma decisão de retirada, **não** uma declaração de que todos os testes manuais de paridade foram executados. O detalhe mantém-se em `docs/VCC-0820-10-retirement-readiness.json`.
