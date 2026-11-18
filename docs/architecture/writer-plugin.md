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


## Host genérico (desacoplagem concluída)

O Launcher descobre os plugins instalados, regista o nome que cada DLL expõe e abre as janelas por `IOfficePlugin.Id`. O dicionário de janelas evita abrir duas instâncias do mesmo plugin e substitui a antiga variável específica `_writerWindow`. O botão «Novo documento» escolhe um módulo que declare `SupportsNewDocument`, sem conhecer o ID Writer.

A recuperação de sessões é construída a partir das aplicações efetivamente disponíveis, sem registo fixo do Writer. O catálogo base contém apenas as outras aplicações antigas; o Writer aparece exclusivamente quando `Plugins/writer/plugin.json` e a respetiva DLL estão disponíveis.

`Vcc.Office.Desktop.csproj` **não referencia** `Vcc.Office.Writer.csproj`. A compilação da solução constrói os projetos de forma independente; o passo de empacotamento `python scripts/stage-office-plugins.py <publish-directory> Release` percorre os manifestos em `src/*/plugin.json` e copia as DLLs e manifestos para `Plugins/<id>/` sem referências ao Writer no Host.

Para executar localmente após um build da solução, colocar os plugins na pasta de saída do executável com o mesmo script, por exemplo:
`python scripts/stage-office-plugins.py src/Vcc.Office.Desktop/bin/Debug/net10.0 Debug`.
