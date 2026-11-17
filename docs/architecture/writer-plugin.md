# VCC Writer como plugin independente

## Estrutura

- `Vcc.Office.Plugin.Abstractions`: contrato `IOfficePlugin` (ID, nome e criação da janela).
- `Vcc.Office.PluginHost`: descoberta de manifestos e carregamento dinâmico de DLLs com `AssemblyLoadContext` e `AssemblyDependencyResolver`.
- `Vcc.Office.Writer`: aplicação Avalonia, `WriterPlugin` e **motor documental próprio** (`WriterService.cs`: modelos, edição, TXT/DOCX, exportação PDF, acessibilidade e persistência); utiliza apenas infraestrutura comum do CoreShell, como `AtomicFileStore` e `ValidationResult`.
- `Vcc.Office.Desktop`: launcher principal. Não compila contra `WriterWindow` nem tem referência à DLL Writer para invocar os seus tipos.

O motor `VccWriterService` foi removido de `Vcc.Office.CoreShell` para impedir que o serviço específico do Writer fique no núcleo do launcher.

O projeto do launcher referencia o Writer apenas como **dependência de build** (`ReferenceOutputAssembly=false`) para compilar e colocar a DLL na distribuição. Em execução, descobre módulos por manifesto.

## Distribuição

```text
VCC Office Suite.exe (Windows) / VCC Office Suite (Linux/macOS)
Vcc.Office.PluginHost.dll
Vcc.Office.Plugin.Abstractions.dll
Plugins/
  Writer/
    plugin.json
    Vcc.Office.Writer.dll
```

O manifesto contém `id`, `assembly`, `entryType` e `apiVersion`. O launcher usa `AppContext.BaseDirectory/Plugins`; se não existir Writer, o restante launcher continua a funcionar.

## Limitações e segurança

- Nesta fase só Writer é plugin: Sheets/Slides/Data permanecem no Host.
- Carregamento de DLLs **é execução de código no processo do launcher**, não é uma sandbox. Distribuir apenas plugins de confiança.
- O primeiro carregamento é por processo, sem atualização a quente, descarregamento nem isolamento de plugins maliciosos.
- O contrato é versionado via `apiVersion=1` para impedir manifestos incompatíveis.
- A folha visual atual do Writer não implementa ainda paginação A4 física ou DOCX com formatação avançada.

## Verificação

Compilar `VCC.Office.Avalonia.sln` em Release e executar os smoke tests. Em Windows/Linux/macOS, verificar que o pacote contém `Plugins/Writer/Vcc.Office.Writer.dll` e `Plugins/Writer/plugin.json`. O teste de integração verifica descoberta e abertura dinâmica a partir desse diretório, bem como funcionamento sem plugins.

## Identidade e metadados do plugin

Cada implementação de `IOfficePlugin` fornece o seu `Id`, `DisplayName`, `ToolTip`, `CreateMenuIcon(size)` e `CreateWindowIcon()`. O Launcher consome estes valores diretamente da DLL para compor o menu lateral, o dashboard clássico, as grelhas de aplicações e os tooltips. O nome deixa de ser determinado pelo Launcher para plugins instalados.

O VCC Writer inclui o seu próprio ícone azul com **W** branco compilado no projeto `Vcc.Office.Writer`, sem carregar o ícone do executável principal. O recurso de ícone de janela e os elementos vetoriais de menu pertencem ao plugin. Tal não muda necessariamente o ícone genérico que o Explorador do Windows utiliza para visualizar o próprio ficheiro `.dll`; o ícone da aplicação é usado na interface do launcher e na janela do Writer.

Sem o plugin, o Launcher continua a abrir e apresenta a entrada de catálogo como indisponível, preservando o funcionamento dos restantes módulos. Um plugin de terceiros continua a ser código de confiança executado no processo da suite.
