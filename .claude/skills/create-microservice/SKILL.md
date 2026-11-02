---
name: create-microservice
description: Cria um novo microsserviço do Sentinel em services/<nome>/ com Domain, Application, Infrastructure, Api e projetos de teste, registrados na Sentinel.sln, com DI, autenticação, observabilidade, Dockerfile e entradas de plataforma. Use somente quando a pessoa pedir um serviço novo.
argument-hint: <nome-em-kebab-case>
disable-model-invocation: true
---

# Criar microsserviço: $ARGUMENTS

Procedimento obrigatório da constituição §6. Siga a ordem. Pare e pergunte se faltar a especificação ou se o
bounded context se sobrepuser a um serviço existente.

## 1. Entender antes de criar

1. Leia `docs/constitution.md` §2.4, §3, §4, §6 e §13.1.
2. Leia a especificação em `docs/services/` (crie-a com a pessoa antes, se não existir; a skill não inventa o
   escopo). Identifique: bounded context (RFC §4), dados próprios, eventos consumidos e publicados, APIs
   síncronas, dependências de outros serviços.
3. Confirme que nenhum serviço existente já tem essa responsabilidade (`ls services/`, specs em `docs/services/`).
4. Escolha os nomes: diretório kebab-case (`risk-catalog`), projetos PascalCase (`RiskCatalog.Domain`).
   Crie só as camadas com responsabilidade real (§3): um serviço sem persistência pode não precisar de algo em
   Infrastructure além de mensageria.

## 2. Projetos e solution (somente CLI)

Nunca rode `dotnet new sln` nem crie `.sln` novo. Use `dotnet new`, `dotnet sln Sentinel.sln add` e
`dotnet add reference`:

```bash
S=services/<nome>; P=<Pascal>
dotnet new classlib -n $P.Domain         -o $S/src/$P.Domain         -f net9.0
dotnet new classlib -n $P.Application    -o $S/src/$P.Application    -f net9.0
dotnet new classlib -n $P.Infrastructure -o $S/src/$P.Infrastructure -f net9.0
dotnet new webapi   -n $P.Api            -o $S/src/$P.Api            -f net9.0 --use-controllers
dotnet new xunit    -n $P.UnitTests        -o $S/tests/$P.UnitTests        -f net9.0
dotnet new xunit    -n $P.IntegrationTests -o $S/tests/$P.IntegrationTests -f net9.0

dotnet add $S/src/$P.Application    reference $S/src/$P.Domain
dotnet add $S/src/$P.Infrastructure reference $S/src/$P.Application $S/src/$P.Domain
dotnet add $S/src/$P.Api            reference $S/src/$P.Application $S/src/$P.Infrastructure   # Infrastructure só para DI (§4.4)
dotnet add $S/tests/$P.UnitTests        reference $S/src/$P.Domain $S/src/$P.Application
dotnet add $S/tests/$P.IntegrationTests reference $S/src/$P.Api

for p in $(find $S -name '*.csproj'); do dotnet sln Sentinel.sln add "$p"; done
```

Depois de gerar:
- Remova os `Class1.cs`/`UnitTest1.cs` e o `WeatherForecast` gerados.
- Tire o atributo `Version` dos `PackageReference` que os templates criarem: versões ficam só em
  `Directory.Packages.props`. Pacote que ainda não está lá exige justificativa (`.claude/rules/dependencies.md`).
- O hook `validate-architecture` verifica as referências a cada alteração de `.csproj`.

## 3. Estrutura interna

Copie a estrutura de um serviço de referência do mesmo porte (alert-orchestrator ou tenants-billing) e ajuste
nomes e namespaces. Não existe `libs/` compartilhada: as cópias abaixo são intencionais e devem ficar em sincronia
com as dos outros serviços.

| Camada | Itens |
| --- | --- |
| Api | `Program.cs`, `Controllers/` (rotas `/api/v1/<nome>`), `Contracts/` (`ResponseBaseDTO`, `ApiProblems`, `ApiExceptionHandler`, `Pagination`, `ProblemResult`), `Security/` (`JwtAuthenticationExtensions`, `JwtOptions`, `TenantAccessEvaluator`, `AuthorizationConstants`), `Observability/ObservabilityExtensions`, `appsettings.json` sem segredos |
| Application | casos de uso, DTOs, portas (interfaces); MediatR só se o serviço precisar de CQRS (skill `cqrs-implementation`) |
| Domain | agregados, value objects, enums, eventos de domínio, interfaces de repositório (skill `create-entity`) |
| Infrastructure | `ServiceCollectionExtensions.Add<Pascal>Infrastructure`, persistência, `Messaging/` (com `Reliability/` e `KafkaTracing` se consumir/publicar), `Security/ServiceTokenHandler` se chamar outros serviços |

Configurações obrigatórias (Kafka, URLs de outros serviços, connection strings) são lidas no registro e falham
na inicialização com o nome da chave; sem fallback `localhost` no código. Valores locais vão em
`appsettings.json`; segredos em User Secrets ou `platform/.env`.

## 4. Execução e plataforma

- `Dockerfile` multi-stage que publica só a Api e copia `Directory.Packages.props` do contexto `repo-root`
  (modelo: `services/alert-orchestrator/Dockerfile`), mais `.dockerignore`.
- `platform/docker-compose.yml`: serviço com `additional_contexts: repo-root`, variáveis de ambiente, Loki/OTLP,
  e tópicos novos no `kafka-init`.
- `platform/.env.example`: chaves com placeholders (`troque-por-...`).
- Banco: database em `platform/init-db/postgres/1-database.sql` e schema gerado das migrations
  (`dotnet ef migrations script --idempotent`) em `2-schema.sql`; migrations nunca rodam no startup.
- `platform/observability/prometheus/prometheus.yml` (scrape) e, se exposto, `services/api-gateway/ApiGateway.Api/ocelot.json`.
- Quem chamar o serviço por HTTP entra em `Jwt:TrustedServices` dele (ADR-0002).

## 5. Testes e documentação

- Testes mínimos: invariantes do domínio, um caso de uso, autenticação/isolamento de tenant no endpoint
  (`WebApplicationFactory`). Use a skill `test-generation`.
- Atualize `docs/services/<Serviço>.md` (estado da implementação), a lista de serviços na constituição §2.3 só
  com autorização (arquivo protegido), `CLAUDE.md` (protegido, peça autorização) e ADR se a decisão for
  significativa.

## 6. Validação

```bash
dotnet sln Sentinel.sln list | grep services/<nome>
find services/<nome> -name '*.sln'          # deve ser vazio
dotnet build Sentinel.sln -warnaserror
dotnet test services/<nome>/tests/<Pascal>.UnitTests
dotnet format whitespace Sentinel.sln --verify-no-changes
```

Ao final, peça ao subagent `architecture-reviewer` uma revisão do serviço novo e relate o resultado.
