# VCC-0832 — Catálogo central de modelos

O Launcher apresenta modelos declarados pelos plugins instalados e ativos através do contrato opcional `IOfficeTemplatePlugin` e agrega metadados sem depender dos motores concretos. Cada modelo expõe ID, título, categoria, descrição e pré-visualização **textual**. Não são miniaturas reais do documento final.

A opção **Modelos** aparece no dashboard moderno, nos dashboards clássico/grelha e no menu lateral. A galeria permite pesquisar por texto, filtrar por aplicação ou categoria e criar um novo documento numa janela separada, sem substituir ficheiros existentes.

Modelos iniciais: Writer (carta, ata, relatório); Sheets (orçamento, tarefas, despesas); Slides (projeto, reunião, formação). O Data não anuncia modelos porque não suporta a criação de documentos via contrato. O conteúdo é produzido exclusivamente pela DLL correspondente, e o Launcher funciona também sem plugins.

Não inclui importação de modelos externos, armazenamento na cloud, nem pré-visualização gráfica fidedigna. Testes headless verificam a descoberta, IDs únicos, filtros, ausência de DLLs e criação real de um Writer a partir de um modelo.
