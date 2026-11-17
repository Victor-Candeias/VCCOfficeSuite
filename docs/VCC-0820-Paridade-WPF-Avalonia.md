# VCC-0820 — Matriz de equivalência WPF → Avalonia (Fase 2)

## Escopo e ressalvas

A nova interface Avalonia partilha o CoreShell com WPF, mas **reutilizar serviços não equivale a testar integralmente a experiência de utilização**. O WPF só deve ser removido quando os fluxos necessários tiverem comportamento equivalente, testes e critérios de aceitação aprovados.

| Capacidade | WPF legado | Avalonia no fim desta fase | Equivalência |
| --- | --- | --- | --- |
| Launcher, dashboards, temas e pesquisa | Sim | Sim | Parcial: testes headless, falta aceitação visual/manual |
| Ficheiros recentes e favoritos | Sim | Sim | Parcial: validar comportamento real |
| VCC Data — tabelas, SQL, formulários, relatórios | Sim | Implementação Avalonia existente | Parcial: rever casos e funções avançadas |
| VCC Writer | Serviço Core; editor Avalonia em PR #522 | PR #522 ainda pendente nesta fase | Não demonstrada |
| VCC Sheets | Serviço Core com células/fórmulas, importação XLSX e exportações | **Novo** editor de células, fórmulas, importação XLSX e exportação CSV/PDF | Parcial: sem grelha avançada, edição por seleções, XLSX de saída nem fidelidade completa |
| VCC Slides | Serviço Core com decks/objetos/transições e PPTX | **Novo** editor de títulos, notas, adicionar diapositivos/texto, importar/exportar PPTX e HTML | Parcial: sem canvas editável, formas/imagens visuais ou modo apresentação completo |
| VCC Mail e outros módulos | Serviços Core e/ou cartões | Não migrados visualmente | Não demonstrada |
| Windows, Linux e macOS | WPF: Windows apenas | Avalonia compila nos três OS | Requer testes reais de arranque/UX |
| Instalação/atualização/rollback e preservação de dados | Windows WPF | Publicação Avalonia em evolução | Não demonstrada |

### Entrega técnica nesta fase

- `SheetsWindow`: células individuais, fórmulas, leitura do valor calculado, pré-visualização A1:H12, criação/persistência local, importar primeira folha XLSX e exportar CSV/PDF.
- `SlidesWindow`: apresentações e diapositivos, editar título/notas, adicionar objetos de texto, persistir estado, importar/exportar PPTX e exportar HTML.
- `VccSlidesService`: métodos `AddSlideAsync` e `SetSlideTitleAsync` para partilhar lógica entre plataformas.
- Acessibilidade inicial: botões rotulados; ainda é necessário validar teclado e leitores de ecrã.
- Smoke tests de construção das duas janelas e teste CoreShell de persistência Slides.

### Próximos bloqueios da remoção WPF

1. Concluir e integrar o Writer Avalonia e os seus testes.
2. Completar a experiência Sheets: grelha editável, seleção, copiar/colar, formatos, gráficos, compatibilidade XLSX bidirecional.
3. Completar Slides: editor visual de objetos, layouts, imagens, apresentação e maior fidelidade PPTX.
4. Inventariar e migrar as funções de Ribbon, configurações, acessibilidade, atalhos e qualquer módulo adicional ainda em WPF.
5. Criar testes funcionais equivalentes por módulo, além dos smoke tests, e executar validação manual em Windows/Linux/macOS.
6. Validar dados de utilizador e instalação/rollback antes da remoção de `src/Vcc.Office.Shell` e do CI WPF.

**Não remover WPF nesta fase.** O objetivo é reduzir lacunas de migração com entregas isoladas e verificáveis.
