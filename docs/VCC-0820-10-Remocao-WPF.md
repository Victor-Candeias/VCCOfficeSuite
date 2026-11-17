# VCC-0820 — Retirada efetiva do WPF

**Autorização:** o proprietário aprovou explicitamente a remoção de todos os projetos WPF em 2026-11-17. O código WPF permanece recuperável através do histórico Git, mas deixa de fazer parte do branch principal.

## Alterações efetuadas

- Removido `src/Vcc.Office.Shell/`, incluindo XAML, code-behind e ícone exclusivo legado.
- Eliminado o workflow `.github/workflows/build.yml` que publicava executáveis WPF.
- `VCC.Office.sln` substituída pela solução Avalonia, com CoreShell, Desktop e testes.
- O pipeline Avalonia utiliza agora a solução canónica `VCC.Office.sln` em Windows, Linux e macOS.
- `VCC.Office.Avalonia.sln` mantida como alias de compatibilidade para comandos existentes.
- Documentada a aceitação do proprietário e mantidos os 13 critérios com estado real, sem declarar que testes manuais não realizados passaram.

## Dados dos utilizadores

Não são apagados dados ou configurações do perfil. A aplicação Avalonia continua a reconhecer os formatos `shell-state.json`, `dashboards.sqlite` e `application-session.json`, com migração de preferências por leitura e um estado Avalonia próprio.

## Riscos aceites

O proprietário autorizou retirar o WPF mesmo com critérios manuais de paridade registados como não verificados. O manifesto assinala explicitamente este override. Testes CI validam compilação e smoke tests, mas não certificam acessibilidade real ou ergonomia em cada ambiente.

## Recuperação

Para recuperar código WPF histórico: Git permite consultar o commit anterior à retirada e fazer cherry-pick/restauro de ficheiros; a versão nova distribuída é exclusivamente Avalonia.
