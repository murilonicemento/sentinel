# Claude Code no Sentinel: hooks, agents e skills

Este documento descreve a automação do Claude Code versionada em `.claude/`. Ela aplica a
[constituição](../constitution.md) durante o desenvolvimento assistido: hooks fazem verificações determinísticas,
subagents fazem revisões especializadas e skills guiam procedimentos recorrentes.

```text
.claude/
├── settings.json            # registro dos hooks (protegido)
├── hooks/                   # scripts Python dos hooks (protegido)
│   ├── hooklib.py
│   ├── protect-files.py
│   ├── validate-secrets.py
│   ├── validate-architecture.py
│   └── validate-build.py
├── agents/                  # subagents
│   ├── architecture-reviewer.md
│   ├── code-reviewer.md
│   ├── security-auditor.md
│   └── test-engineer.md
├── skills/<nome>/SKILL.md   # skills
└── rules/                   # regras do projeto, carregadas automaticamente (protegido)
```

## Divisão de responsabilidades

| Tipo | Quando age | Para quê |
| --- | --- | --- |
| Hook | Automaticamente, em todo uso de ferramenta ou fim de turno | Regras verificáveis por script: arquivos protegidos, segredos, grafo de dependências, build e testes |
| Subagent | Quando o Claude delega ou a pessoa pede | Julgamento especializado que um script não faz: arquitetura, bugs, segurança, testes |
| Skill | Quando a pessoa invoca `/nome` ou o Claude reconhece a tarefa | Procedimento passo a passo, com os padrões reais do repositório |

Regra de integração: o que um hook já garante, os agents e as skills não repetem (por exemplo, nenhum agent roda
a suíte de testes ou verifica formatação, porque o `validate-build` faz isso). As skills chamam um subagent só no
ponto em que a revisão especializada agrega valor.

## Hooks

Registrados em `.claude/settings.json`. Todos leem o evento JSON no stdin e respondem com o JSON documentado pelo
Claude Code. Nenhum deles mostra valores sensíveis nas mensagens.

### protect-files (PreToolUse)

- **Ferramentas**: `Edit`, `Write`, `MultiEdit`, `NotebookEdit`, `Bash`.
- **Protege**: `CLAUDE.md`, `docs/constitution.md`, `docs/RFC-Arquitetura.md`, `global.json`,
  `.claude/settings.json`, `.claude/hooks/`, `.claude/rules/` e `.github/workflows/`.
- **Comportamento**: uma alteração em caminho protegido recebe `permissionDecision: "ask"`, ou seja, só acontece
  se a pessoa aprovar no prompt. No Bash, redirecionamentos (`>`, `>>`), `sed -i`, `tee`, `rm`, `mv`, destino de
  `cp`, e `git checkout/restore/reset/rm` sobre esses caminhos (incluindo `.`) também pedem confirmação. Leituras
  nunca são afetadas.
- **Alterar a lista**: edite `PROTECTED` em `protect-files.py`. O próprio arquivo é protegido, então a mudança
  exige aprovação.

### validate-secrets (PreToolUse)

- **Ferramentas**: `Edit`, `Write`, `MultiEdit` e comandos `Bash` que gravam arquivos.
- **Analisa só o conteúdo novo**: o arquivo inteiro num `Write`, ou as linhas do `new_string` que não estavam no
  `old_string` num `Edit`. Arquivos ignorados pelo git (ex.: `platform/.env`) são permitidos, porque são o lugar
  certo para segredos locais.
- **Detecta**: chaves privadas PEM, chaves AWS, tokens do GitHub e do Slack, chaves do SendGrid, Twilio, Google,
  Anthropic/OpenAI e Stripe, JWTs literais, senhas em connection strings e URIs, e valores literais em chaves
  sensíveis (`*Password`, `*SigningKey`, `*ApiKey`, `*Token`...) de arquivos de configuração (`.json`, `.yml`, `.env*`).
- **Não acusa**: placeholders (`${VAR}`, `<valor>`, `troque-por-...`, `TrocaEssaSenha123`, `your-...`, vazio),
  credenciais padrão de infraestrutura local (`postgres`, `minioadmin`, `guest`...), chaves de teste em `tests/`
  e exemplos em Markdown, exceto os formatos de alta confiança. Calibrado contra os 1.175 arquivos versionados,
  sem nenhum achado.
- **Resposta**: `deny` com a regra e a linha. Para um valor fictício que precisa ter formato real (ex.: fixture
  PEM), acrescente o marcador `sentinel:allow-secret` na mesma linha: o hook passa a pedir aprovação humana
  (`ask`) em vez de negar.

### validate-architecture (PostToolUse)

- **Ferramentas**: `Edit`, `Write`, `MultiEdit` e comandos `Bash` com `dotnet add|remove|new|sln` ou que citam
  `.csproj`/`.props` (nesse caso, todos os `.csproj` de `services/` são verificados).
- **Bloqueia** (`decision: "block"`, o Claude precisa corrigir):
  - referência de projeto que viola a direção Domain ← Application ← Infrastructure/Api (§4.5);
  - referência a projeto de outro microsserviço (§5.3);
  - qualquer pacote ou framework em Domain (§4.1);
  - pacotes de persistência, mensageria, cache, web ou provedores externos em Application (§4.2);
  - `Microsoft.NET.Sdk.Web` em Domain ou Application.
- **Pede revisão** (`additionalContext`):
  - Api referenciando Domain diretamente;
  - controller (`Controllers/` ou `Endpoints/`) importando namespace de Infrastructure (§4.4);
  - connection string em `appsettings*.json` apontando para banco de outro serviço (§11.1).
- **Por que não analisa C#**: a direção das dependências está no grafo do MSBuild, e o compilador recusa um
  `using` de camada não referenciada. Lido o `.csproj` como XML, o resto fica com o build do `validate-build`. O
  que nem o grafo nem o compilador decidem vira pedido de revisão, ou fica a cargo do `architecture-reviewer`.

### validate-build (Stop)

- **Escopo**: arquivos alterados no working tree (`git status`), cada um mapeado para o `.csproj` mais próximo em
  `services/`. Alterações em `Directory.Packages.props`, `Directory.Build.*` ou `global.json` compilam a
  `Sentinel.sln` inteira.
- **Executa, como no CI**: `dotnet build <projeto> -warnaserror`, `dotnet format whitespace <projeto>
  --verify-no-changes` e `dotnet test` nos projetos `*UnitTests` dos serviços afetados. Testes de integração
  (Testcontainers) só rodam com `SENTINEL_VALIDATE_BUILD_INTEGRATION=1`.
- **Falha**: `decision: "block"` com o resumo dos erros. O Claude continua e precisa corrigir ou explicar por que a
  falha não vem das suas alterações.
- **Sem ciclos infinitos**:
  - o mesmo conjunto de arquivos já validado com sucesso não é revalidado;
  - se nada mudou desde a última falha, o hook não roda de novo e só avisa a pessoa;
  - depois de 3 bloqueios consecutivos na sessão, o hook para de bloquear e avisa a pessoa;
  - o Claude Code também limita as continuações forçadas por Stop hooks.
- **Desligar**: `SENTINEL_VALIDATE_BUILD=0`.
- **Limitação**: o working tree inclui alterações feitas fora da sessão, então mudanças locais anteriores também
  entram no escopo.

### Configurar os hooks por variável de ambiente

Defina as variáveis no ambiente que inicia o Claude Code ou em `.claude/settings.local.json` (não versionado):

```json
{ "env": { "SENTINEL_VALIDATE_BUILD_INTEGRATION": "1" } }
```

## Subagents

| Agent | Responsabilidade | Ferramentas | Escreve? |
| --- | --- | --- | --- |
| `architecture-reviewer` | Clean Architecture, DDD, bounded contexts, acoplamento, propriedade de dados, conformidade com a constituição | Read, Grep, Glob, Bash | Não |
| `code-reviewer` | Bugs, lógica, assincronia, tratamento de erros, recursos, duplicação, legibilidade | Read, Grep, Glob, Bash | Não |
| `security-auditor` | Autenticação, autorização, isolamento de tenant, entrada, dados sensíveis, segredos, mensageria | Read, Grep, Glob, Bash | Não |
| `test-engineer` | Testes unitários e de integração, casos de borda, tenant, commands, queries e eventos (com a skill `test-generation` pré-carregada) | Read, Grep, Glob, Bash, Edit, Write | Só testes |

- Todos começam pela constituição e pelas regras de `.claude/rules/` pertinentes, apresentam achados com arquivo,
  linha, evidência e recomendação, classificados em Crítica, Alta, Média ou Baixa, e dizem o que não foi
  verificado.
- Os revisores não modificam arquivos, salvo pedido explícito.
- Nenhum agent tem a ferramenta `Agent`, então não disparam outros agents.

**Como usar**:

- Linguagem natural: "use o architecture-reviewer para revisar o serviço novo".
- Menção: `@"security-auditor (agent)" revise o controller de exportações`.
- Automático: o Claude delega quando a tarefa casa com a `description` do agent.

## Skills

Invoque com `/nome [argumentos]`. As skills com invocação automática também são carregadas quando o Claude
reconhece a tarefa.

| Skill | Uso | Argumentos | Integração |
| --- | --- | --- | --- |
| `create-microservice` | Novo serviço completo: projetos, solution única, DI, segurança, observabilidade, Dockerfile, compose, Prometheus | `<nome-kebab>` | Só manual (`disable-model-invocation`); termina com `architecture-reviewer` |
| `create-entity` | Entidades, agregados e value objects com invariantes e encapsulamento | `<serviço> <Conceito>` | Usa `test-generation` |
| `create-api-endpoint` | Endpoint v1 com DTOs, `ResponseBaseDTO<T>`, Problem Details, paginação, papéis e tenant | `<serviço> <MÉTODO> <recurso>` | `security-auditor` em dados por tenant |
| `cqrs-implementation` | Commands, queries, handlers, validação e DTOs; MediatR só onde o serviço já usa | `<serviço> <CasoDeUso>` | Usa `test-generation` |
| `implement-multitenancy` | Escopo de tenant da claim ao repositório, cache, índice, consumer e testes de acesso cruzado | `<serviço> [recurso]` | Termina com `security-auditor` |
| `create-kafka-consumer` | Consumer pelo ADR-0003: contrato, kit Reliability, DLQ, idempotência no Redis, trace, métricas | `<serviço> <tópico> <Evento>` | `architecture-reviewer` para contrato novo |
| `test-generation` | Procedimento de testes: nível, convenções, matriz de cenários, execução | `<serviço> <alvo>` | Pré-carregada no `test-engineer` |
| `code-review` | Revisão do diff: escopo, contratos, cópias sincronizadas, testes, consolidação por severidade | `[base \| caminho]` | Delega ao `code-reviewer` e, se o diff justificar, ao `architecture-reviewer` e ao `security-auditor` |

> A skill de projeto `code-review` substitui o comando `/code-review` embutido no Claude Code para este
> repositório. O alias embutido `/review` continua executando a revisão padrão.

## Dependências

- **Python 3.9+** no `PATH` como `python3` (somente biblioteca padrão).
- **git** e o **.NET SDK** definido em `global.json` (9.0.115), usados pelo `validate-build`.
- **Docker**, apenas para testes de integração (`SENTINEL_VALIDATE_BUILD_INTEGRATION=1`, ou execução manual).
- **Windows**: os hooks exigem `python3` disponível no shell usado pelo Claude Code (Git Bash ou WSL).

Nenhum pacote NuGet ou ferramenta nova foi adicionado ao repositório.

## Testar os hooks manualmente

Os hooks são scripts comuns que recebem o JSON do evento. Exemplo, a partir da raiz do repositório:

```bash
export CLAUDE_PROJECT_DIR=$PWD
echo '{"tool_name":"Bash","tool_input":{"command":"sed -i s/9/8/ global.json"},"cwd":"'$PWD'"}' \
  | python3 .claude/hooks/protect-files.py
# → {"hookSpecificOutput": {... "permissionDecision": "ask" ...}}
```

Use `claude --debug` para ver a execução dos hooks numa sessão real e `/hooks` para listar os hooks ativos.

## Adicionar componentes

- **Hook**:
  1. Crie o script em `.claude/hooks/<nome>.py`, reutilizando `hooklib.py`.
  2. Registre-o em `.claude/settings.json` com `matcher` e `timeout`.
  3. Teste com eventos sintéticos, incluindo o caso negativo e uma varredura do repositório para medir falsos
     positivos.
  4. Documente-o aqui.

  Use hook só para regra determinística; julgamento fica com um agent.
- **Agent**:
  1. Crie `.claude/agents/<nome>.md` com `name`, `description` (quando usar e o que não cobre), `tools` mínimas
     e o corpo com: o que ler antes, o que verificar e o formato de saída.
  2. Confira se a responsabilidade não se sobrepõe à de outro agent.
- **Skill**:
  1. Crie `.claude/skills/<nome>/SKILL.md` com `name`, `description` e `argument-hint`.
  2. Use `disable-model-invocation: true` em procedimentos com muitos efeitos colaterais.
  3. Aponte para a constituição, a spec e um arquivo de referência real em vez de copiar código.
  4. Mantenha o arquivo abaixo de 500 linhas.
- **Sempre**: valide o frontmatter, atualize este documento e mantenha tudo em português, com identificadores
  seguindo as convenções do .NET.

Referência oficial: [hooks](https://code.claude.com/docs/en/hooks),
[subagents](https://code.claude.com/docs/en/sub-agents), [skills](https://code.claude.com/docs/en/skills).
