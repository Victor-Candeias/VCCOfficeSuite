# VCC Office — Especificação Técnica Completa (Suite de 26 aplicações)

**Versão:** 2.0 — revisão alargada  
**Data:** 25-10-2026  
**Plataforma inicial:** Windows 11 x64  
**Objetivo:** suite pessoal, modular, local-first e offline-first, sem serviços obrigatórios para operações locais  
**Estratégia:** WPF/.NET 10 + WebView2/React/TypeScript + bibliotecas open-source e adaptadores sujeitos a validação de licença  
**Estado:** especificação e plano de execução; não representa implementação já concluída

> Esta versão mantém os requisitos da especificação v1 e acrescenta 20 aplicações, perfazendo 26 módulos de produto. O roadmap deve priorizar valor e reutilização de infraestrutura; a compatibilidade total com o Microsoft Office não é prometida.

## Índice executivo

1. Visão e arquitetura de referência
2. Catálogo de 26 aplicações e critérios individuais
3. Componentes partilhados e integração entre módulos
4. Modelo de dados lógico e persistência
5. Formatos, compatibilidade e bibliotecas
6. Segurança, offline, desempenho e licenciamento
7. MVP, fases, sprints e roadmap
8. Testes, métricas e critérios de conclusão
9. Estrutura do repositório e contratos técnicos
10. Apêndice: especificação original v1 integral

---

## 1. Visão de produto e arquitetura

A suite é composta por **26 aplicações** (6 originais + 20 adicionais), apresentadas num Launcher único, mas carregadas como módulos independentes. Instalação modular é uma meta posterior: no MVP, o instalador pode entregar o núcleo e os primeiros módulos; a ativação e distribuição de pacotes independentes chega após estabilizar o sistema de extensões.

### 1.1 Camadas

```text
VCC Office Launcher (WPF/.NET 10)
  |-- Shell nativo: janelas, ficheiros, impressão, notificações, atualizações
  |-- WebView2 + React: UI, Ribbon, temas, acessibilidade, comandos
  |-- Aplicações: Writer / Sheets / Slides / Notes / Data / Mail / ...
  |-- Core Services
  |    |-- DocumentService, FileTypeRegistry, ImportExportPipeline
  |    |-- WorkspaceService, SearchService, TemplateService
  |    |-- SettingsService, RecoveryService, BackupService
  |    |-- JobScheduler, Permissions, Secrets, Logging, EventBus
  |-- Persistence: SQLite + FTS5 + ficheiros de projeto/assets
  |-- Adaptadores: OOXML, PDF, OCR, scanner, mail, CalDAV/CardDAV
  `-- Plugins: manifestos, permissões, APIs versionadas, isolamento
```

### 1.2 Regras arquiteturais

- **Um núcleo, vários módulos:** aplicações não referenciam diretamente classes internas de outras aplicações; comunicam por contratos e eventos versionados.
- **UI desacoplada:** contratos DTO tipados entre React e WPF via WebView2; validar origem, payload e autorização de cada comando.
- **Dados do utilizador sob controlo local:** documentos no sistema de ficheiros, SQLite para índices e metadados; sem login obrigatório.
- **Processamento em background:** conversões, OCR, indexação, backups e automações usam filas persistentes com cancelamento e limites de recursos.
- **Recuperação:** escrita atómica (temporário + fsync quando aplicável + rename), snapshots e deteção de conflitos.
- **Bibliotecas substituíveis:** interfaces para renderizadores, motores de cálculo, OCR, PDF e formatos.
- **Offline por defeito:** Mail, CalDAV/CardDAV, atualizações e integrações remotas dependem da rede, mas a cache e os módulos locais não.
- **Licenças e direitos:** não incorporar automaticamente componentes GPL/AGPL ou comerciais sem confirmar compatibilidade de redistribuição, distribuição de fontes e obrigações.

### 1.3 Áreas de trabalho

O **VCC Workspace** é uma funcionalidade do Core, não a 27.ª aplicação. Agrupa ficheiros, links, tarefas, notas e projetos sem obrigar a mover os originais. O **VCC Launcher** é igualmente infraestrutura comum, não contado como aplicação.

---

## 2. Catálogo completo de aplicações

| # | Aplicação | Âmbito | Formatos principais | Primeiro incremento verificável |
|---:|---|---|---|---|
| 1 | **VCC Writer** | Documentos ricos | DOCX, ODT, HTML, PDF | DOCX básico com round-trip, autosave e exportação PDF |
| 2 | **VCC Sheets** | Folhas de cálculo | XLSX, CSV, ODS, PDF | 100 funções essenciais, recálculo e XLSX sem perda silenciosa |
| 3 | **VCC Slides** | Apresentações | PPTX, PDF, PNG | Editar 10 slides, apresentar, exportar PPTX básico |
| 4 | **VCC Notes** | Notas estruturadas | Markdown, HTML, PDF | Notas offline e pesquisa FTS |
| 5 | **VCC Data** | Bases de dados pessoais | SQLite, CSV, JSON, SQL | Criar tabelas, consultar, importar CSV e backup |
| 6 | **VCC Mail** | Correio eletrónico | EML, MBOX (importação faseada), ICS | Enviar/receber em conta de teste; credenciais protegidas |
| 7 | **VCC PDF** | Ferramentas PDF | PDF, PDF/A quando validado, PNG | Visualizar, anotar, juntar/dividir e guardar sem corromper |
| 8 | **VCC Draw** | Diagramas e vetores | SVG, PNG, PDF, formato próprio | Criar diagrama, ligar nós, exportar SVG/PDF |
| 9 | **VCC Projects** | Gestão de projetos | CSV, JSON, PDF, importações condicionais | Gantt com dependências e recálculo de datas |
| 10 | **VCC Publisher** | Publicações e paginação | PDF, SVG, PNG, formato próprio | Folheto multipágina e PDF com margens corretas |
| 11 | **VCC Planner** | Tarefas e Kanban | CSV, JSON, ICS opcional | Kanban offline com drag/drop, filtros e persistência |
| 12 | **VCC Images** | Edição de imagem | PNG, JPEG, WebP, TIFF, SVG parcial | Edição não destrutiva básica e exportação |
| 13 | **VCC Analytics** | Dashboards e BI | CSV, XLSX, SQLite, JSON, PDF | Dashboard a partir de CSV e exportação PDF |
| 14 | **VCC Forms** | Formulários e questionários | JSON, CSV, PDF | Criar formulário, validar, guardar e exportar respostas |
| 15 | **VCC Calendar** | Calendário e agenda | ICS, CSV | Criar eventos recorrentes e exportar/importar ICS |
| 16 | **VCC Contacts** | Contactos e grupos | VCF, CSV | Importar/exportar VCF e resolver duplicados |
| 17 | **VCC MindMap** | Mapas mentais | JSON, SVG, PNG, PDF | Mapa de 100 nós navegável e exportável |
| 18 | **VCC Whiteboard** | Quadro branco infinito | JSON, SVG, PNG, PDF | Desenho offline, undo/redo, exportação |
| 19 | **VCC Scan** | Digitalização e OCR | PDF, PNG, JPEG, TXT | Scanner de teste e PDF pesquisável com OCR |
| 20 | **VCC Convert** | Conversão em lote | DOCX, XLSX, PPTX, PDF, imagens, CSV | Converter lote com resumo de êxitos/falhas |
| 21 | **VCC Backup** | Cópias de segurança | ZIP, manifest JSON | Backup consistente e restauro validado |
| 22 | **VCC Automate** | Automatização local | JSON, YAML opcional | Fluxo pasta->converter->arquivar com permissões |
| 23 | **VCC Templates** | Modelos reutilizáveis | DOCX, XLSX, PPTX, JSON, ZIP | Criar documento a partir de modelo |
| 24 | **VCC Math** | Equações e matemática | LaTeX, MathML, SVG, PNG | Editar equação e inserir no Writer/Slides |
| 25 | **VCC Archive** | Arquivo documental | PDF, DOCX, XLSX, PPTX, EML, imagens | Indexar e localizar 10 mil ficheiros sem duplicar |
| 26 | **VCC Time** | Registo de tempo | CSV, JSON, PDF | Registar tempo e emitir relatório por projeto |

### 2.1 Requisitos funcionais, tecnologia candidata e aceitação por módulo

#### 2.1.1 VCC Writer

- **Missão:** Documentos ricos.
- **Funcionalidades:** Paginação, estilos, tabelas, imagens, revisão, impressão.
- **Tecnologias/bibliotecas candidatas:** Tiptap/ProseMirror; Open XML SDK; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** DOCX, ODT, HTML, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** DOCX básico com round-trip, autosave e exportação PDF.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.2 VCC Sheets

- **Missão:** Folhas de cálculo.
- **Funcionalidades:** Fórmulas, referências, múltiplas folhas, filtros, gráficos, tabelas dinâmicas.
- **Tecnologias/bibliotecas candidatas:** Motor de grelha/cálculo sob POC; Open XML SDK; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** XLSX, CSV, ODS, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** 100 funções essenciais, recálculo e XLSX sem perda silenciosa.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.3 VCC Slides

- **Missão:** Apresentações.
- **Funcionalidades:** Layouts, temas, elementos, notas, apresentação, transições.
- **Tecnologias/bibliotecas candidatas:** React + SVG/Canvas; Open XML SDK; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** PPTX, PDF, PNG; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Editar 10 slides, apresentar, exportar PPTX básico.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.4 VCC Notes

- **Missão:** Notas estruturadas.
- **Funcionalidades:** Cadernos, secções, páginas, anexos, etiquetas, pesquisa.
- **Tecnologias/bibliotecas candidatas:** Editor rico + SQLite/FTS5; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** Markdown, HTML, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Notas offline e pesquisa FTS.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.5 VCC Data

- **Missão:** Bases de dados pessoais.
- **Funcionalidades:** Tabelas, relações, queries, formulários, relatórios.
- **Tecnologias/bibliotecas candidatas:** SQLite, Dapper/EF Core conforme POC; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** SQLite, CSV, JSON, SQL; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Criar tabelas, consultar, importar CSV e backup.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.6 VCC Mail

- **Missão:** Correio eletrónico.
- **Funcionalidades:** IMAP/SMTP, contas, pastas, pesquisa, anexos, cache.
- **Tecnologias/bibliotecas candidatas:** MailKit/MimeKit; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** EML, MBOX (importação faseada), ICS; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Enviar/receber em conta de teste; credenciais protegidas.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.7 VCC PDF

- **Missão:** Ferramentas PDF.
- **Funcionalidades:** Ver, pesquisar, anotar, juntar, separar, rodar, formulários, redigir, assinar.
- **Tecnologias/bibliotecas candidatas:** PDFium (visualização); PDFsharp/QuestPDF ou alternativas validadas; OCR opcional; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** PDF, PDF/A quando validado, PNG; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Visualizar, anotar, juntar/dividir e guardar sem corromper.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.8 VCC Draw

- **Missão:** Diagramas e vetores.
- **Funcionalidades:** Fluxogramas, UML, BPMN subset, ERD, conectores, alinhamento.
- **Tecnologias/bibliotecas candidatas:** React Flow ou JointJS (licença a verificar), SVG; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** SVG, PNG, PDF, formato próprio; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Criar diagrama, ligar nós, exportar SVG/PDF.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.9 VCC Projects

- **Missão:** Gestão de projetos.
- **Funcionalidades:** WBS, dependências, calendário, caminho crítico, baseline, recursos.
- **Tecnologias/bibliotecas candidatas:** Motor próprio de scheduling + Gantt UI; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** CSV, JSON, PDF, importações condicionais; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Gantt com dependências e recálculo de datas.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.10 VCC Publisher

- **Missão:** Publicações e paginação.
- **Funcionalidades:** Páginas, margens, sangria, grelhas, caixas de texto, impressão.
- **Tecnologias/bibliotecas candidatas:** Canvas/SVG + motor de layout e tipografia; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** PDF, SVG, PNG, formato próprio; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Folheto multipágina e PDF com margens corretas.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.11 VCC Planner

- **Missão:** Tarefas e Kanban.
- **Funcionalidades:** Quadros, colunas, etiquetas, prioridades, checklists, recorrência.
- **Tecnologias/bibliotecas candidatas:** React DnD + SQLite; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** CSV, JSON, ICS opcional; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Kanban offline com drag/drop, filtros e persistência.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.12 VCC Images

- **Missão:** Edição de imagem.
- **Funcionalidades:** Cortar, rodar, redimensionar, ajustar cor, camadas faseadas.
- **Tecnologias/bibliotecas candidatas:** SkiaSharp/ImageSharp (licenças), OpenCV opcional; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** PNG, JPEG, WebP, TIFF, SVG parcial; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Edição não destrutiva básica e exportação.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.13 VCC Analytics

- **Missão:** Dashboards e BI.
- **Funcionalidades:** ETL local, medidas, filtros, dashboards, gráficos, refresh.
- **Tecnologias/bibliotecas candidatas:** DuckDB/SQLite + ECharts/Plotly (licenças); validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** CSV, XLSX, SQLite, JSON, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Dashboard a partir de CSV e exportação PDF.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.14 VCC Forms

- **Missão:** Formulários e questionários.
- **Funcionalidades:** Construtor, campos, validação, lógica condicional, respostas.
- **Tecnologias/bibliotecas candidatas:** JSON Schema + React Hook Form/Ajv; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** JSON, CSV, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Criar formulário, validar, guardar e exportar respostas.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.15 VCC Calendar

- **Missão:** Calendário e agenda.
- **Funcionalidades:** Vistas dia/semana/mês, recorrência, lembretes, fusos.
- **Tecnologias/bibliotecas candidatas:** SQLite + iCalendar; CalDAV opcional; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** ICS, CSV; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Criar eventos recorrentes e exportar/importar ICS.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.16 VCC Contacts

- **Missão:** Contactos e grupos.
- **Funcionalidades:** Pessoas, empresas, grupos, duplicados, pesquisa.
- **Tecnologias/bibliotecas candidatas:** SQLite + vCard; CardDAV opcional; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** VCF, CSV; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Importar/exportar VCF e resolver duplicados.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.17 VCC MindMap

- **Missão:** Mapas mentais.
- **Funcionalidades:** Nós, hierarquia, colapsar, estilos, ligações.
- **Tecnologias/bibliotecas candidatas:** SVG/Canvas + layout automático; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** JSON, SVG, PNG, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Mapa de 100 nós navegável e exportável.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.18 VCC Whiteboard

- **Missão:** Quadro branco infinito.
- **Funcionalidades:** Caneta, formas, sticky notes, zoom/pan, seleção.
- **Tecnologias/bibliotecas candidatas:** Canvas/WebGL avaliado; modelo vetorial; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** JSON, SVG, PNG, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Desenho offline, undo/redo, exportação.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.19 VCC Scan

- **Missão:** Digitalização e OCR.
- **Funcionalidades:** Aquisição, recorte, deskew, multipágina, OCR, pesquisa.
- **Tecnologias/bibliotecas candidatas:** WIA/TWAIN via adaptadores; Tesseract opcional; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** PDF, PNG, JPEG, TXT; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Scanner de teste e PDF pesquisável com OCR.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.20 VCC Convert

- **Missão:** Conversão em lote.
- **Funcionalidades:** Filas, perfis, conversão, logs, preview, relatórios.
- **Tecnologias/bibliotecas candidatas:** Pipelines internos e adaptadores isolados; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** DOCX, XLSX, PPTX, PDF, imagens, CSV; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Converter lote com resumo de êxitos/falhas.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.21 VCC Backup

- **Missão:** Cópias de segurança.
- **Funcionalidades:** Snapshots, retenção, verificação, restauro, agendamento.
- **Tecnologias/bibliotecas candidatas:** SQLite backup API, ZIP, hash SHA-256, DPAPI; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** ZIP, manifest JSON; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Backup consistente e restauro validado.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.22 VCC Automate

- **Missão:** Automatização local.
- **Funcionalidades:** Triggers, condições, ações, variáveis, execução auditada.
- **Tecnologias/bibliotecas candidatas:** Motor workflow JSON, sandbox de ações, scheduler; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** JSON, YAML opcional; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Fluxo pasta->converter->arquivar com permissões.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.23 VCC Templates

- **Missão:** Modelos reutilizáveis.
- **Funcionalidades:** Galeria, categorias, variáveis, pré-visualização, favoritos.
- **Tecnologias/bibliotecas candidatas:** Catálogo SQLite + bundles versionados; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** DOCX, XLSX, PPTX, JSON, ZIP; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Criar documento a partir de modelo.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.24 VCC Math

- **Missão:** Equações e matemática.
- **Funcionalidades:** Editor de equações, símbolos, inserção em documentos.
- **Tecnologias/bibliotecas candidatas:** MathLive/KaTeX/MathJax (avaliar), LaTeX; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** LaTeX, MathML, SVG, PNG; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Editar equação e inserir no Writer/Slides.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.25 VCC Archive

- **Missão:** Arquivo documental.
- **Funcionalidades:** Metadados, tags, OCR indexado, pesquisa, versões.
- **Tecnologias/bibliotecas candidatas:** SQLite FTS5, indexadores, hashes; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** PDF, DOCX, XLSX, PPTX, EML, imagens; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Indexar e localizar 10 mil ficheiros sem duplicar.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

#### 2.1.26 VCC Time

- **Missão:** Registo de tempo.
- **Funcionalidades:** Timers, registos manuais, projetos, categorias, relatórios.
- **Tecnologias/bibliotecas candidatas:** SQLite + timers do host; validar licença, manutenção e API com POC antes da adoção.
- **Interoperabilidade:** CSV, JSON, PDF; os formatos avançados podem exigir importação parcial ou exportação com aviso.
- **Critério de aceitação do primeiro incremento:** Registar tempo e emitir relatório por projeto.
- **Integração:** abrir a partir do Launcher, registar ficheiros e ações no Core, participar na pesquisa e backup conforme permissões; exportar dados estruturados para os módulos consumidores.
- **Testes obrigatórios:** abrir/guardar/fechar/reabrir, undo/redo quando aplicável, Unicode/PT-PT, ficheiros inválidos, grandes volumes, cancelamento e regressão visual quando aplicável.

---

## 3. Serviços transversais e integração

### 3.1 Serviços partilhados

| Serviço | Contrato / responsabilidade |
|---|---|
| `IFileTypeRegistry` | Associar extensões/MIME, handlers e capacidades de importação/exportação |
| `IDocumentSessionService` | Sessões, dirty state, lock lógico, autosave, undo checkpoints |
| `IWorkspaceService` | Projetos de trabalho, referências de ficheiros, etiquetas, links e permissões |
| `ISearchService` | FTS5, indexação incremental, extração de texto e metadados |
| `IConversionService` | Conversões assíncronas, perfis, validação de perda e relatórios |
| `IJobQueue` | Jobs duráveis, prioridades, retries, cancelamento e progresso |
| `ITemplateCatalog` | Modelos versionados, variáveis e pré-visualizações |
| `IBackupService` | Snapshots consistentes, retenção e testes de restauro |
| `IEventBus` | Eventos locais versionados, com isolamento e idempotência |
| `ISecretsService` | Windows Credential Manager/DPAPI para tokens e passwords |
| `IPermissionService` | Capabilities por plugin, por ação e por origem |
| `IPrintService` | Impressão e PDF por pipeline próprio de cada editor |

### 3.2 Fluxos de integração

1. **Forms → Data/Sheets/Analytics:** respostas em esquema JSON versionado; tabela destino criada com mapeamento explícito de tipos.
2. **Projects ↔ Planner ↔ Time ↔ Calendar:** tarefas e marcos com identificadores globais; não duplicar entidades sem ligação e estratégia de sincronização.
3. **Scan → PDF → Archive:** aquisição, OCR, PDF pesquisável, indexação e metadados.
4. **Draw/MindMap/Images/Math → Writer/Slides/Publisher:** inserir como SVG/PNG e, quando suportado, manter referência ao ficheiro editável original.
5. **Templates → Writer/Sheets/Slides/Publisher/Forms:** instanciação com placeholders validados e sem execução implícita de código.
6. **Convert/Automate/Backup:** processamento via fila com permissões explícitas; nunca executar scripts externos silenciosamente.
7. **Mail ↔ Contacts ↔ Calendar:** anexos, vCard e convites ICS; deduplicação e confirmação antes de alterações externas.
8. **Analytics ← Sheets/Data/Time/Projects:** importações em modo leitura, snapshots de origem e atualização controlada.

### 3.3 Envelope de eventos

```json
{
  "eventId": "uuid",
  "eventType": "vcc.document.saved.v1",
  "occurredAtUtc": "2026-10-25T00:00:00Z",
  "sourceModule": "writer",
  "workspaceId": "uuid-or-null",
  "entityId": "uuid",
  "schemaVersion": 1,
  "payload": {"fileUri": "file:///...", "revision": 3}
}
```

Eventos internos são locais; persistir apenas os necessários para jobs/recovery. O bus não é um mecanismo de autorização.

---

## 4. Modelo de dados e persistência

### 4.1 Princípios

- SQLite em `%LOCALAPPDATA%/VCCOffice/data/office.db` para metadados; perfis e ficheiros grandes separados.
- Ficheiros de utilizador permanecem nos locais escolhidos; o índice guarda URI, hash e datas, não cópias obrigatórias.
- Chaves `TEXT` UUID, `created_at_utc`, `updated_at_utc`, `row_version`, `schema_version` e soft-delete apenas quando justificado.
- Foreign keys ON, WAL quando apropriado, transações curtas, migrações idempotentes com backup prévio.
- Segredos fora da base de dados; anexos e imagens por content-addressed storage quando apropriado.
- Dados de aplicações isolados em tabelas próprias; referências cruzadas via identificadores estáveis e tabelas de ligação.

### 4.2 Entidades nucleares

| Entidade/tabela | Campos essenciais | Relações / índices |
|---|---|---|
| `app_modules` | id, name, version, enabled, manifest_json | UNIQUE(name) |
| `workspaces` | id, name, root_uri, settings_json | nome + owner local |
| `workspace_items` | id, workspace_id, target_type, target_id, sort_order | FK workspace; index target |
| `documents` | id, module_id, uri, mime, sha256, last_opened, revision | UNIQUE(uri); index last_opened |
| `document_versions` | id, document_id, snapshot_uri, sha256, created_at | FK document |
| `assets` | id, sha256, uri, mime, size_bytes, ref_count | UNIQUE(sha256) |
| `tags`, `entity_tags` | tag id/name, entity_type/id | índice entity |
| `templates` | id, module_id, name, version, bundle_uri, schema_json | module/name/version |
| `jobs` | id, kind, status, input_json, progress, attempts, error_json | index status/priority |
| `automation_runs` | id, workflow_id, job_id, started_at, outcome | FK workflow |
| `search_index` | FTS5 title, body, tags, source_id | FTS5 + source map |
| `audit_events` | id, action, actor, target, timestamp, result | index timestamp/target |
| `settings` | scope, key, value_json, schema_version | PK(scope,key) |

### 4.3 Entidades de negócio por módulo

| Módulo | Entidades principais | Regras de integridade |
|---|---|---|
| Writer | document, paragraph, style, comment, revision | hierarquia estruturada, IDs estáveis |
| Sheets | workbook, worksheet, cell, formula, named_range, chart | endereços únicos por folha |
| Slides | deck, slide, shape, theme, animation | z-order e IDs estáveis |
| Notes | notebook, section, page, note_attachment | cascata controlada |
| Data | database_connection, table_definition, query_definition, form_definition | SQL parametrizado |
| Mail | account, mailbox, message_cache, attachment_ref | UIDVALIDITY/UID por pasta |
| PDF | pdf_document, annotation, form_field, signature_metadata | coordenadas em espaço da página |
| Draw | diagram, node, edge, stencil | endpoints válidos |
| Projects | project, task, dependency, resource, assignment, baseline | impedir ciclos de dependência |
| Publisher | publication, page, frame, master_page | unidades físicas explícitas |
| Planner | board, column, card, checklist_item | ordenação determinística |
| Images | image_project, layer, edit_operation | histórico não destrutivo |
| Analytics | datasource, dataset, measure, dashboard, visual | refresh versionado |
| Forms | form, form_version, field, submission, answer | submissões imutáveis por versão |
| Calendar | calendar, event, recurrence_rule, reminder | UTC + timezone IANA/Windows mapeada |
| Contacts | contact, organization, group, contact_method | dedupe sem merge automático |
| MindMap | mindmap, topic, topic_link | sem ciclos na árvore principal |
| Whiteboard | board, stroke, object, viewport | ordenação e coordenadas estáveis |
| Scan | scan_job, scanned_page, ocr_result | rastreabilidade origem/página |
| Convert | conversion_profile, conversion_batch, conversion_item | estados e idempotência |
| Backup | backup_set, backup_item, restore_operation | checksums e manifesto |
| Automate | workflow, workflow_version, trigger, action, run | aprovação de ações privilegiadas |
| Templates | template, template_version, variable_definition | schema de variáveis validado |
| Math | equation, symbol_palette, export_asset | fonte LaTeX preservada |
| Archive | archive_entry, classification, retention_rule, extracted_text | hash para duplicados |
| Time | time_entry, timer_session, category, rate | fim >= início; não sobrepor sem aviso |

### 4.4 Exemplo de DDL de referência

```sql
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS workspaces (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, root_uri TEXT,
  settings_json TEXT NOT NULL DEFAULT '{}',
  created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS documents (
  id TEXT PRIMARY KEY, module_id TEXT NOT NULL, uri TEXT NOT NULL UNIQUE,
  mime TEXT, sha256 TEXT, revision INTEGER NOT NULL DEFAULT 0,
  last_opened_utc TEXT, created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS jobs (
  id TEXT PRIMARY KEY, kind TEXT NOT NULL, status TEXT NOT NULL,
  priority INTEGER NOT NULL DEFAULT 0, input_json TEXT NOT NULL,
  progress REAL NOT NULL DEFAULT 0, attempts INTEGER NOT NULL DEFAULT 0,
  error_json TEXT, created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL,
  CHECK(status IN ('queued','running','succeeded','failed','cancelled'))
);
CREATE INDEX IF NOT EXISTS ix_jobs_status_priority ON jobs(status,priority,created_at_utc);
```

**Nota:** o modelo detalhado de cada editor deve ficar no seu próprio ADR/schema, para evitar colocar milhões de células, strokes ou nós diretamente em tabelas genéricas.

---

## 5. Formatos e compatibilidade

| Formato | Estratégia | Limites iniciais |
|---|---|---|
| DOCX | OOXML com mapeamento para modelo Writer; corpus round-trip | campos avançados, macros, revisão complexa |
| XLSX | import/export OOXML + motor de fórmulas | VBA, Power Query, fórmulas dinâmicas, pivots complexas |
| PPTX | OOXML e mapa de objetos suportados | animações complexas, SmartArt, fontes ausentes |
| PDF | renderização e operações estruturais; exportação própria | edição arbitrária do conteúdo, assinaturas certificadas |
| ODT/ODS/ODP | adaptadores faseados | fidelidade parcial |
| ACCDB/MDB | importação por conector opcional, sem promessa de round-trip | dependência de drivers e diferenças de tipos |
| MPP | importação opcional após POC, sem garantia | formato proprietário e calendário complexo |
| SVG/PNG/JPEG/WebP | pipelines gráficos | fontes/filtros avançados |
| ICS/VCF/EML | parsers padronizados, testes de interoperabilidade | extensões específicas de fornecedores |
| CSV/JSON/Markdown | leitura/escrita com UTF-8 e opções regionais | tipos inferidos e fórmulas |

**Política de fidelidade:** guardar original antes de conversão; matriz de features por formato; relatório de incompatibilidades antes de sobrescrever; testes `open → save → reopen` e comparação semântica, visual e estrutural. Para ficheiros com macros, preservar original e não executar macros. Nunca afirmar 100% de compatibilidade.

### 5.1 Bibliotecas candidatas e decisão de adoção

| Área | Candidatos para POC | Verificação obrigatória |
|---|---|---|
| Host/UI | .NET 10, WPF, WebView2, React, Vite | runtime redistribuível e acessibilidade |
| Writer | Tiptap/ProseMirror, Open XML SDK | paginação, round-trip, licença extensões |
| Sheets | Univer e alternativas | licença da versão, motor de fórmulas, offline |
| PDF | PDFium, PDFsharp, QuestPDF, bibliotecas de assinatura | redistribuição, edição, PDF/A e assinaturas |
| Gráficos/BI | Apache ECharts, DuckDB | licenças e binários Windows |
| Imagens/OCR | SkiaSharp, ImageSharp, OpenCV, Tesseract | licença de cada pacote e modelos OCR |
| Diagramas | React Flow, alternativas SVG | limites de licença e exportação |
| Mail | MailKit/MimeKit | segurança TLS/OAuth e formatos |
| Matemática | KaTeX, MathJax, MathLive | fidelidade MathML/LaTeX e licença |
| Bases de dados | SQLite, FTS5, Dapper/EF Core | migrações, backup, versões |
| Instalador | WiX, Inno Setup, MSIX | assinatura, atualização, rollback |

Todas as dependências têm de ser fixadas por versão, registadas em `THIRD_PARTY_NOTICES.md`, analisadas com SBOM e verificadas antes da redistribuição. ONLYOFFICE pode ser estudado numa POC separada, **não** assumido como motor embutível offline.

---

## 6. Segurança, privacidade e desempenho

- Permissões de sistema de ficheiros por diálogo explícito; proteção contra path traversal e ZIP bombs.
- WebView2: mensagens com DTOs validados, sem execução arbitrária de JS/SQL, navegação externa controlada e CSP quando aplicável.
- Plugins e automações: capability manifest, confirmação para escrita em massa, rede, execução externa e acesso a pastas; isolamento em processo separado para componentes não confiáveis.
- Mail: TLS, OAuth quando suportado, credenciais em Windows Credential Manager/DPAPI; nunca em logs.
- PDF: assinaturas criptográficas exigem biblioteca e certificado adequados; imagem de assinatura não equivale a assinatura digital; redação deve remover efetivamente dados e ser testada.
- Backups: manifestos com hashes, cópia consistente de SQLite, teste periódico de restauro, encriptação opcional com gestão segura de chaves.
- Automação: impedir loops, limitar tempo/memória, logs auditáveis, dry-run e rollback possível para operações reversíveis.
- Telemetria externa desativada por defeito; diagnósticos locais com opção de exportação sanitizada.
- Alvos iniciais de desempenho **a validar por benchmark**: launcher utilizável em <= 3 s numa máquina de referência SSD; pesquisa em 10 mil documentos <= 2 s após indexação; operações longas não bloqueiam a UI; 100 mil linhas em Sheets com virtualização.

---

## 7. MVP, fases e roadmap

### 7.1 MVP realista (primeira versão utilizável)

**Incluído:** Core/Launcher, configurações PT-PT, abas, abertura/guarda, autosave/recovery, SQLite, logs, Writer básico, Sheets básico, Slides básico, PDF viewer e Templates básico. Os módulos PDF e Templates são incrementos pequenos e reutilizam infraestrutura.

**Excluído do MVP:** Mail completo, colaboração cloud, pivots avançadas, macros, assinatura digital qualificada, edição PDF arbitrária, importação MPP/ACCDB, sincronização bidirecional e plugins de terceiros.

### 7.2 Roadmap de entrega (estimativas relativas, não compromisso de calendário)

| Fase | Objetivo | Dependências | Saída demonstrável |
|---|---|---|---|
| 0 | Descoberta, licenças, ADRs, POCs | nenhuma | decisões técnicas e protótipos de formatos |
| 1 | Core + Launcher + persistência | 0 | app instalada, shell e módulos de teste |
| 2 | Writer MVP | 1 | DOCX básico + PDF |
| 3 | Sheets MVP | 1 | XLSX, fórmulas e CSV |
| 4 | Slides MVP | 1 | PPTX básico + apresentação |
| 5 | PDF + Templates + Convert inicial | 2–4 | visualização PDF, modelos e conversão em lote |
| 6 | Notes + Archive + Backup | 1,5 | pesquisa transversal, versões e restauro |
| 7 | Draw + MindMap + Whiteboard + Math | 1,4 | editores vetoriais e inserção nos documentos |
| 8 | Planner + Projects + Time + Calendar | 1,6 | tarefas, Gantt, timers e agenda integrados |
| 9 | Forms + Data + Analytics | 1,3,8 | formulários → tabelas → dashboards |
| 10 | Images + Scan + Publisher | 5,7 | imagens, OCR, paginação e impressão |
| 11 | Contacts + Mail | 1,8 | contactos, contas e convites |
| 12 | Automate + plugins | 5–11 | fluxos locais auditados |
| 13 | Hardening, acessibilidade, instalador modular | todas | suite estável e documentação final |

**Ordem recomendada:** priorizar uma release utilizável após fases 0–5 e acrescentar módulos em releases independentes. Não iniciar 26 implementações em paralelo. Para um único programador, este é um projeto plurianual; as durações dependem fortemente de reutilização e da fidelidade pretendida.

### 7.3 Plano inicial de 8 sprints (sprints de referência, duração a definir)

| Sprint | Entrega | Demonstração / teste de saída |
|---|---|---|
| 1 | ADRs, licenças, solução, CI | build limpo Windows e SBOM |
| 2 | WPF + WebView2 + React shell | navegação e Ribbon acessível |
| 3 | SQLite, settings, ficheiros, jobs | migração, reabertura, cancelamento |
| 4 | sessões, autosave, recovery, backup | crash simulado e recuperação |
| 5 | Writer modelo + editor | documento com estilos e tabelas |
| 6 | Writer DOCX/PDF + corpus | round-trip e relatório de perdas |
| 7 | Sheets POC + grelha virtual | 10 mil células e fórmulas |
| 8 | Sheets XLSX + Slides skeleton | abrir/guardar workbook e criar slide |

### 7.4 Definição de pronto por fase

Uma fase só é concluída com: funcionalidades aceites; testes automatizados verdes; import/export sem corrupção; documentação de limites; licença aprovada; instalação/upgrade; acessibilidade básica; logging sem dados sensíveis; benchmark e regressão de desempenho.

---

## 8. Estratégia de testes e critérios de conclusão

### 8.1 Pirâmide de testes

1. **Unitários:** modelos, regras, fórmulas, calendários, scheduling, validações, parsers e serialização.
2. **Contratos:** IPC UI/host, eventos, APIs de plugins, migrations e schemas.
3. **Integração:** SQLite, ficheiros, mail sandbox, scanner simulado, OCR e pipelines de conversão.
4. **E2E desktop:** abrir, editar, guardar, recuperar crash, imprimir e exportar, incluindo atalhos e teclado.
5. **Round-trip:** DOCX/XLSX/PPTX/PDF/ICS/VCF/CSV com fixtures versionadas e hashes de origem.
6. **Visual regression:** renderização Writer, Slides, Publisher, PDF e gráficos em várias escalas/DPI.
7. **Segurança:** fuzzing de ficheiros, zip bombs, path traversal, macros, XSS em WebView, credenciais, plugins e automações.
8. **Carga:** 100 mil linhas, 1.000 slides/objetos (cenários específicos), 10 mil ficheiros indexados, jobs concorrentes.
9. **Acessibilidade:** navegação por teclado, contraste, leitores de ecrã, foco, zoom 200%.
10. **Instalação:** Windows 11 limpo, sem internet após instalação, upgrade, rollback e desinstalação.

### 8.2 Gates mensuráveis (metas iniciais, ajustáveis após baseline)

| Gate | Critério |
|---|---|
| Build | CI Windows verde em main; dependências e versões bloqueadas |
| Qualidade | >= 80% de cobertura do Core crítico; módulos por risco, não só percentagem |
| Ficheiros | 0 corrupção de ficheiros originais no corpus de regressão; backups antes de conversões destrutivas |
| Compatibilidade | matriz por feature e formato; perdas conhecidas sinalizadas |
| Offline | abrir, editar, guardar e exportar ficheiros locais sem rede |
| Recovery | restauro de sessão após encerramento forçado em cenários testados |
| Segurança | 0 vulnerabilidades críticas/altas conhecidas sem mitigação aceite |
| Usabilidade | tarefas nucleares executáveis só com teclado e mensagens de erro úteis |
| Performance | UI responsiva durante OCR, indexação, conversão e backup |
| Release | instalador assinado quando disponível, notas, licenças, SBOM e procedimento de rollback |

### 8.3 Critérios finais da suite

- [ ] As **26 aplicações** têm pelo menos o primeiro incremento funcional e os critérios individuais da secção 2.1 aprovados.
- [ ] Shell e serviços partilhados são estáveis, sem dependências circulares entre módulos.
- [ ] Dados locais exportáveis, backup e restauro testados.
- [ ] Pesquisa global e Workspace funcionam com os formatos suportados.
- [ ] Formatos Office têm matriz de compatibilidade, testes e avisos de perdas.
- [ ] Aplicações que requerem rede degradam graciosamente em offline.
- [ ] Nenhum componente é distribuído sem verificação de licença e notices.
- [ ] Instalador, atualizações, desinstalação, logs, manual e documentação de desenvolvimento concluídos.

---

## 9. Estrutura do monorepo e contratos

```text
VCCOffice/
├── VCCOffice.slnx
├── src/
│   ├── Desktop/VCC.Office.Desktop/
│   ├── Core/
│   │   ├── VCC.Office.Abstractions/
│   │   ├── VCC.Office.Core/
│   │   ├── VCC.Office.Storage/
│   │   ├── VCC.Office.Workspaces/
│   │   ├── VCC.Office.Search/
│   │   ├── VCC.Office.Jobs/
│   │   ├── VCC.Office.Security/
│   │   ├── VCC.Office.Conversion/
│   │   └── VCC.Office.Plugins/
│   ├── Modules/
│   │   ├── Writer/   ├── Sheets/     ├── Slides/
│   │   ├── Notes/    ├── Data/       ├── Mail/
│   │   ├── PDF/      ├── Draw/       ├── Projects/
│   │   ├── Publisher/├── Planner/    ├── Images/
│   │   ├── Analytics/├── Forms/      ├── Calendar/
│   │   ├── Contacts/ ├── MindMap/    ├── Whiteboard/
│   │   ├── Scan/     ├── Convert/    ├── Backup/
│   │   ├── Automate/ ├── Templates/  ├── Math/
│   │   ├── Archive/  └── Time/
│   ├── Adapters/OOXML/ PDF/ OCR/ Mail/ Scanner/
│   └── Frontend/
│       ├── shell/
│       ├── shared-ui/
│       ├── contracts/
│       └── modules/
├── tests/Unit/ Integration/ Contracts/ E2E/ Fixtures/ Visual/
├── benchmarks/
├── installers/
├── docs/adr/ modules/ schemas/ compatibility/ operations/
├── scripts/
├── .github/workflows/
├── THIRD_PARTY_NOTICES.md
├── LICENSE
└── README.md
```

### 9.1 Manifesto de módulo (exemplo)

```json
{
  "id": "vcc.pdf",
  "name": "VCC PDF",
  "version": "0.1.0",
  "apiVersion": 1,
  "entryPoint": "modules/pdf/index",
  "fileTypes": ["application/pdf"],
  "capabilities": ["files.read", "files.write", "jobs.submit"],
  "commands": ["pdf.open", "pdf.annotate", "pdf.merge"]
}
```

### 9.2 Contrato IPC (exemplo)

```ts
export type HostRequest = {
  requestId: string;
  apiVersion: 1;
  moduleId: string;
  command: string;
  payload: unknown;
};
export type HostResponse = {
  requestId: string;
  ok: boolean;
  data?: unknown;
  error?: { code: string; message: string };
};
```

No host, aplicar validação de schema, autorização por capability, timeouts e logging sanitizado. Não aceitar comandos genéricos `executeShell`, `eval` ou SQL arbitrário de módulos.

### 9.3 Documentação e rastreabilidade

Cada módulo deve conter `README.md`, `FEATURE_MATRIX.md`, `DATA_MODEL.md`, `TEST_PLAN.md`, `LIMITATIONS.md` e ADRs relevantes. Cada requisito recebe ID (ex.: `PDF-FR-001`) ligado a issue, teste e release.

---

## 10. Riscos e decisões em aberto

| Risco | Mitigação |
|---|---|
| Fidelidade Office | corpus real, matriz de suporte, warnings e não sobrescrever silenciosamente |
| Licenças | inventário SBOM, revisão de cada biblioteca e alternativas |
| Complexidade de 26 módulos | releases incrementais, interfaces comuns, POCs e limites de âmbito |
| Dependências entre módulos | contratos versionados, eventos, testes de integração |
| Edição PDF/assinaturas | distinguir anotações, manipulação estrutural, redação e assinatura certificada |
| Performance | virtualização, lazy loading, workers, benchmarks e budgets |
| Segurança dos ficheiros | parsing defensivo, isolamento, backups e sandbox |
| Migrações de dados | versionamento, backup e testes de upgrade/rollback |
| Protocolos externos | mocks, cache offline e gestão de tokens |

### 10.1 Próximas ações técnicas

1. Aprovar esta especificação como baseline v2.
2. Criar ADRs de editor Writer, motor Sheets, renderizador PDF e estratégia de módulos.
3. Prototipar 3 ficheiros reais por formato com round-trip e relatório de perdas.
4. Criar solução, shell WPF/WebView2, React e CI Windows.
5. Definir contratos IPC, SQLite migrations, jobs e recuperação.
6. Abrir backlog de 26 módulos, ordenado pelo roadmap, com testes de aceitação.

---

# Apêndice A — Especificação original v1 (referência integral)

A especificação abaixo é mantida como referência histórica. Quando houver diferenças de âmbito, módulos ou prioridades, **prevalece a versão 2.0 acima**.

# VCC Office — Especificação Técnica Completa

**Versão:** 1.0  
**Data:** 24-10-2026  
**Produto:** VCC Office  
**Plataforma inicial:** Windows 11 x64  
**Distribuição:** Uso pessoal, offline-first, modular  
**Abordagem:** Híbrida (interface própria + bibliotecas open-source licenciadas adequadamente)

---

## 1. Visão e objetivos

VCC Office é uma suite desktop para produtividade que reúne editor de documentos (Writer), folhas de cálculo (Sheets), apresentações (Slides), notas (Notes), bases de dados (Data) e email/calendário (Mail). A suite deverá executar localmente, sem necessidade de serviços cloud para documentos, folhas de cálculo, apresentações, notas ou bases de dados. A utilização de correio eletrónico e sincronização de calendários requer rede. A meta é interoperabilidade elevada com formatos Office, não paridade total com Microsoft Office.

### 1.1 Objetivos funcionais

1. Instalar e executar a suite com um único instalador.
2. Disponibilizar um launcher e seis aplicações integradas, iniciáveis individualmente.
3. Criar, abrir, editar, guardar, imprimir e exportar documentos, folhas de cálculo e apresentações.
4. Preservar dados e formatação ao abrir e guardar ficheiros externos, com indicação de perdas potenciais.
5. Permitir trabalho offline, recuperação de sessão, versões locais e pesquisa.
6. Partilhar componentes de UI, persistência, preferências, segurança e telemetria local opt-in.
7. Suportar expansão por plugins com permissões e isolamento progressivo.

### 1.2 Fora do âmbito inicial

- Compatibilidade 100% com todos os ficheiros Office, macros VBA ou add-ins COM.
- Colaboração simultânea em tempo real e armazenamento cloud obrigatório.
- Substituição integral de Outlook/Exchange, incluindo todas as funcionalidades empresariais.
- Execução de macros de terceiros sem isolamento.
- Compatibilidade binária com bases Access `.accdb` ou `.mdb`.

### 1.3 Princípios

**Local-first**, formatos abertos quando apropriado, interoperabilidade, segurança por defeito, componentes substituíveis, UX consistente, testes com documentos reais, migrações reversíveis e ausência de dependências ocultas de servidores.

---

## 2. Estratégia híbrida e decisões arquiteturais

### 2.1 Stack principal

| Camada | Tecnologia | Responsabilidade |
|---|---|---|
| Host desktop | .NET 10 + WPF | Janelas, menus nativos, processos, ficheiros, impressão e integração Windows |
| Web UI | WebView2 + React + TypeScript + Vite | Interface, editores, Ribbon, painéis e temas |
| IPC | WebView2 `postMessage` com contratos tipados | Comunicação segura UI ↔ host |
| Domínio | Bibliotecas C# por aplicação | Casos de uso, validações, orquestração |
| Persistência interna | SQLite | Configurações, índices, versões, catálogos, notas e metadados |
| DOCX/PPTX/XLSX | Open XML SDK e adaptadores | Leitura/escrita dos pacotes OOXML onde adequado |
| Documentos | Tiptap/ProseMirror + motor de paginação a validar | Edição estruturada, operações, layout |
| Folhas de cálculo | Motor existente avaliável (p.ex. Univer) ou motor isolado | Grelha, cálculo, edição, seleção, fórmulas |
| Slides | React + SVG/Canvas + modelo próprio | Editor visual e apresentação |
| Email | MailKit/MimeKit | IMAP, SMTP, MIME e anexos |
| Exportação PDF | Pipeline dedicado, por aplicação | Fidelidade de layout e impressão |
| Testes | xUnit, Vitest, Playwright, testes de integração | Qualidade funcional e regressões |
| Distribuição | MSIX ou WiX/Inno Setup após avaliação | Instalação, atualização e desinstalação |

**Decisão crítica:** não assumir que ONLYOFFICE Docs é um motor local embutível sem serviços. A integração com ONLYOFFICE requer avaliação separada de arquitetura, licença, redistribuição, execução offline e custos. Para o MVP, a aplicação não dependerá dele. O mesmo vale para licenças, edições comerciais e funcionalidades de bibliotecas de folhas de cálculo: verificar as condições atuais antes da adoção.

### 2.2 Processo de seleção de dependências

Para cada biblioteca, registar: licença e versão, redistribuição permitida, obrigações de atribuição/código-fonte, dependências nativas, modo offline, suporte a formatos, acessibilidade, manutenção, desempenho e testes de prova de conceito. Manter `docs/adr/` e `THIRD_PARTY_NOTICES.md`. Não misturar código sob licenças incompatíveis sem revisão.

### 2.3 Arquitetura lógica

```text
VCC Office Desktop (WPF)
  ├── Launcher / Shell / Window Manager
  ├── WebView2 Host + IPC Gateway
  ├── Identity local / Settings / Theme / Recent Files
  ├── File Service / Autosave / Recovery / Versions
  ├── Search / Print / Export / Plugin Host
  └── Application Modules
       ├── Writer: document model + editor + DOCX adapter
       ├── Sheets: workbook model + calculation + XLSX adapter
       ├── Slides: slide model + canvas + PPTX adapter
       ├── Notes: notebook model + SQLite
       ├── Data: relational workspace + SQLite
       └── Mail: accounts + IMAP/SMTP + local cache

Frontend React
  ├── Shared Design System / Ribbon / Commands / Dialogs
  ├── Writer UI / Sheets UI / Slides UI
  └── Notes UI / Data UI / Mail UI
```

### 2.4 Limites entre módulos

Cada módulo disponibiliza contratos `Open`, `Create`, `Save`, `SaveAs`, `Export`, `Print`, `CanClose`, `GetCapabilities` e `GetDocumentState`. Não aceder diretamente às tabelas internas de outros módulos. Comunicação via interfaces versionadas e eventos de domínio. A aplicação não deverá depender de um singleton global de estado de documentos.

### 2.5 Segurança do WebView2 e IPC

- Carregar apenas conteúdos locais confiáveis; bloquear navegação não autorizada.
- Definir origem permitida para mensagens, validar esquemas JSON, tamanho e permissões de comandos.
- Nunca expor acesso arbitrário ao filesystem à página React.
- Utilizar tokens de correlação, cancelamento e timeouts nas chamadas.
- Separar operações de leitura e escrita; exigir confirmação para operações destrutivas.
- Atualizar WebView2 Runtime com a política de instalação definida; testar ausência do runtime.
- Impedir execução de scripts não confiáveis a partir de documentos importados.

---

## 3. Modelo de experiência e interface

### 3.1 Shell comum

- Launcher com cartões Writer, Sheets, Slides, Notes, Data e Mail.
- Área de ficheiros recentes, favoritos, modelos e recuperação.
- Barra de título e Ribbon consistentes, comandos de acesso rápido e pesquisa de comandos.
- Separadores para vários ficheiros; janelas independentes opcionais.
- Tema claro/escuro/sistema, escala de interface e atalhos configuráveis.
- Estado de gravação, avisos de incompatibilidade e indicador de ficheiro alterado.
- Navegação por teclado, leitores de ecrã e localização PT-PT desde o início.

### 3.2 Convenções transversais

`Ctrl+N` novo, `Ctrl+O` abrir, `Ctrl+S` guardar, `Ctrl+Shift+S` guardar como, `Ctrl+P` imprimir, `Ctrl+Z/Y` desfazer/refazer, `Ctrl+F` localizar. Cada módulo pode acrescentar comandos específicos, documentando conflitos.

---

## 4. VCC Writer — editor de documentos

### 4.1 MVP

- Documento novo, abrir/guardar DOCX simples e formato interno estruturado.
- Texto com estilos, parágrafos, títulos, listas, links e tabelas simples.
- Imagens embutidas, alinhamento, margens, tamanho de página, cabeçalho/rodapé básicos.
- Localizar/substituir, desfazer/refazer, contador de palavras.
- Pré-visualização e exportação PDF com paginação controlada.
- Avisos para elementos não suportados na importação.

### 4.2 Fases avançadas

Secções, colunas, notas de rodapé, índices, referências cruzadas, comentários, controlo de alterações, modelos, mala direta, tabelas complexas, campos, equações, acessibilidade e revisão ortográfica offline. **A paginação fiel a Word é um risco técnico elevado**; implementar protótipos com documentos de referência antes de prometer equivalência.

### 4.3 Modelo lógico

`Document {id, title, sections[], styles[], metadata, revision}`; `Section {pageSettings, blocks[], headers, footers}`; `Block {type, attributes, children}`; `Inline {text, marks}`; `Asset {id, mime, checksum, blobRef}`. O modelo de edição é independente de DOCX. Conversores bidirecionais e relatório de perdas (`CompatibilityReport`).

---

## 5. VCC Sheets — folhas de cálculo

### 5.1 MVP

- Livro com múltiplas folhas, grelha virtualizada e edição de células.
- Valores numéricos, texto, datas, percentagens e moeda.
- Fórmulas essenciais: SUM, AVERAGE, MIN, MAX, COUNT, IF, AND, OR, referências absolutas/relativas.
- Intervalos, copiar/colar, preenchimento, ordenação e filtros básicos.
- Formatação de células, linhas e colunas; importação/exportação CSV e XLSX de âmbito definido.
- Gráficos simples e exportação PDF de intervalo/folha.

### 5.2 Motor de cálculo

Representar fórmulas como AST, construir grafo de dependências, recalcular incrementalmente, detetar ciclos e propagar erros (`#REF!`, `#DIV/0!`, `#VALUE!`). Separar cálculo, UI e serialização. Avaliar motor externo mediante prova de conceito de licença, compatibilidade e desempenho. Não usar `eval` para fórmulas.

### 5.3 Fases avançadas

Tabelas dinâmicas, validação de dados, formatação condicional, funções de pesquisa, funções de datas, gráficos avançados, nomes definidos, referências entre folhas, importação de dados, cenários, impressão avançada e suporte seletivo a funções dinâmicas. Macros VBA não serão executadas.

### 5.4 Modelo lógico

`Workbook {id, sheets[], definedNames[], styles[], metadata}`; `Worksheet {id, name, dimensions, cells, merges, filters, charts}`; `Cell {address, value, formula, styleId, type}`; `CalculationGraph {dependencies, dirtyNodes}`. Persistência esparsa para evitar armazenar células vazias.

---

## 6. VCC Slides — apresentações

### 6.1 MVP

- Criar/abrir/guardar apresentações; formatos internos e PPTX básico.
- Slides com layouts, caixas de texto, imagens, formas, alinhamento, camadas e agrupamento simples.
- Painel de miniaturas, notas do apresentador, temas e modelos.
- Modo apresentação em ecrã completo, exportação PDF e imagens.
- Desfazer/refazer e copiar/colar objetos.

### 6.2 Fases avançadas

Slide master, guias e grelhas, gráficos, tabelas, animações, transições, vídeo/áudio, apresentação em segundo monitor, temporizador, vista do apresentador, acessibilidade e compatibilidade PPTX progressiva.

### 6.3 Modelo lógico

`Presentation {id, theme, masters[], slides[], assets[], metadata}`; `Slide {id, layoutId, background, elements[], notes, transition}`; `Element {id, kind, x, y, width, height, rotation, zIndex, style, payload}`. Medidas internas normalizadas; conversão para unidades EMU no adaptador PPTX.

---

## 7. VCC Notes — notas pessoais

### MVP

Blocos de notas, secções, páginas ricas, listas de tarefas, imagens, anexos, etiquetas, pesquisa full-text, exportação Markdown/PDF e cópias de segurança locais.

### Futuro

Desenho livre, captura de ecrã, links bidirecionais, OCR local opcional, modelos, encriptação de blocos e sincronização opcional entre dispositivos.

### Modelo

`Notebook`, `Section`, `NotePage`, `NoteBlock`, `Tag`, `PageTag`, `Attachment`, `PageRevision`. Índice SQLite FTS5 com conteúdo reconstruível.

---

## 8. VCC Data — bases de dados pessoais

### MVP

Criar/abrir projetos SQLite, definir tabelas, colunas, chaves primárias/estrangeiras, índices, importar CSV, consultar dados com grelha, editor SQL e exportar CSV/JSON.

### Futuro

Designer de relações, formulários, consultas guardadas, relatórios imprimíveis, migrações visuais, validações, gráficos e conectores read-only. Compatibilidade Access limitada a importações explícitas e testadas; não prometer execução de formulários/macros Access.

### Modelo

`DataProject {id, databasePath, metadata}`; `SavedQuery`, `FormDefinition`, `ReportDefinition`, `ConnectionProfile`. O esquema de dados do utilizador reside na base SQLite do projeto, não na base interna da suite.

### Segurança

Consultas SQL são executadas apenas na base selecionada e com aviso para comandos destrutivos. Nunca permitir que uma ligação remota execute comandos do host.

---

## 9. VCC Mail — email, contactos e calendário

### MVP

Configurar contas IMAP/SMTP, autenticação OAuth2 quando suportada e passwords de aplicação quando apropriadas, sincronizar cabeçalhos e mensagens, enviar/responder/reencaminhar, anexos, pesquisa local, pastas e rascunhos. Guardar segredos com Windows DPAPI/Credential Manager, nunca em texto simples.

### Futuro

Contactos, calendário local, CalDAV/CardDAV com conectores testados, regras, etiquetas, assinaturas, tarefas, notificações, sincronização incremental, múltiplas identidades e tratamento robusto de conflitos.

### Limitações

Outlook/Exchange e Microsoft 365 podem exigir Microsoft Graph, permissões e políticas específicas; IMAP/SMTP não cobrem todas as funcionalidades. Offline significa acesso à cache e rascunhos, não envio/receção sem rede.

### Modelo

`MailAccount`, `MailFolder`, `MailMessage`, `MailAttachment`, `Draft`, `Contact`, `Calendar`, `CalendarEvent`, `SyncCursor`, `OutboxJob`. Corpos e anexos com armazenamento separado e política de retenção.

---

## 10. Serviços transversais

### 10.1 File Service

Abertura com deteção por assinatura e extensão, gravação atómica (ficheiro temporário + substituição), lock cooperativo, histórico de ficheiros recentes, `Save As`, backups, controlo de conflitos e política para ficheiros grandes.

### 10.2 Autosave e recuperação

Journal local de alterações com checkpoints; guardar versões de recuperação em pasta própria; restaurar após crash; limpar versões antigas por política de retenção. Autosave não substitui gravação explícita no formato original quando há risco de perda de compatibilidade.

### 10.3 Exportação e impressão

Contrato `IExportProvider` com formatos suportados e capacidades por módulo. Impressão deve suportar margens, orientação, escala, intervalos e pré-visualização. PDFs devem ser comparados visualmente com referências.

### 10.4 Pesquisa

Indexação local de títulos, metadados e texto extraído de formatos suportados; SQLite FTS5; exclusões configuráveis; índices reconstruíveis. Respeitar permissões do sistema de ficheiros.

### 10.5 Plugins

Manifesto `plugin.json` com id, versão, API mínima, capacidades e permissões. Na primeira versão, plugins desativados ou apenas internos; plugins de terceiros só após sandbox/processo isolado, assinatura e política de confiança.

### 10.6 Atualizações e privacidade

Sem telemetria remota por defeito. Atualizações manuais ou opt-in, verificadas por assinatura/hash. Logs locais com rotação e sem tokens, palavras-passe ou conteúdo de documentos.

---

## 11. Modelo de dados interno e persistência

### 11.1 SQLite (`office.db`)

| Tabela | Campos principais | Índices/regras |
|---|---|---|
| `app_settings` | key, value_json, updated_at | PK key |
| `recent_files` | id, path, module, opened_at, pinned, missing | índice opened_at |
| `workspaces` | id, name, created_at | PK id |
| `workspace_items` | workspace_id, file_id, position | UNIQUE workspace_id/file_id |
| `file_metadata` | id, path, checksum, format, size, modified_at | UNIQUE path normalizado |
| `recovery_sessions` | id, module, source_path, snapshot_path, status, created_at | índice status |
| `file_revisions` | id, file_id, revision_no, blob_path, checksum, created_at | UNIQUE file_id/revision_no |
| `templates` | id, module, title, file_path, version | índice module |
| `plugin_registry` | id, version, enabled, permissions_json | PK id |
| `jobs` | id, kind, payload_json, status, retries, updated_at | índice status |

As aplicações Notes e Mail terão bases de dados próprias para isolamento, migrações e manutenção. `VCC Data` mantém bases do utilizador separadas. Conteúdo de documentos Writer/Sheets/Slides deve residir principalmente nos ficheiros de trabalho; `office.db` contém apenas metadados e recuperação.

### 11.2 Esquema de armazenamento

```text
%LOCALAPPDATA%/VCCOffice/
  settings/
  data/office.db
  data/notes.db
  data/mail.db
  cache/
  search/
  recovery/
  revisions/
  logs/
  plugins/
```

Documentos do utilizador permanecem onde forem guardados, por exemplo em `%USERPROFILE%/Documents/VCC Office/`. Não guardar dados de aplicação dentro da pasta de instalação.

### 11.3 Evolução de esquemas

Cada base tem `schema_migrations(version, applied_at, checksum)`. Migrações idempotentes, transacionais quando possível, backup antes de migrações destrutivas e testes de upgrade/downgrade suportado. Definir política de compatibilidade de versões dos formatos internos.

---

## 12. Formatos e compatibilidade Microsoft Office

| Formato | MVP | Evolução | Limitações |
|---|---|---|---|
| `.docx` | Importar/exportar documentos simples | Estilos, secções, tabelas e comentários avançados | Layout/paginação e elementos complexos podem divergir |
| `.xlsx` | Valores, fórmulas comuns, estilos básicos | Gráficos, tabelas, validações, pivots | Funções avançadas e macros não garantidas |
| `.pptx` | Slides, texto, imagens, formas simples | Masters, transições e animações | Efeitos e tipografia podem divergir |
| `.doc`, `.xls`, `.ppt` | Não suportado diretamente | Conversão opcional isolada e licenciada | Formatos binários legados |
| `.docm`, `.xlsm`, `.pptm` | Leitura controlada apenas se testada | Preservação seletiva sem executar macros | Nunca executar VBA importado |
| `.pdf` | Exportar Writer/Sheets/Slides | Importação parcial para referência | PDF não é formato editável equivalente |
| `.csv`, `.tsv` | Importar/exportar Sheets e Data | Opções avançadas de delimitador/encoding | Não preservam fórmulas nem estilos |
| `.odt`, `.ods`, `.odp` | Fora do MVP | Adaptadores opcionais | Avaliar conformidade ODF |
| `.md`, `.txt` | Importação/exportação simples Writer/Notes | Conversão enriquecida | Formatação limitada |

### 12.1 Pipeline de importação/exportação

`Detect → Validate ZIP/package → Parse → Map to canonical model → CompatibilityReport → Edit → Serialize → Validate package → Round-trip test`.

**Regra:** nunca substituir silenciosamente o ficheiro original após importação com perdas relevantes; sugerir guardar cópia. Para OOXML, validar relações, namespaces, referências de media, content types, tamanhos e segurança contra ZIP bombs e entidades externas.

### 12.2 Matriz de testes de interoperabilidade

Criar corpus com ficheiros reais e sintéticos: simples, médios, complexos, fontes ausentes, imagens, tabelas, fórmulas, gráficos, notas, elementos desconhecidos e ficheiros corrompidos. Executar round-trip `Office → VCC → Office`, comparação semântica e comparação visual. Documentar cobertura por funcionalidade e versão, não apenas por extensão.

---

## 13. Estrutura de repositório

```text
VCCOffice/
├── VCCOffice.sln
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── src/
│   ├── Desktop/VCC.Office.Desktop/
│   ├── Core/
│   │   ├── VCC.Office.Abstractions/
│   │   ├── VCC.Office.Application/
│   │   ├── VCC.Office.Storage/
│   │   ├── VCC.Office.FileSystem/
│   │   ├── VCC.Office.Interoperability/
│   │   ├── VCC.Office.Export/
│   │   ├── VCC.Office.Search/
│   │   └── VCC.Office.Security/
│   ├── Modules/
│   │   ├── VCC.Office.Writer/
│   │   ├── VCC.Office.Sheets/
│   │   ├── VCC.Office.Slides/
│   │   ├── VCC.Office.Notes/
│   │   ├── VCC.Office.Data/
│   │   └── VCC.Office.Mail/
│   └── Frontend/
│       ├── apps/shell/
│       ├── modules/writer/
│       ├── modules/sheets/
│       ├── modules/slides/
│       ├── modules/notes/
│       ├── modules/data/
│       ├── modules/mail/
│       └── packages/ui/
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── e2e/
│   ├── interoperability/
│   ├── performance/
│   └── fixtures/
├── docs/
│   ├── adr/
│   ├── architecture/
│   ├── modules/
│   ├── formats/
│   └── release/
├── scripts/
├── packaging/
├── .github/workflows/
├── THIRD_PARTY_NOTICES.md
├── LICENSE
└── README.md
```

### 13.1 Convenções de código

C# nullable enabled, analyzers e warnings as errors no CI; TypeScript strict; ESLint/Prettier; APIs assíncronas com `CancellationToken`; contratos DTO versionados; testes de domínio sem dependência de UI; semantic versioning para pacotes internos.

---

## 14. MVP da suite

### 14.1 Objetivo do primeiro lançamento utilizável

**MVP 1:** Shell + Writer + Sheets + Slides básicos. Notes, Data e Mail surgem nas fases posteriores. Este recorte permite validar os três formatos Office principais sem dispersar a equipa.

### 14.2 Backlog priorizado

| ID | Prioridade | Entrega | Critério mínimo |
|---|---|---|---|
| CORE-01 | P0 | Shell WPF/WebView2 | Inicia offline e abre módulos |
| CORE-02 | P0 | IPC seguro | Comandos validados e testados |
| CORE-03 | P0 | File Service | Abrir, guardar e guardar como sem corrupção |
| CORE-04 | P0 | Recuperação | Recupera edição após fecho inesperado |
| CORE-05 | P0 | Temas/atalhos | Claro/escuro e atalhos principais |
| WR-01 | P0 | Writer básico | Edita texto, listas, tabelas simples e imagens |
| WR-02 | P0 | DOCX | Round-trip de corpus simples com relatório de perdas |
| SH-01 | P0 | Grelha e fórmulas | Cálculo e dependências corretos |
| SH-02 | P0 | XLSX/CSV | Importação/exportação de corpus simples |
| SL-01 | P0 | Editor Slides | Objetos básicos e modo apresentação |
| SL-02 | P0 | PPTX | Importação/exportação de corpus simples |
| EXP-01 | P0 | PDF | Exportação dos três módulos |
| QA-01 | P0 | Testes/CI | Pipeline verde e fixtures de compatibilidade |
| PKG-01 | P0 | Instalador | Instala/desinstala em Windows limpo |
| CORE-06 | P1 | Recentes/modelos | Launcher com histórico e modelos básicos |
| ACC-01 | P1 | Acessibilidade | Navegação teclado e labels acessíveis |

### 14.3 Definition of Done do MVP

- Nenhuma funcionalidade P0 incompleta ou com falha crítica conhecida.
- Instalação e arranque sem ligação à Internet após instalação de pré-requisitos.
- Abrir, editar, guardar e reabrir ficheiros simples DOCX, XLSX e PPTX de referência.
- Nenhuma perda silenciosa de conteúdo nos cenários suportados.
- Recuperação de sessão testada com crash induzido.
- Testes automatizados, instalador, documentação e inventário de licenças disponíveis.

---

## 15. Roadmap completo de desenvolvimento

As durações seguintes são **estimativas indicativas**, não compromissos. Para um único programador, funcionalidades Office de alta fidelidade podem exigir vários anos. Ajustar após provas de conceito.

### Fase 0 — Descoberta e provas de conceito (3–6 semanas)

- ADR da stack, matriz de bibliotecas e licenças.
- Protótipos: DOCX paginado, XLSX com fórmulas, PPTX com layout.
- Medir round-trip, memória, tamanho de ficheiro e execução offline.
- Decidir engines e estratégia de conversão com base em resultados.
- **Gate:** nenhuma dependência central sem licença e modo de execução validados.

### Fase 1 — Core e shell (4–8 semanas)

- Solução .NET, React, WPF/WebView2, IPC, logging, SQLite, temas, comandos, ficheiros, recuperação, instalador inicial.
- **Gate:** criar/guardar ficheiro de teste, recuperar após crash, pipeline CI verde.

### Fase 2 — Writer MVP (8–16 semanas)

- Editor estruturado, estilos básicos, tabelas/imagens, importação/exportação DOCX, PDF e impressão.
- **Gate:** corpus DOCX simples com resultados documentados.

### Fase 3 — Sheets MVP (10–20 semanas)

- Grelha virtualizada, cálculo, fórmulas, estilos, filtros, XLSX/CSV, gráficos básicos e PDF.
- **Gate:** cálculo determinístico, round-trip e desempenho com folhas de referência.

### Fase 4 — Slides MVP (8–16 semanas)

- Canvas, layouts, texto, formas, imagens, apresentação, PPTX e PDF.
- **Gate:** round-trip e modo apresentação estável.

### Fase 5 — MVP integrado e release alpha/beta (4–8 semanas)

- Integração dos três módulos, acessibilidade, estabilização, instalador e testes em máquina limpa.
- **Gate:** critérios de MVP cumpridos e bugs bloqueantes resolvidos.

### Fase 6 — Notes (4–8 semanas)

- Notebooks, páginas, etiquetas, anexos, pesquisa e exportação.
- **Gate:** pesquisa, backup/restauro e integridade testados.

### Fase 7 — Data (6–12 semanas)

- Gestão SQLite, designer de tabelas, grelha, SQL e importação/exportação.
- **Gate:** alterações de esquema seguras e dados preservados.

### Fase 8 — Mail (8–16 semanas)

- IMAP/SMTP, contas, pastas, mensagens, rascunhos, cache, credenciais seguras.
- **Gate:** testes com servidores de teste, offline/online e anexos.

### Fase 9 — Compatibilidade avançada (contínua, 6–18+ meses)

- Writer: secções, revisão e paginação; Sheets: funções/pivots/gráficos; Slides: masters/animações.
- **Gate:** cobertura de corpus crescente e regressões visuais controladas.

### Fase 10 — Integração, plugins e maturidade (contínua)

- Pesquisa global, interoperabilidade interna, modelos, APIs de plugins isolados, perfis de desempenho, documentação e atualizações seguras.
- **Gate:** estabilidade e política de compatibilidade/publicação estabelecidas.

### Marcos de lançamento

- `0.1` protótipo técnico (Fase 0–1)
- `0.5` alpha com Writer/Sheets/Slides básicos
- `0.9` beta integrada
- `1.0` suite principal estável (Writer/Sheets/Slides)
- `1.x` Notes, Data e Mail em entregas modulares
- `2.0` compatibilidade e funcionalidades avançadas, após maturação

---

## 16. Plano de testes e qualidade

### 16.1 Pirâmide de testes

1. **Unitários:** modelos, fórmulas, parser, comandos, migrações e validações.
2. **Integração:** SQLite, ficheiros, IPC, formatos OOXML, email com servidores de teste.
3. **E2E:** abrir, editar, guardar, fechar, reabrir, imprimir e recuperar.
4. **Interoperabilidade:** round-trip Office/VCC/Office e comparação semântica/visual.
5. **Performance:** livros grandes, documentos longos, decks pesados, memória e arranque.
6. **Segurança:** ficheiros malformados, ZIP bombs, anexos, links externos, injeção no IPC e SQL.
7. **Acessibilidade:** teclado, foco, contraste, leitores de ecrã e zoom.
8. **Instalação:** upgrade, downgrade permitido, desinstalação e dados preservados.

### 16.2 Metas iniciais propostas (sujeitas a medição)

| Indicador | Meta inicial |
|---|---|
| Arranque do shell em máquina de referência | p95 ≤ 5 s |
| Abrir DOCX simples de 10 páginas | p95 ≤ 3 s |
| Abrir XLSX simples de 10 mil células | p95 ≤ 5 s |
| Abrir PPTX simples de 20 slides | p95 ≤ 5 s |
| Testes unitários do domínio crítico | ≥ 80% cobertura de linhas, sem usar só cobertura como qualidade |
| Bugs P0/P1 conhecidos em release estável | 0 |
| Perda silenciosa de dados em corpus suportado | 0 casos |
| Dependência de Internet para edição local | 0 |

Ambiente de referência: Windows 11 x64, 16 GB RAM, SSD; medir em hardware real. Definir limites e percentis com fixtures versionadas.

### 16.3 CI/CD

Build .NET/Node, lint, testes unitários, análise de dependências e licenças, SCA, integração OOXML, testes E2E Windows, geração de artefactos, SBOM e assinatura do instalador. Releases com changelog, checksum e estratégia de rollback.

---

## 17. Segurança, privacidade e robustez

- Tratar ficheiros importados e emails como entradas não confiáveis.
- Limites de tamanho, profundidade, decompression ratio e tempo de processamento.
- Bloquear scripts/macros ativos, referências externas automáticas e execução de anexos.
- Guardar segredos no gestor de credenciais do Windows; mascarar logs.
- Gravação atómica, backups e recuperação testados; tratamento de disco cheio.
- Assinar binários e instaladores; verificar integridade de atualizações.
- Encriptação opcional de dados sensíveis em repouso e exportação segura de backups.
- Documentar threat model para WebView2, IPC, plugins, formatos e Mail.

---

## 18. Riscos e mitigação

| Risco | Impacto | Mitigação |
|---|---|---|
| Fidelidade DOCX complexa | Alto | POC precoce, corpus, aviso de perdas, adaptadores versionados |
| Fórmulas XLSX não suportadas | Alto | Matriz de funções, motor isolado, testes determinísticos |
| Layout PPTX e fontes | Alto | Renderização comparativa e fallback explícito |
| Licenças de motores externos | Alto | Revisão antes de integrar, alternativas, notices |
| Escopo excessivo | Alto | MVP restrito a três módulos e gates por fase |
| Corrupção de ficheiros | Crítico | Save atómico, backups, testes de falha e round-trip |
| Segurança de documentos/emails | Alto | Parsing defensivo, sandbox e bloqueio de conteúdo ativo |
| Desempenho WebView2 | Médio | Virtualização, lazy loading, profiling e budgets |
| Manutenção solo | Alto | Arquitetura modular, automação de testes e entregas pequenas |

---

## 19. Critérios de conclusão por aplicação

### Writer

- Criar, editar, guardar, reabrir e exportar corpus suportado.
- Layout e paginação consistentes dentro da matriz publicada.
- Sem perda silenciosa; impressão e recuperação verificadas.

### Sheets

- Cálculo determinístico, ciclos e erros corretos.
- XLSX/CSV suportados com testes round-trip.
- Grelha responsiva e gráficos/print conforme matriz.

### Slides

- Objetos editáveis e apresentação estável.
- PPTX suportado com relatório de incompatibilidades.
- Exportação PDF/imagem e testes de layout.

### Notes

- Notas persistentes, pesquisa e restauro de backup íntegros.

### Data

- CRUD e migrações seguros, SQL controlado e import/export verificados.

### Mail

- Envio/receção/sincronização testados, segredos protegidos e cache offline consistente.

### Suite

- Instalação e atualização testadas, UI uniforme, logs seguros, documentação de licenças, acessibilidade mínima, regressões automatizadas e ausência de falhas críticas.

---

## 20. Primeiro sprint recomendado

**Sprint 1 (2 semanas):** criar repositório e solução; WPF + WebView2 + React; contrato IPC com validação; launcher com cartões; tema claro/escuro; SQLite com migração inicial; abrir/guardar ficheiro de teste; testes básicos; build CI Windows. Entregável: executável local que abre o shell offline.

**Sprint 2 (2 semanas):** File Service, recentes, autosave/recovery, Ribbon comum, comandos e `Save As`; iniciar POC Writer DOCX.

**Sprint 3 (2 semanas):** comparar Tiptap/paginação, validar licença de motor Sheets, POC PPTX e definir matriz real de compatibilidade; produzir ADR de seleção.

### Histórias iniciais

- Como utilizador, quero iniciar a suite sem Internet.
- Como utilizador, quero criar e guardar um ficheiro sem risco de corrupção.
- Como utilizador, quero recuperar uma edição após falha inesperada.
- Como utilizador, quero saber quando um ficheiro Office contém funcionalidades não suportadas.
- Como programador, quero adicionar um módulo sem alterar o shell central.

---

## 21. Checklist de preparação para implementação

- [ ] Confirmar licença de cada biblioteca e versão selecionada.
- [ ] Fechar ADR do motor Writer e abordagem de paginação.
- [ ] Fechar ADR do motor Sheets e suporte a fórmulas.
- [ ] Definir modelo de Slides e pipeline PPTX.
- [ ] Criar corpus inicial de 30–50 ficheiros de referência por formato.
- [ ] Criar solução e estrutura do monorepo.
- [ ] Definir contratos de IPC e esquemas JSON.
- [ ] Implementar SQLite migrations, logging e recuperação.
- [ ] Configurar CI Windows e builds reproduzíveis.
- [ ] Definir instalador e política de updates.
- [ ] Registar métricas e critérios de aceitação do MVP.
- [ ] Criar guia de contribuição, licenças e ADRs.

---

## 22. Decisões ainda em aberto

1. Motor de folhas de cálculo: avaliar Univer e alternativas face à licença e capacidade offline; não fixar dependência antes de POC.
2. Writer: determinar motor de paginação e limite de fidelidade DOCX realista.
3. Slides: escolher motor de canvas (SVG/Canvas) e estratégia de edição de texto.
4. PDF: escolher pipeline de impressão/exportação que preserve layout em Windows offline.
5. Instalador: MSIX versus EXE tradicional conforme experiência desejada.
6. Atualizações: manual, opt-in ou canal local.
7. Localização: PT-PT prioritário, en-US opcional.
8. Formatos internos: pacote ZIP próprio versionado versus JSON + assets, com migração.
9. Calendar/contacts: protocolos e conectores após estabilizar Mail.

**Conclusão:** construir primeiro uma suite utilizável e estável com Core + Writer + Sheets + Slides, com compatibilidade Office mensurável e sem promessas de fidelidade universal. Acrescentar Notes, Data e Mail como módulos independentes, sem comprometer o funcionamento offline dos editores.
