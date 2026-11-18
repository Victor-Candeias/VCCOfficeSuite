# VCC-0830 — Sistema universal de abertura de documentos

A aplicação base utiliza o contrato opcional `IOfficeDocumentPlugin` (sem dependências dos editores). Cada plugin anuncia apenas os formatos que consegue importar e implementa `OpenDocumentAsync` sobre uma janela criada pelo próprio plugin.

Fluxo: selecionar ficheiro ou clicar num recente → procurar capacidades dos plugins ativos → mostrar escolha quando existem vários candidatos → importar numa nova janela → mostrar editor → registar nos recentes somente após importação bem-sucedida. Nunca invocar `Process.Start` ou aplicações do sistema operativo. O documento original não é substituído pelo Launcher; novas janelas evitam descartar alterações existentes.

Formatos confirmados atualmente: Writer TXT e DOCX (DOCX é importação apenas de texto, sem round-trip seguro); Sheets XLSX (primeira folha, limitações de fidelidade); Slides PPTX (validar fidelidade). O Data não anuncia CSV porque a sua importação atual exige uma tabela destino selecionada; as extensões legadas DOC, XLS, PPT, CSV, ODT, ODS e ficheiros de dados não são anunciadas até terem importação real. A relação estática de formatos usada para metadados de ficheiros recentes não é a fonte de verdade do encaminhamento.

Cada plugin continua isolado numa DLL; o Launcher conhece apenas os contratos. A rota ignora plugins desativados, recusa extensões não implementadas e fornece uma caixa de seleção quando múltiplos plugins anunciam o mesmo formato. Smoke tests cobrem o catálogo, formatos não suportados e abertura real de TXT através da DLL descoberta.
