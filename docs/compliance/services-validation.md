# Validação de Conformidade dos Serviços — Fase 1

- **Data:** 2026-10-24
- **Branch:** `chore/services-compliance-validation` (base `ba8d4aa` + `0a48b82`)
- **Referências:** `docs/constitution.md`, `CLAUDE.md`, `.claude/rules/*.md`
- **Status:** Auditoria concluída. Nenhuma correção aplicada além do fix de build da Fase 0.

## 1. Escopo e método

Foram avaliados os 10 serviços listados na constituição §2.3: `api-gateway`, `ingestion`, `risk-catalog`, `geospatial`, `risk-evaluation`, `alert-orchestrator`, `channels`, `reporting`, `tenants-billing` e `audit-compliance`.

A auditoria usou:

- análise estática (estrutura de projetos, referências, `using`, pacotes, controllers, `Program.cs`, DI, consumers, `appsettings*.json`, `ocelot.json`);
- `dotnet build Sentinel.sln --no-incremental`;
- `dotnet test Sentinel.sln` e execução isolada dos projetos com falha;
- `dotnet format Sentinel.sln --verify-no-changes`;
- `dotnet list Sentinel.sln package --vulnerable --include-transitive`.

### Limitações

- Nenhum serviço foi executado contra a infraestrutura do `docker-compose`. Comportamento em runtime (health checks, propagação de trace, conexão com Kafka/Redis) não foi verificado.
- A alcançabilidade das vulnerabilidades de pacotes não foi avaliada.
- A análise é por amostragem dirigida (grep + leitura dos pontos de entrada). Pode haver ocorrências adicionais do mesmo padrão em arquivos não lidos integralmente.

### Severidade

| Nível | Critério |
|---|---|
| **P0** | Acesso sem autenticação a dados ou operações sensíveis, quebra de isolamento entre tenants, perda silenciosa de mensagens, segredo real versionado |
| **P1** | Violação da constituição §13 (dependências, modelos internos expostos, falhas previsíveis não tratadas, solution), vazamento de detalhes internos, testes críticos quebrados |
| **P2** | Desvio de regra sem risco imediato (contrato REST, observabilidade, configuração, cobertura) |
| **P3** | Estilo, nomenclatura, formatação, warnings |

## 2. Baseline (Fase 0)

### Build

`0 erros`, `112 warnings` após o fix `0a48b82`. Distribuição: CS8618 (95), NU1603 (3), CS0618 (3), CS8602 (3), CS0114 (2), CS8620 (1), xUnit1013 (2).

### Testes (`dotnet test Sentinel.sln`)

| Projeto | Resultado | Causa identificada |
|---|---|---|
| Geospatial.UnitTests | ✅ 65/65 | |
| RiskCatalog.UnitTests | ✅ 51/51 | |
| Ingestion.UnitTests | ✅ 24/24 | |
| AlertOrchestrator.UnitTests | ✅ 5/5 | |
| ChannelsService.UnitTests | ✅ 7/7 | |
| Reporting.UnitTests / IntegrationTests | ✅ 21/21, 6/6 | |
| AuditCompliance.UnitTests | ✅ 74/74 | |
| AuditCompliance.IntegrationTests | ⚠️ 6/28 falharam em paralelo; 28/28 isolado (2 execuções) | Não identificada; provável concorrência entre containers no run paralelo |
| **RiskEvaluation.UnitTests** | ❌ 0/10 | `new Mock<RiskCalculationService>()` em classe concreta sem construtor sem parâmetros (`RiskEvaluationServiceTests.cs:33`) |
| **Ingestion.IntegrationTests** | ❌ 0/21 | `BsonSerializer.RegisterSerializer` global em `Ingestion.Api/Program.cs:35` lança exceção quando mais de uma `WebApplicationFactory` sobe no mesmo processo |
| **RiskCatalog.IntegrationTests** | ❌ 22/35 | Testes usam client sem token contra endpoints `[Authorize]`; asserções aceitam até `500` como sucesso |
| RiskEvaluation / Geospatial / AlertOrchestrator.IntegrationTests | ⚠️ vazios | Projeto existe sem nenhum `[Fact]`/`[Theory]` |
| TenantsBilling.* | ✅ 10/10, 1/1 (via `TenantsBilling.sln`) | **Não executados** por `dotnet test Sentinel.sln` (fora da solution) |

## 3. Matriz de conformidade

Legenda: ✅ conforme · ⚠️ desvio parcial · ❌ não conforme · — não aplicável

| Serviço | Estrutura / sln | Camadas | Contrato API | Auth / Tenant | Erros | Config / Segredos | Mensageria / Resiliência | Concorrência / Idempotência | Observabilidade | Testes | Docs |
|---|---|---|---|---|---|---|---|---|---|---|---|
| api-gateway | ⚠️ | — | ❌ | ❌ | ❌ | ✅ | — | — | ❌ | ❌ | ⚠️ |
| ingestion | ✅ | ❌ | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ | ❌ | ❌ | ⚠️ |
| risk-catalog | ✅ | ⚠️ | ❌ | ⚠️ | ❌ | ⚠️ | ⚠️ | ✅ | ❌ | ❌ | ⚠️ |
| geospatial | ⚠️ | ❌ | ❌ | ⚠️ | ❌ | ⚠️ | ✅ | ✅ | ❌ | ⚠️ | ⚠️ |
| risk-evaluation | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ |
| alert-orchestrator | ✅ | ✅ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ⚠️ | ⚠️ | ⚠️ | ⚠️ |
| channels | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ⚠️ | ⚠️ | ❌ | ⚠️ |
| reporting | ✅ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ✅ | ⚠️ | ⚠️ | ⚠️ | ❌ |
| tenants-billing | ❌ | ⚠️ | ⚠️ | ❌ | ❌ | ⚠️ | ❌ | ⚠️ | ❌ | ⚠️ | ⚠️ |
| audit-compliance | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ |

`audit-compliance` é o serviço de referência: já segue `/api/v1/<service>`, JWT + validação de tenant, Problem Details, paginação, rate limiting, `ValidateOnStart`, commit após escrita durável, DLQ e testes de isolamento entre tenants.

## 4. Achados transversais

| ID | Sev. | Achado | Evidência | Regra | Fase |
|---|---|---|---|---|---|
| X-01 | P0 | ~~6 serviços e o gateway não têm autenticação: alert-orchestrator, channels, reporting, risk-evaluation, tenants-billing, api-gateway. Geospatial usa apenas API key compartilhada~~ **Resolvido na Fase 4 (ADR-0002):** JWT com fallback que nega por padrão em todos os serviços; geospatial trocou a API key por JWT; chamadas entre serviços usam tokens de serviço com chave própria por chamador. O `api-gateway` só repassa o `Authorization` (pendente: ADR-0002, decisões pendentes) | `Program.cs` sem `AddAuthentication`; `ocelot.json` sem `AuthenticationOptions` | security.md (Authorization Model), constituição §8.1 | concluído |
| X-02 | P1 | ~~`ExceptionHandlingMiddleware` devolve `exception.Message` em respostas 400 e **500**~~ **Resolvido na Fase 5:** as 5 cópias do middleware não devolvem mais mensagem nem tipo da exceção no 500 e ignoram `/api/v1`, que usa `ApiExceptionHandler` (500 genérico com `traceId`). As rotas legadas congeladas ainda devolvem a mensagem nos 400 de validação | `*/Middlewares/ExceptionHandlingMiddleware.cs:31,44` em ingestion, risk-catalog, geospatial, risk-evaluation, alert-orchestrator | error-handling.md (Unexpected Errors), constituição §7.3 | 5 |
| X-03 | P1 | ~~Erros não usam RFC 9457 Problem Details (exceto audit-compliance); não há códigos de erro estáveis nem `traceId`~~ **Resolvido na Fase 5:** `/api/v1` de todos os serviços responde erros em `application/problem+json` (RFC 9457) com `type` `urn:sentinel:<serviço>:error:<código>`, extensões `code` e `traceId` e erros por campo em `errors` (ver 4.2). As rotas legadas mantêm o formato antigo até o sunset | middlewares acima; channels, reporting e tenants-billing sem handler | error-handling.md (Public HTTP Error Response) | 5 |
| X-04 | P2 | ~~Nenhuma rota segue `/api/v1/<service-name>` exceto audit-compliance; tenants-billing usa `/api/v1/tenants` e `/api/v1/plans`~~ **Resolvido na Fase 5:** todos os serviços expõem `/api/v1/<serviço>`; as rotas antigas estão depreciadas (ver 4.2) | seção 5 de cada serviço | api-design.md (API Conventions) | 5 |
| X-05 | P2 | ~~Nenhum endpoint de coleção tem paginação `page`/`pageSize` (exceto audit-compliance)~~ **Resolvido na Fase 5:** coleções v1 paginadas com `page` (≥ 1), `pageSize` (padrão 10, máximo 50) e ordenação determinística; valores inválidos retornam 400 com erro por campo | risk-catalog, alert-orchestrator, risk-evaluation, ingestion, channels, reporting | api-design.md (Pagination) | 5 |
| X-06 | P2 | ~~Sem `Directory.Packages.props`; 33 pacotes com versões divergentes~~ **Resolvido na Fase 2:** versões centralizadas em `Directory.Packages.props` (71 pacotes, ver seção 4.1). `Directory.Build.props` não foi criado: só traria ganho removendo as propriedades repetidas dos `.csproj` | `Directory.Packages.props` | constituição §2.2, §15.1 | concluído |
| X-07 | P1 | ~~Pacotes vulneráveis (diretos e transitivos), ver tabela abaixo~~ **Resolvido na Fase 2:** `dotnet list Sentinel.sln package --vulnerable --include-transitive` não reporta nenhum pacote (ver tabela) | `dotnet list package --vulnerable` | dependencies.md (Vulnerability Assessment) | concluído |
| X-08 | P2 | ~~Nenhuma opção validada no startup (`ValidateOnStart`) exceto audit-compliance; fallbacks fixos para `localhost` em reporting (4), risk-evaluation (3), alert-orchestrator, channels, tenants-billing~~ **Resolvido nas fases 7 e 8:** fallbacks `localhost` removidos do código (Fase 7); as opções que restavam sem validação no startup (geospatial: `Elasticsearch`, `KafkaConsumerOptions`, `KafkaProducerOptions`) usam `ValidateOnStart` com mensagem que nomeia a chave, coberto por teste. JWT, token de serviço e as opções do audit-compliance já validavam desde a Fase 4 | `?? "localhost..."` em DI / `Program.cs` | configuration.md (Configuration Validation) | 4 |
| X-09 | P2 | ~~Health checks ausentes em geospatial, ingestion, risk-catalog, risk-evaluation; tenants-billing tem só `/health` estático; nenhum serviço além de audit-compliance separa liveness/readiness~~ **Resolvido na Fase 7:** todos os serviços expõem `/health/live` (sem dependências), `/health/ready` (dependências críticas, timeout de 3 s) e `/health` (todas as verificações, mantido por compatibilidade), anônimos e só com o status. Readiness: PostgreSQL (alert-orchestrator e channels quando habilitado, ingestion, risk-catalog, tenants-billing), MongoDB (audit-compliance, ingestion, risk-evaluation), Redis (risk-evaluation, alert-orchestrator/channels quando habilitado) e Elasticsearch + Redis (reporting). Redis do ingestion/risk-catalog, Kafka do channels e Elasticsearch do geospatial aparecem só em `/health` (degradado) | `Program.cs` | observability.md (Health Checks) | 7 |
| X-10 | P2 | ~~Observabilidade heterogênea: OTel tracing ausente em geospatial, ingestion, risk-catalog, risk-evaluation, tenants-billing; ingestion e risk-catalog usam sink Serilog do Elasticsearch em vez de Loki; channels e reporting sem Loki; tenants-billing sem Serilog~~ **Resolvido na Fase 7 (ADR-0004):** cada Api tem uma cópia de `Observability/ObservabilityExtensions`: Serilog com console + Loki (label `service`, propriedades `TraceId`/`SpanId`), traces OTLP para o Tempo (ASP.NET Core, HttpClient e Kafka), métricas Prometheus (ASP.NET Core, HttpClient, runtime e meters próprios). O sink do Elasticsearch saiu do ingestion e do risk-catalog. O contexto W3C atravessa o Kafka pelos headers (`KafkaTracing`) | `Program.cs`, `appsettings.json` | CLAUDE.md (Conventions), observability.md | 7 |
| X-11 | P2 | ~~Testes de autorização e isolamento entre tenants só existem em audit-compliance (e 401 em risk-catalog)~~ **Resolvido na Fase 8:** todos os serviços com API testam 401 e 403 pelo pipeline JWT real; os que guardam dados por tenant testam também o isolamento (alert-orchestrator, audit-compliance, channels, ingestion, reporting, tenants-billing). risk-catalog, risk-evaluation e geospatial não guardam dados por tenant (catálogo global e cálculos sem `TenantId`), então para eles valem só 401/403. Novos: `ChannelsAuthorizationTests` (401 nas 6 rotas, token de outro emissor, role errada, tenant fora do token), testes de ponta a ponta do ingestion e do risk-catalog | `services/*/tests` | testing.md (Critical Flows), constituição §9.1 | 8 |
| X-12 | P3 | ~~`dotnet format --verify-no-changes` falha em 6 serviços: risk-catalog (142), ingestion (82), risk-evaluation (82), alert-orchestrator (6), geospatial (4), channels (2)~~ **Resolvido na Fase 8:** `dotnet format whitespace` aplicado a todos os projetos da solution em commit próprio, só de espaço em branco (26 arquivos); todos passam em `--verify-no-changes`, verificado projeto por projeto | saída do comando | coding-standarts.md (Formatting) | 8 |
| X-13 | P3 | ~~95 warnings CS8618 (ingestion 38, risk-catalog 44, geospatial 9, risk-evaluation 3, channels 1)~~ **Resolvido na Fase 8:** o build completo tem 0 warnings (baseline: 112). Strings com `= string.Empty` (os DTOs de entrada com `[Required]` continuam rejeitando o campo vazio), coleções com `= []`, `required` nos payloads do lote do geospatial (campo ausente vira 400), DTOs do USGS anuláveis (o provedor omite campos; features sem posição são ignoradas) e `= null!` nas navegações EF obrigatórias (padrão documentado do EF Core). As CS8602 eram bugs: payload nulo no lote do geospatial gerava 500 (GEO-14) e o risk-catalog relia o parâmetro regional pelo fator de ajuste (RC-15) | build | coding-standarts.md (Validation) | 8 |
| X-14 | P3 | ~~Enums sem sufixo `Enum` em alert-orchestrator (3), geospatial (2), risk-evaluation (2), tenants-billing (2); demais serviços usam o sufixo~~ **Decidido na Fase 8 (D-1):** os enums existentes não são renomeados (renomear afeta serialização de eventos e contratos); a constituição vale para código novo | `public enum` | constituição §5.1 × coding-standarts.md (conflito, ver §6) | decisão |
| X-15 | P0 | ~~Consumers Kafka que chamam `Consume()` bloqueante no `ExecuteAsync` antes de qualquer `await`: no .NET 9 o loop roda dentro do `StartAsync` e o host não termina de subir enquanto não houver mensagens~~ **Resolvido:** `await Task.Yield()` antes do loop em alert-orchestrator (AO-00), tenants-billing (2), channels (`NotificationConsumerHostedService`) e reporting (`ReportingKafkaConsumerHostedService`, `ReportingDeadLetterConsumerHostedService`), com testes de regressão que falham sem a correção. Reproduzido no tenants-billing: sem "Application started" em 40 s. No geospatial (`SensorEventDetectedConsumer`) o impacto era menor (o `Task.Delay` do fim do loop libera o startup após o primeiro poll, até 5 s de atraso); corrigido da mesma forma | `HostedServices/*`, `Messaging/Consumers/*` | concurrency.md (General Rules) | concluído |
| X-16 | P2 | O `docker-compose` usa defaults locais para credenciais de infraestrutura (`${POSTGRES_PASSWORD:-postgres}`, `${MINIO_ROOT_PASSWORD:-minioadmin}`, `${MONGO_INITDB_ROOT_PASSWORD:-mongoadmin}`, `${AUDIT_COMPLIANCE_DB_PASSWORD:-audit_compliance_local}`) e a senha do Grafana é literal (`GF_SECURITY_ADMIN_PASSWORD=admin`). As novas chaves JWT e de tokens de serviço foram adicionadas sem default | `platform/docker-compose.yml` | configuration.md (Secret Management) | 7 |
| X-17 | P2 | ~~channels e reporting não existem no `platform/docker-compose.yml` (nem em `kafka-init` como consumidores), embora o tenants-billing liste channels em `Jwt:TrustedServices`. Não sobem no ambiente local nem podem ter rota no gateway~~ **Resolvido na Fase 6:** channels (porta 7008) e reporting (7009) no compose com dependências e variáveis de JWT/ServiceToken; tópicos que os serviços já usavam e faltavam no `kafka-init` (`notification-events`, `notification-events-dlq`, `reporting-events` e seus `-dead-letter`/`-replay`) e os DLQs novos; rotas `/gateway/v1/channels` e `/gateway/v1/reporting`; credenciais opcionais dos provedores em `.env.example` | `platform/docker-compose.yml` | CLAUDE.md (Event pipeline) | 6 |
| X-18 | P3 | ~~Conflito MSB3277 de `Microsoft.EntityFrameworkCore.Relational` (9.0.1 vindo do Npgsql × 9.0.20 do EF Design) nos projetos que consomem as Infrastructures com EF~~ **Resolvido na Fase 6:** versão central 9.0.20 e referência explícita nas Infrastructures; os warnings caíram de 105 para 103 | `Directory.Packages.props` | dependencies.md (Compatibility Assessment) | 6 |
| X-19 | P3 | ~~O outbox do ingestion publica num hosted service, fora da requisição que gravou a coleta: o span de publicação inicia um trace novo, sem ligação com o `POST` original. Ligar os dois exigiria gravar o `traceparent` na tabela `outbox` (mudança de schema manual)~~ **Resolvido na Fase 8:** coluna aditiva `outbox.trace_parent` (seção do ingestion no `2-schema.sql`, idempotente, `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`); o repositório grava o `traceparent` da requisição e o relay abre o span `outbox relay` como filho dele, então a publicação no Kafka fica no trace do `POST` (teste de ponta a ponta). Linhas antigas sem valor seguem com trace novo | `OutboxHostedService`, `outbox` | observability.md (Request Context Propagation) | 8 |
| X-20 | P3 | ~~DLQs e replays (reporting, tenants-billing) só preservam o contexto copiado do header original; não abrem span próprio de publicação. O caminho de negócio está coberto~~ **Resolvido na Fase 8:** os sinks de DLQ (7 cópias) e os replays do reporting e do tenants-billing abrem span de publicação e substituem o `traceparent` copiado por um filho do processamento que falhou | `*DeadLetter*`, `*Replay*` | observability.md | 8 |
| X-21 | P3 | Não há alertas nem dashboards além do channels; `prometheus.yml` não tem `rule_files`. Os limiares dependem de baseline (observability.md proíbe inventar valores) | `platform/observability` | observability.md (Alerting Thresholds) | 9 |
| X-22 | P1 | ~~As imagens públicas do MinIO deixaram de ser publicadas (Docker Hub: "repository does not exist"; quay.io: 401): o serviço `minio` do compose e os testes do ingestion não baixavam a imagem numa máquina sem cache~~ **Resolvido na Fase 8:** `cgr.dev/chainguard/minio` (build da Chainguard a partir do fonte do MinIO, AGPL) fixada por digest, no compose e nos testes. A imagem roda como uid 65532: um volume `minio-data` criado pela imagem antiga precisa de `chown` ou ser recriado | `platform/docker-compose.yml` | 8 |

### 4.1 Centralização de versões (X-06)

Regra adotada: maior versão já usada no repositório, dentro da mesma major, com as decisões abaixo.

| Pacote | Versão central | Decisão |
|---|---|---|
| `Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore.*`, `Microsoft.AspNetCore.*` | 9.0.20 | Família alinhada num único patch: o EF Core mais novo em uso (Design 9.0.16) exige `Microsoft.Extensions.*` no mesmo patch ou acima (NU1605), e 9.0.20 é a maior versão já usada (OpenApi), sem downgrade |
| MediatR | 12.5.0 | Mantém Apache-2.0. `Ingestion.Application` voltou de 13.0.0 (licença dupla RPL-1.5/comercial a partir da 13) |
| StackExchange.Redis | 2.9.17 | **Exceção:** `Reporting.Infrastructure` mantém 3.0.0 via `VersionOverride` |
| Microsoft.Extensions.Logging.Abstractions | 9.0.20 | **Exceção:** `Reporting.Infrastructure` mantém 10.0.5 via `VersionOverride`, exigido pelo StackExchange.Redis 3.0.0 |
| Microsoft.IdentityModel.Tokens / JsonWebTokens | 8.19.2 | Exigido pelo JwtBearer 9.0.20 (antes 8.0.1 direto em `RiskCatalog.Application`) |
| System.IdentityModel.Tokens.Jwt | 6.36.0 | Usado só pelo channels (dependência do Twilio 6.x) |
| Confluent.Kafka | 2.13.2 | channels e tenants-billing sobem de 2.2.0; demais de 2.8–2.11 |
| Serilog.AspNetCore | 9.0.0 | geospatial e risk-evaluation sobem de 8.0.3 (os outros 6 serviços já usavam 9.0.0) |

As exceções `VersionOverride` devem ser eliminadas quando ingestion, risk-catalog e risk-evaluation migrarem para StackExchange.Redis 3.x ou o reporting voltar para 2.x.

Os Dockerfiles copiam `Directory.Packages.props` do contexto adicional `repo-root`, definido em `platform/docker-compose.yml`. Build avulso: `docker build --build-context repo-root=. -t <service> services/<service>`.

Achado lateral: `services/alert-orchestrator/Dockerfile` usa `mcr.microsoft.com/dotnet/sdk:10.0-preview` no estágio de build, enquanto o projeto é `net9.0` e o `global.json` fixa o SDK 9.0.115 (AO-09).

### 4.2 Contrato v1 (Fase 5)

Decisões D-3 e D-4: o contrato novo vive em `/api/v1/<serviço>`. As rotas antigas ficam congeladas e depreciadas, com os cabeçalhos `Deprecation`, `Sunset` e `Link` (rota sucessora) e `deprecated: true` no OpenAPI. Prazo padrão de 90 dias: anúncio em 2026-10-25, sunset em 2027-01-23.

| Serviço | Base v1 | Rotas legadas depreciadas |
| --- | --- | --- |
| ingestion | `/api/v1/ingestion` | `/api/ingestion/*` |
| risk-catalog | `/api/v1/risk-catalog` | `/api/event-types/*`, `/api/risk-model/*` |
| geospatial | `/api/v1/geospatial` | `/api/geospatial/*` |
| risk-evaluation | `/api/v1/risk-evaluation` | `/api/risk-evaluation/*` |
| alert-orchestrator | `/api/v1/alert-orchestrator` | `/api/alert-windows/*` |
| channels | `/api/v1/channels` | `/api/notifications/*`, `/api/dlq/*` |
| reporting | `/api/v1/reporting` | `/api/reporting/*` |
| tenants-billing | `/api/v1/tenants-billing` | `/api/v1/tenants/*`, `/api/v1/plans` |
| audit-compliance | `/api/v1/audit-compliance` (já seguia o padrão) | — |

Regras comuns do v1:

- Sucesso: envelope `ResponseBaseDTO<T>` (`statusCode`, `success`, `data`); `204` sem corpo.
- Erro: `application/problem+json` com `type` `urn:sentinel:<serviço>:error:<código>`, `title`, `status`, `detail`, `instance` e as extensões `code` e `traceId`; validação traz `errors` por campo. Exceções inesperadas viram 500 genérico, registradas no log com stack trace.
- Paginação: `page` ≥ 1, `pageSize` 1–50 (padrão 10), resposta com `items`, `page`, `pageSize`, `totalItems`, `totalPages`; parâmetros inválidos retornam 400.
- Tenant: lido das claims (`tenant_id`); tenant do body ou da rota fora das claims retorna 403 `TENANT_ACCESS_DENIED`. Principais de serviço têm escopo de plataforma nas leituras.

Códigos de erro comuns: `VALIDATION_ERROR` (400), `AUTHENTICATION_REQUIRED` (401), `ACCESS_DENIED` (403), `TENANT_ACCESS_DENIED` (403), `RESOURCE_NOT_FOUND` (404), `CONFLICT` (409), `UNPROCESSABLE_REQUEST` (422), `RATE_LIMIT_EXCEEDED` (429), `SERVICE_UNAVAILABLE` (503), `INTERNAL_ERROR` (500). Códigos específicos:

| Serviço | Códigos |
| --- | --- |
| ingestion | `TENANT_NOT_FOUND` (422), `TENANT_INACTIVE` (422), `DATA_SOURCE_NOT_FOUND` (422), `DATA_SOURCE_ALREADY_EXISTS` (409) |
| risk-catalog | `EVENT_TYPE_NOT_FOUND` (404 em leitura, 422 quando referenciado), `RISK_MATRIX_NOT_FOUND`, `IDF_CURVE_NOT_FOUND`, `REGIONAL_PARAMETERS_NOT_FOUND` (404), `EVENT_TYPE_ALREADY_EXISTS`, `SEVERITY_ALREADY_EXISTS`, `SEVERITY_CRITERION_OVERLAP`, `RISK_MATRIX_ALREADY_EXISTS`, `IDF_CURVE_ALREADY_EXISTS`, `REGIONAL_PARAMETERS_ALREADY_EXIST` (409), `SEVERITY_NOT_FOUND`, `CATALOG_VERSION_NOT_FOUND` (422), `TENANT_CONTEXT_REQUIRED` (403) |
| risk-evaluation | `INVALID_RISK_MODEL` (400) |
| channels | `NOTIFICATION_DELIVERY_FAILED` (502) |
| tenants-billing | `TENANT_NOT_FOUND` (404), `TENANT_ALREADY_EXISTS` (409), `PLAN_NOT_FOUND` (422) |

O gateway expõe `/gateway/v1/<serviço>/{everything}` para os serviços presentes no compose e mantém as rotas `/gateway/<serviço>/...` existentes.

### Vulnerabilidades (X-07)

| Pacote | Sev. | Projetos afetados | Origem provável |
|---|---|---|---|
| Snappier 1.0.0 | High | ingestion (todos), risk-evaluation (todos) | MongoDB.Driver antigo |
| SharpCompress 0.30.1 | Moderate | ingestion, risk-evaluation | MongoDB.Driver antigo |
| System.Text.Json 7.0.3 | High | Ingestion.Api, Ingestion.IntegrationTests | `Microsoft.VisualStudio.Web.CodeGeneration.Design` |
| Microsoft.Build 17.10.4 | High | Ingestion.Api, Ingestion.IntegrationTests | `CodeGeneration.Design` |
| System.Security.Cryptography.Pkcs / Xml 8.0.0 | High | RiskCatalog.Api, RiskCatalog.IntegrationTests | `CodeGeneration.Design` |
| NuGet.Packaging / NuGet.Protocol | Low | Ingestion.Api, RiskCatalog.Api | `CodeGeneration.Design` |
| SSH.NET 2023.0.0 / 2025.1.0 | High | RiskCatalog.IntegrationTests, Ingestion.IntegrationTests | Testcontainers antigos |
| System.IdentityModel.Tokens.Jwt / Microsoft.IdentityModel.JsonWebTokens 6.19.0 | Moderate | channels (Api, Infrastructure, UnitTests) | Twilio |
| OpenTelemetry.Api 1.11.2 | Moderate | AlertOrchestrator.Api, ChannelsService.Api, Reporting.Api | OTel 1.11.x |

`Microsoft.VisualStudio.Web.CodeGeneration.Design` é ferramenta de scaffolding e não deveria ser dependência de runtime dos projetos Api.

**Remediação aplicada (Fase 2):**

| Origem | Ação |
|---|---|
| `CodeGeneration.Design` | Removido de `Ingestion.Api` e `RiskCatalog.Api` (nenhum código o utiliza) |
| MongoDB.Driver 3.1.0 / 3.5.0 | → 3.12.0 (mesma versão do audit-compliance) em ingestion e risk-evaluation |
| Testcontainers 4.0.0 / 4.9.0 | → 4.14.0 nos integration tests de ingestion e risk-catalog; imagens passadas pelo construtor do builder (o construtor sem parâmetros ficou obsoleto) |
| OpenTelemetry 1.11.2 | → 1.15.3 (`Extensions.Hosting`) e 1.15.3-beta.1 (`Exporter.Prometheus.AspNetCore`) em alert-orchestrator, channels e reporting |
| Twilio 6.0.0 → JWT 6.19.0 | `System.IdentityModel.Tokens.Jwt` fixado em 6.36.0 (mesma major) em `ChannelsService.Infrastructure`, sem upgrade de major do Twilio |

## 5. Achados por serviço

### 5.1 api-gateway

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| GW-01 | P2 | ~~Sem rotas para alert-orchestrator, channels, reporting e tenants-billing~~ **Resolvido na Fase 5:** rota `/gateway/v1/<serviço>/{everything}` → `/api/v1/<serviço>/{everything}` para os 7 serviços presentes no compose (inclui alert-orchestrator e tenants-billing). channels e reporting continuam sem rota porque não existem no `docker-compose.yml` (X-17) | `ocelot.json` | 5 |
| GW-02 | P2 | ~~`climatic` e `disaster` não têm rota no gateway. (O endpoint `sensor-collection`, antes comentado, foi reativado com roteamento automático Climatic/Disaster pelo `MeasurementType` do data source.)~~ **Resolvido na Fase 5:** coletas expostas pela rota v1 do ingestion (`sensor-collections`, `climatic-collections`, `disaster-collections`). As rotas legadas `climatic`/`disaster` não ganharam rota no gateway por estarem depreciadas | `ocelot.json` | 5 |
| GW-03 | P2 | ~~Sem logging estruturado, tracing, health check ou rate limiting~~ **Resolvido na Fase 7:** observabilidade da plataforma, health checks e rate limit nativo por IP de origem (janela fixa, 300/min configurável em `RateLimiting`, 429 com `Retry-After` e Problem Details `RATE_LIMIT_EXCEEDED`; `X-Forwarded-For` ignorado; health e `/metrics` isentos). Novo projeto `ApiGateway.IntegrationTests` | `ApiGateway.Api/Program.cs` | 7 |
| GW-04 | P2 | Layout fora do padrão (`services/api-gateway/ApiGateway.Api/` sem `src/`) e sem testes | estrutura | 2 / 8 |

### 5.2 ingestion

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| ING-01 | P0 | ~~`TenantId` vem do body dos comandos e não é verificado contra as claims do JWT; qualquer identidade com `IngestionWrite` escreve em qualquer tenant~~ **Resolvido na Fase 4:** os 4 comandos de escrita conferem o `TenantId` com as claims `tenant_id` (403 se não pertencer) | `Application/Commands/Register*Command.cs:10-11`; `IngestionController.cs:46-77` | concluído |
| ING-02 | P1 | ~~Application referencia `Infrastructure.Read` e usa `MongoDB.Driver` / `Minio` diretamente (inversão de dependência)~~ **Resolvido na Fase 3:** porta `IDetectedEventReadRepository` na Application, implementação Mongo em `Infrastructure.Read` (que passou a referenciar a Application); `FireFirmsApiResponseDTO` e CsvHelper movidos para `Infrastructure.Write`; bucket do MinIO injetado no provider. Application depende só de Domain, MediatR e Logging.Abstractions | `Ingestion.Application.csproj:18`; `Handlers/GetCollectionStatisticsHandler.cs`, `Handlers/GetLatestDetectedEventsHandler.cs`, `Services/SensorCollectionService.cs`; `DTO/CollectionStatisticsResponseDTO.cs` (atributos Bson) | concluído |
| ING-03 | P1 | ~~Credenciais versionadas em `appsettings.json`: senha na connection string, MinIO `minioadmin`, `AuthKey: "changeme"`~~ **Resolvido na Fase 4:** senha, credenciais MinIO, `Jwt:Key` e `Scalar:AuthToken` removidos dos `appsettings*.json`; valores vêm de variáveis de ambiente/User Secrets | `Ingestion.Api/appsettings.json:11,19-20,34` | concluído |
| ING-04 | P1 | ~~Integration tests 0/21. A exceção de serializer Bson duplicado foi corrigida (`TryRegisterSerializer`), mas a suíte segue quebrada por causas independentes: (a) `RemoveAll<OutboxHostedService>()` não remove o hosted service (registrado como `IHostedService`), e o outbox derruba o host porque a tabela `outbox` não existe no Postgres do Testcontainers; (b) nenhum teste envia JWT para endpoints `[Authorize]` (401); (c) os comandos não preenchem `TenantId`, hoje `[Required]`; (d) os testes de `sensor-collection` foram escritos para o handler antigo; o endpoint foi reativado com roteamento automático e os testes precisam ser revistos~~ **Resolvido na Fase 8:** a suíte antiga (contrato que não existe mais: sem tenant, sem token, mocks que não removiam o outbox) foi substituída por `Stack/`, de ponta a ponta com Postgres (a seção real do `2-schema.sql`), Mongo, Redis, MinIO e Kafka: rotas legadas de fonte de dados e coleta, 401/403, concorrência, transação do outbox, coleta repetida sem duplicar linhas, relay no trace da requisição e isolamento de tenant no v1. 0/21 → 34/34. Os testes revelaram ING-16 a ING-21 | `IngestionWebApplicationFactory.cs:50` | 8 |
| ING-05 | P2 | ~~API devolve `SensorEventDetected` (evento de aplicação) e objetos anônimos em vez de DTOs; `limit` sem paginação~~ **Resolvido na Fase 5:** `/api/v1/ingestion` usa DTOs (`DetectedEventDTO`, `DetectedEventStatisticsDTO`, `CollectionAcceptedDTO`), paginação em `detected-events` e erros tipados (`TENANT_NOT_FOUND`, `TENANT_INACTIVE`, `DATA_SOURCE_NOT_FOUND`, `DATA_SOURCE_ALREADY_EXISTS`). As coletas respondem 202, porque o retorno é o `DataSourceId` e os eventos seguem pelo outbox | `IngestionController.cs:25,66,76` | 5 |
| ING-06 | P2 | ~~Retry HTTP com `retryCount: 3` = 4 tentativas (limite é 3 incluindo a inicial)~~ **Resolvido na Fase 6:** `retryCount: 2` (3 tentativas incluindo a primeira) | `Infrastructure.Write/ServiceCollectionExtension.cs:238-245` | 6 |
| ING-07 | P2 | ~~Deduplicação Redis em duas operações (`KeyExistsAsync` + `StringSetAsync`), não atômica entre instâncias~~ **Resolvido na Fase 6:** reivindicação atômica com `SET NX` e lease, liberada em caso de falha; o marcador concluído mantém o valor antigo, compatível com versões anteriores | `Cache/Deduplicators/RedisEventDeduplicator.cs:22,33` | 6 |
| ING-08 | P2 | ~~Sem OTel e health checks; logs via sink do Elasticsearch~~ **Resolvido na Fase 7:** OTel, Loki e health checks (readiness: PostgreSQL e MongoDB) | `Ingestion.Api/Program.cs` | 7 |
| ING-09 | P3 | ~~38 warnings CS8618; 82 violações de formatação; CS0114 e CS0618~~ **Resolvido na Fase 8:** warnings zeradas (X-13); a formatação segue no commit de X-12 | build / format | 8 |
| ING-10 | P1 | ~~`GET last-detected-events` e `GET collection-statistics` consultam a coleção Mongo `events`, mas a ingestão grava em `climaticEvents` / `disasterEvents`: as consultas não enxergam eventos ingeridos. Comportamento preservado na refatoração da Fase 3~~ **Resolvido na Fase 5:** `GET /api/v1/ingestion/detected-events[/statistics]` lê `climaticEvents` e `disasterEvents` com `$unionWith` (domínio derivado da coleção de origem). As rotas legadas continuam lendo `events` (congeladas até o sunset) | `Infrastructure.Read/Persistence/Repositories/MongoDetectedEventReadRepository.cs` | 5 |
| ING-11 | P2 | ~~`GET last-detected-events` e `GET collection-statistics` não são isolados por tenant: os eventos do read model não carregam tenant, então qualquer identidade com `Ingestion.Read` lê dados de todos os tenants~~ **Resolvido na Fase 5:** a projeção grava `TenantId`, `EventId` e `CollectionId`; as consultas v1 filtram pelos tenants do token (principal de serviço vê todos). Documentos antigos sem tenant ficam visíveis só para principais de serviço. Índice `{TenantId, CollectedAt, EventId}` adicionado ao `2-schema.js` | `MongoDetectedEventReadRepository.cs` | 5 |
| ING-12 | P1 | ~~A projeção gravava `SensorEventDetected` (sem `EventId`) em `climaticEvents`/`disasterEvents`, que têm índice único em `EventId` no `2-schema.js`: a segunda projeção na mesma coleção falhava com chave duplicada `null`~~ **Resolvido na Fase 5:** a projeção grava `EventId` novo por evento (teste de regressão com Mongo 7 e o mesmo índice) | `MongoDetectedEventReadRepository.cs`, `SensorCollectionService.cs` | 5 |
| ING-13 | P1 | ~~`SensorPollingHostedService` chama `ProcessSensorCollection<object>`; o serviço cria o evento com `Activator.CreateInstance(typeof(TEvent), ...)`, e `object` não tem esse construtor → `MissingMethodException` após registrar a coleta e subir o payload no MinIO. Coletas por polling nunca projetam nem publicam eventos~~ **Resolvido na Fase 6:** o parâmetro genérico e o `Activator.CreateInstance` foram removidos; o evento é criado diretamente, com `EventId` determinístico (mesmo da projeção), então coletas por polling e retentativas não geram duplicatas | `HostedServices/SensorPollingHostedService.cs:48`, `SensorCollectionService.cs` | 6 |
| ING-14 | P2 | ~~`[NotInFuture]` comparava o valor recebido (UTC) com `DateTime.Now` (local); como a comparação de `DateTime` ignora `Kind`, em servidor UTC-3 qualquer instante das últimas 3 h era rejeitado como futuro e, com offset positivo, datas futuras passavam~~ **Resolvido na Fase 5:** comparação em UTC, `Unspecified` tratado como UTC; testes em UTC-3, UTC e UTC+9. Em contêineres (UTC) o comportamento não muda | `Application/Attributes/NotInFuture.cs` | 5 |
| ING-15 | P2 | ~~`EventId` dos eventos detectados era gerado aleatoriamente a cada tentativa, então uma coleta retentada após falha parcial duplicava projeção e eventos publicados~~ **Resolvido na Fase 6 (ADR-0003):** id determinístico por amostra da coleta; a projeção ignora a chave duplicada | `SensorCollectionService.cs`, `MongoDetectedEventReadRepository.cs` | 6 |
| ING-16 | P1 | ~~`WriteDbContext` era singleton com uma única `NpgsqlConnection`, usada ao mesmo tempo por todas as requisições e pelo outbox (o Npgsql não suporta isso)~~ **Resolvido na Fase 8:** `NpgsqlDataSource` compartilhado e uma conexão por escopo; teste com 12 cadastros concorrentes | `WriteDbContext.cs`, `ServiceCollectionExtension.cs` | 8 |
| ING-17 | P1 | ~~O relay marcava as linhas do outbox com `WHERE aggregate_id = @id` passando o id da linha, então nenhuma era marcada e todo evento era republicado a cada 10 s; e o publicador engolia a falha do Kafka (com o filtro corrigido, um evento não publicado seria marcado como processado). Uma exceção ao ler o outbox derrubava o host~~ **Resolvido na Fase 8:** filtro por `id`, publicador relança a falha, lote numa transação com `FOR UPDATE SKIP LOCKED` (antes sem efeito, fora de transação), relay resiliente a falhas do banco e intervalo configurável (`Outbox:IntervalSeconds`). O relay também deixou de liberar o publicador e o producer, que pertencem ao container de DI (o `Flush` num handle já destruído quebrava o desligamento do host) | `OutboxRepository.cs`, `OutboxHostedService.cs`, `SensorEventDetectedPublisher.cs` | 8 |
| ING-18 | P1 | ~~Não havia transactional outbox: `data_collection` e `outbox` eram gravados sem transação; as amostras nunca eram gravadas (`SampleSensorRepository` sem uso e apontando para `sample_sensor`, a tabela é `sensor_sample`); fonte de dados e permissões também eram gravadas separadamente~~ **Resolvido na Fase 8:** porta `IUnitOfWork`; coleta, amostras e outbox numa transação, fonte de dados e permissões em outra; ids determinísticos de coleta, amostra e mensagem com `ON CONFLICT DO NOTHING`, então uma coleta retentada não duplica linhas | `SensorCollectionService.cs`, `RegisterDataSourceHandler.cs`, repositórios | 8 |
| ING-19 | P1 | ~~A seção do ingestion no `2-schema.sql` não rodava: `\c` quebrado em duas linhas e vírgula sobrando em `data_collection`, então `data_collection` e `sensor_sample` não eram criadas no compose~~ **Resolvido na Fase 8:** script corrigido e idempotente; os testes de ponta a ponta executam essa seção real | `platform/init-db/postgres/2-schema.sql` | 8 |
| ING-20 | P2 | ~~`IConnectionMultiplexer` registrado como scoped: uma conexão Redis nova por requisição, nunca fechada~~ **Resolvido na Fase 8:** singleton; `ConnectionStrings:Redis` obrigatória | `ServiceCollectionExtension.cs` | 8 |
| ING-21 | P2 | `SensorPollingHostedService` está comentado no DI: o ingestion não faz polling das fontes externas (USGS, FIRMS, NOAA, Stormglass), ao contrário do que a documentação descreve. Ligar exige credenciais e fontes de dados configuradas por tenant. Também em P2 do mesmo fluxo: `DateTime.Now` (hora local) no domínio, corrigido para UTC na Fase 8 (`CreatedAt` e janela de frequência, com testes) | `ServiceCollectionExtension.cs:AddHostedServices` | decisão |

### 5.3 risk-catalog

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| RC-01 | P1 | ~~`Guid.Parse(User.FindFirst("tenantId")?.Value!)` gera 500 quando a claim falta~~ **Resolvido na Fase 4:** tenant lido de `tenant_id` (fallback para a claim legada `tenantId`); sem claim válida retorna 403 | `RiskModelController.cs:114` | concluído |
| RC-02 | P1 | ~~Falhas de operação viram `Problem(... statusCode: 500)` genérico; not found e listas vazias retornam `204`~~ **Resolvido na Fase 5:** v1 devolve 404 Problem Details para recurso inexistente, lista paginada vazia em vez de 204, 409/422 com códigos estáveis (`RiskCatalogException`, derivada de `ArgumentException` para manter os 400 das rotas legadas) e 500 genérico só quando a persistência não conclui | `EventTypeController.cs:26-117`; `RiskModelController.cs:35-124` | 5 |
| RC-03 | P1 | ~~13 integration tests falhando e a suíte não valida comportamento real: GETs usam client sem token; `CreateAuthenticatedClient()` cria uma `new RiskCatalogWebApplicationFactory()` sem `InitializeAsync` (containers parados); nada cria o schema (ver RC-06), por isso as asserções aceitam `500`; `GeospatialIntegrationTests` chama rotas inexistentes (`/api/riskmodel/regional-parameters`) e aceita `404` como sucesso~~ **Resolvido na Fase 8:** a suíte antiga (asserções que aceitavam 500, client sem token, containers parados, rotas inexistentes) foi substituída por `Stack/LegacyCatalogApiTests`, de ponta a ponta com Postgres (seção `risk_catalog` do `2-schema.sql`), Redis e Kafka: o que é criado é relido do banco, a publicação chega ao Kafka com `publishedBy`, e 400/401/403/204 seguem o contrato congelado das rotas legadas (RC-02). 14 falhas → 42/42 | `EventTypeControllerIntegrationTest.cs:25-33,72,87,100`; `GeospatialIntegrationTests.cs:44,55` | 8 (depende de RC-06) |
| RC-04 | P2 | ~~Application referencia JwtBearer / IdentityModel e lança `BadHttpRequestException` (tipo HTTP)~~ **Resolvido na Fase 3:** pacotes JwtBearer/IdentityModel/Configuration/Http.Json removidos da Application (JwtBearer agora referenciado pelo Api); `BadHttpRequestException` trocada por `ArgumentException` (mesmo 400, ver RC-11) | `RiskCatalog.Application.csproj:10`; `Services/EventTypeService.cs:1,328` | concluído |
| RC-05 | P2 | ~~Api referencia EF Core / Npgsql diretamente~~ **Resolvido na Fase 3:** EF Core e Npgsql removidos do Api (não eram usados) | `RiskCatalog.Api.csproj` | concluído |
| RC-06 | P2 | ~~`RiskCatalogDbContext` sem migrations EF versionadas; schema depende de script externo~~ **Resolvido na Fase 6:** migration de baseline `InitialBaseline`; a seção `risk_catalog` do `2-schema.sql` passa a ser o script idempotente gerado dela (sem migrations pendentes nem divergência do modelo, teste com Postgres 15.5). O script anterior não correspondia ao modelo, então bancos criados por ele precisam ser recriados | `Infrastructure/Persistence` | 6 |
| RC-07 | P2 | ~~Rotas `/api/event-types`, `/api/risk-model` (sem versão e sem nome do serviço)~~ **Resolvido na Fase 5:** `/api/v1/risk-catalog/{event-types,severities,severity-criteria,risk-matrices,idf-curves,regional-risk-parameters,catalog-publications}`; rotas antigas depreciadas | controllers | 5 |
| RC-08 | P2 | ~~Retry HTTP com 4 tentativas~~ **Resolvido na Fase 6:** `retryCount: 2` (3 tentativas incluindo a primeira) | `InfrastructureServiceCollectionExtension.cs:104-115` | 6 |
| RC-09 | P2 | ~~Sem OTel e health checks; sink do Elasticsearch~~ **Resolvido na Fase 7:** OTel, Loki e health checks (readiness: PostgreSQL) | `Program.cs` | 7 |
| RC-10 | P3 | ~~44 CS8618, 142 violações de formatação, xUnit1013 (2)~~ **Resolvido na Fase 8:** warnings zeradas (X-13; xUnit1013 e CS0114 saíram com a suíte antiga); a formatação segue no commit de X-12 | build / format | 8 |
| RC-11 | P3 | ~~Efeito colateral do RC-04: para o erro de sobreposição de faixa de severidade, o campo `type` do corpo de erro 400 passou de `"BadHttpRequestException"` para `"ArgumentException"` (status inalterado). O campo expõe nome de tipo interno e será substituído por Problem Details (X-03)~~ **Resolvido na Fase 5:** em v1 o tipo interno não aparece mais (Problem Details com `code` `SEVERITY_CRITERION_OVERLAP`). A rota legada mantém o corpo antigo até o sunset | `EventTypeService.cs` | 5 |
| RC-12 | P1 | ~~O risk-catalog chamava o geospatial sem nenhuma credencial (o geospatial exigia `X-Api-Key`), então essas chamadas falhavam com 401~~ **Resolvido na Fase 4:** risk-catalog usa token de serviço (`Geospatial.Read`) | `InfrastructureServiceCollectionExtension.cs` | concluído |
| RC-13 | P1 | ~~As configurações EF usavam tipos do SQL Server (`uniqueidentifier`, `nvarchar`, `double`) e `uuid_generate_v4()` (extensão não instalada): o schema gerado pelo modelo não era válido no Postgres~~ **Resolvido na Fase 6:** tipos Postgres (`uuid`, `varchar`, `double precision`, `integer`) e `gen_random_uuid()` | `Persistence/DatabaseConfigurations/*.cs` | 6 |
| RC-14 | P2 | A criação de parâmetro regional rejeita ("já existe") qualquer região com o mesmo fator de ajuste de outra; duas regiões diferentes com fator 1,5 não podem coexistir. Mudar a regra exige definir o que identifica uma região | `RiskModelService.CreateRegionalRiskParametersAsync` | decisão |
| RC-15 | P2 | ~~Depois de inserir o parâmetro regional, o serviço o relia pelo fator de ajuste (não é único) só para invalidar o cache, podendo invalidar a região errada ou gerar NullReferenceException~~ **Resolvido na Fase 8:** usa o id da entidade inserida; teste de regressão | `RiskModelService.cs` | 8 |

### 5.4 geospatial

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| GEO-01 | P1 | ~~Controller usa `IGeospatialEventRepository` (Domain) direto e devolve `GeospatialOperationEvent` (evento de domínio) no contrato~~ **Camadas resolvidas na Fase 3:** buscas via `ISearchGeospatialEventsUseCase`. **Resolvido na Fase 5:** `/api/v1/geospatial` devolve DTOs (`GeospatialV1DTOs`), sem `GeospatialOperationEvent` | `GeospatialController.cs:5-6,21,39-67` | 5 |
| GEO-02 | P2 | ~~API key única compartilhada, comparação com `string.Equals` (não constante no tempo), sem contexto de tenant~~ **Resolvido na Fase 4:** API key removida; JWT com política `GeospatialRead` e tokens de serviço de ingestion, risk-catalog e risk-evaluation | `Attributes/ApiKeyAuthAttribute.cs:23` | concluído |
| GEO-03 | P2 | ~~Respostas em objetos anônimos e `BadRequest("...")` em texto~~ **Resolvido na Fase 5:** v1 usa envelope `ResponseBaseDTO` e Problem Details com erros por campo | `GeospatialController.cs:60,77,89,104,115` | 5 |
| GEO-04 | P2 | ~~Placeholder de chave em `appsettings.json` (`CHANGE_ME_...`), aceito no startup~~ **Resolvido na Fase 4:** `APIKey` removida dos `appsettings` | `Geospatial.Api/appsettings.json:65` | concluído |
| GEO-05 | P2 | ~~Sem OTel, métricas e health checks~~ **Resolvido na Fase 7:** OTel, métricas e health checks (Elasticsearch só em `/health`, porque as operações geométricas não dependem dele) | `Program.cs` | 7 |
| GEO-06 | P2 | ~~Integration tests vazios~~ **Resolvido nas fases 4 a 8:** 19 integration tests (autorização, contrato v1, validação de opções no startup) | `tests/Geospatial.IntegrationTests` | 8 |
| GEO-07 | P3 | ~~NU1603 (Confluent.Kafka 2.7.0 inexistente) resolvido fixando 2.8.0, a versão já resolvida; restam 9 CS8618 e 2 CS8602~~ **Resolvido na Fase 8:** as 9 CS8618 e as 2 CS8602 também (X-13, GEO-14) | csproj / build | 8 |
| GEO-12 | P1 | ~~Cada amostra era indexada com `Id(CollectionId)`; as amostras de uma coleta compartilham o id, então cada uma sobrescrevia a anterior e só a última ficava no índice~~ **Resolvido na Fase 6:** id do documento é o `EventId` do evento (com fallback para `CollectionId` em produtores antigos) | `ElasticsearchGeospatialEventRepository.cs` | 6 |
| GEO-13 | P1 | ~~O consumidor esperava 30 s depois de cada mensagem e mandava qualquer falha do Elasticsearch (inclusive indisponibilidade) direto para o DLQ, sem retentar; se o DLQ falhasse, a mensagem era perdida~~ **Resolvido na Fase 6 (ADR-0003):** kit de consumo confiável; Elasticsearch fora do ar é transitório | `SensorEventDetectedConsumer.cs` | 6 |
| GEO-14 | P2 | ~~Item do lote com `payload` nulo, ausente ou malformado gerava 500 (NullReferenceException / JsonException)~~ **Resolvido na Fase 8:** `ArgumentException` (400) preservando a causa; testes de regressão | `BatchRequestMapper.cs` | 8 |

O consumer (`SensorEventDetectedConsumer.cs`) está conforme: commit manual após processamento e envio para `geospatial-dead-letter`.

### 5.5 risk-evaluation

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| RE-01 | P0 | ~~Consumer sem `EnableAutoCommit = false` (padrão `true`): offsets podem ser confirmados antes do processamento; sem DLQ e sem idempotência~~ **Resolvido na Fase 6 (ADR-0003):** consumidor confiável com commit manual, DLQ `risk-evaluation-dead-letter` e idempotência por `EventId` (ou tópico/partição/offset). Corrigida também a vazão: o consumidor lia uma mensagem a cada 30 s e reassinava os tópicos a cada iteração | `Infrastructure/Messaging/KafkaMessageConsumer.cs:24-30` | 6 |
| RE-02 | P0 | ~~API sem autenticação, incluindo `PUT model` que altera o modelo de risco~~ **Resolvido na Fase 4:** políticas de leitura/escrita e `PUT model` restrito a `RiskEvaluation.Admin` | `RiskEvaluationController.cs:112` | concluído |
| RE-03 | P1 | ~~Domain depende de MediatR (`INotification` em contratos)~~ **Resolvido na Fase 3:** `SensorEventDetected` e `RiskCatalogPublishedEvent` são contratos de mensagens consumidas e foram movidos para `RiskEvaluation.Application/Contracts`; Domain sem dependências | `RiskEvaluation.Domain.csproj:10`; `Domain/Contracts/*.cs` | concluído |
| RE-04 | P1 | ~~Resposta de `UpdateRiskModel` inclui `ex.Message`~~ **Resolvido na Fase 5:** `PUT /api/v1/risk-evaluation/model` (só `RiskEvaluation.Admin`) responde `INVALID_RISK_MODEL` sem repassar a mensagem. Correção: o handler só captura `ArgumentException` (validação do modelo); falhas de infraestrutura propagam como 500. A rota legada ainda devolve a mensagem de validação | `Application/Handlers/UpdateRiskModelCommandHandler.cs:81` | 5 |
| RE-05 | P1 | ~~Validação rejeita latitude/longitude `0` (coordenadas válidas); `history` recebe lat/lon como `int`~~ **Resolvido na Fase 5:** v1 aceita `0` (`[Range]` em `EvaluateRiskRequest`; `history` valida faixa). A precisão inteira permanece (RE-11) | `RiskEvaluationController.cs:28-38,67-82` | 5 |
| RE-06 | P1 | ~~Unit tests 0/10 (mock de classe concreta)~~ **Corrigido:** os testes faziam `Setup` de método não virtual e de método `static`; passaram a usar o `RiskCalculationService` real com entradas derivadas dos pesos padrão (10/10) | `RiskEvaluationServiceTests.cs` | concluído |
| RE-07 | P2 | ~~HttpClients para risk-catalog e geospatial sem timeout explícito (padrão 100 s > limite de 60 s) e sem retry; API key vazia aceita~~ **Resolvido na Fase 6:** timeout de 10 s e 2 retentativas (só consultas, seguras para repetir) nos clientes de risk-catalog e geospatial; Redis com `AbortOnConnectFail=false` | `Infrastructure/ServiceCollectionExtension.cs:44-55` | 6 |
| RE-08 | P2 | ~~Placeholder `"sua-api-key-aqui"` versionado; 3 fallbacks `localhost`~~ **Resolvido na Fase 4 (chave):** `ApiKey` removida; os fallbacks `localhost` seguem pendentes em X-08 | `appsettings.json:74` | concluído |
| RE-09 | P2 | ~~Sem OTel e health checks; integration tests vazios~~ **Resolvido na Fase 7 (observabilidade):** OTel e health checks (readiness: MongoDB e Redis). Os integration tests já cobrem o armazenamento (Mongo/Redis) e o cliente do risk-catalog (18 testes) | `Program.cs`; `tests/RiskEvaluation.IntegrationTests` | 7 / 8 |
| RE-10 | P3 | ~~82 violações de formatação, 3 CS8618, enums sem sufixo~~ **Resolvido na Fase 8:** CS8618 zeradas; enums sem sufixo mantidos (D-1); a formatação segue no commit de X-12 | format / build | 8 |
| RE-12 | P1 | ~~`MapEventTypeToRiskMetrics` comparava o tipo em minúsculas com rótulos `temperatureAnomaly`, `humidityAnomaly`, `windGust` e `pressureChange`, que nunca coincidiam: essas quatro métricas climáticas nunca chegavam à avaliação~~ **Resolvido na Fase 6:** rótulos em minúsculas, teste por tipo | `SensorEventDetectedHandler.cs` | 6 |
| RE-13 | P1 | ~~Contrato `risk-catalog-published` incompatível: risk-catalog publica `{ Version: int, Notes, PublishedAt, PublishedBy }`, risk-evaluation espera `{ Version: string, Parameters, Formula }`. Antes a falha de desserialização era perdida pelo commit automático; agora cada publicação vai para `risk-evaluation-dead-letter`. Requer decisão de contrato~~ **Resolvido na Fase 6 (decisão: alinhar ao produtor):** risk-evaluation lê o contrato real `{ Version: int, Notes, PublishedAt, PublishedBy }`, registra a versão em `CatalogVersions` (upsert por versão) e invalida os scores em cache (`SCAN` em `risk:score:*`). `RiskModel`/`Formula` seguem só pelo `PUT model`. Mensagens desse tópico que já estejam em `risk-evaluation-dead-letter` podem ser reprocessadas com o novo contrato | `RiskCatalog.Application/Events/RiskCatalogPublishedEvent.cs`; `RiskEvaluation.Application/Contracts/RiskCatalogPublishedEvent.cs` | 6 |
| RE-17 | P1 | ~~O consumidor de `risk-catalog-published` lia o payload sem diferenciar maiúsculas, mas o risk-catalog publica em camelCase (`"version": 3`): `Version` ficava 0 e toda publicação ia para o DLQ, apesar da correção do RE-13. O teste usava PascalCase~~ **Resolvido na Fase 6:** desserialização sem diferenciar maiúsculas; teste com o mesmo serializador do produtor | `RiskEvaluationEventConsumer.cs` | 6 |
| RE-14 | P2 | ~~Os clientes HTTP de risk-catalog e geospatial transformam respostas de erro em valores padrão (`null`, lista vazia, `false`, `double.MaxValue`), então uma dependência fora do ar produz uma avaliação com dados faltando em vez de falhar; também chamam rotas legadas (`/api/risk-model/...`, `/api/geospatial/...`), depreciadas até 2027-01-23~~ **Resolvido na Fase 6 (decisão: remover código morto):** a única chamada externa usada (`GET /api/geospatial/context`) ia para um endpoint inexistente e sempre devolvia 404; os demais métodos dos clientes de risk-catalog e geospatial nunca eram chamados. Clientes, DTOs e o token de serviço do risk-evaluation foram removidos, assim como a confiança nele em risk-catalog e geospatial (compose e `.env.example`); a avaliação roda explicitamente sem contexto geográfico (ver RE-15) | `HttpClients/*.cs` | 6 |
| RE-15 | P2 | ~~Lacuna de funcionalidade: o score usa só os pesos de `RiskFactors`; não usa dados do risk-catalog (matrizes, curvas IDF, parâmetros regionais) nem contexto geográfico, porque nenhum serviço expõe esse contexto. Requer desenho de produto e um endpoint v1 no geospatial~~ **Resolvido na Fase 6 (decisão: região + matriz, snapshot local, maior fator):** a cada `risk-catalog-published`, risk-evaluation copia a versão para `CatalogSnapshots` pelos novos endpoints paginados `GET /api/v1/risk-catalog/{regional-risk-parameters,severity-criteria,risk-matrix-entries}` (token de serviço com `RiskCatalog.Read`). O score é multiplicado pelo maior `AdjustmentFactor` das regiões que contêm o ponto (polígono ou círculo por centro e raio, limitado a 0,5–2,0), e o nível final é o maior entre o do score e o que a matriz atribui à intensidade do evento. A avaliação usa só a cópia local. Continuam fora: terreno, elevação e distância da costa, que nenhum serviço fornece | `RiskEvaluationService.cs` | 6 |
| RE-16 | P1 | ~~No compose, risk-evaluation não recebia `ConnectionStrings__Redis` e usava `localhost:6379`, então cache de scores e idempotência do consumidor não funcionavam~~ **Resolvido na Fase 6:** `ConnectionStrings__Redis=redis:6379` e dependência do serviço `redis` | `platform/docker-compose.yml` | 6 |

### 5.6 alert-orchestrator

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| AO-00 | P0 | ~~Quando `Consume` retorna `null` após 5 s sem mensagens, o `ExecuteAsync` faz `return`: o consumer encerra definitivamente (sem `Close()`) e o serviço deixa de processar `risk-updated` e `region-intersected`~~ **Corrigido:** `continue` no poll vazio e `await Task.Yield()` antes do loop (sem ele, o `Consume` bloqueante rodaria dentro do `StartAsync` e travaria o host); testes de regressão em `UnitTests/Messaging/EventConsumerPollingTests.cs` | `Messaging/Consumers/RiskUpdatedEventConsumer.cs`; `RegionIntersectedEventConsumer.cs` | concluído |
| AO-01 | P0 | ~~Em erro de processamento o offset não é commitado nem a mensagem vai para DLQ; o próximo commit bem-sucedido avança além dela (perda silenciosa)~~ **Resolvido na Fase 6 (ADR-0003):** os dois consumidores usam o kit de consumo confiável (commit após tratamento durável, DLQ `alert-orchestrator-dead-letter`, retentativa limitada a 3 tentativas) | `Messaging/Consumers/RiskUpdatedEventConsumer.cs:65-74`; idem `RegionIntersectedEventConsumer.cs` | 6 |
| AO-02 | P0 | ~~API sem autenticação; consultas por região/status retornam janelas de todos os tenants~~ **Resolvido na Fase 4:** política de leitura; consultas filtradas pelos tenants da identidade na Application; janela de outro tenant por id é tratada como inexistente | `AlertWindowsController.cs` | concluído |
| AO-03 | P1 | ~~`AlertWindow` sem token de concorrência; quórum/escalonamento com várias instâncias pode sobrescrever atualizações~~ **Resolvido na Fase 6:** token de concorrência otimista em `xmin` (sem coluna nova); a raiz é marcada como modificada em toda atualização, então inserir um sinal também incrementa a versão; conflito é tratado como falha transitória e reprocessado (teste com Postgres 15.5) | `AlertOrchestratorDbContext.cs` (sem `IsConcurrencyToken`/`xmin`) | 6 |
| AO-04 | P2 | ~~Idempotência Redis em get-then-set não atômico~~ **Resolvido na Fase 6 (ADR-0003):** reivindicação atômica `SET NX` com lease em `alert-orchestrator:idempotency:v2:{eventId}`, honrando a chave antiga até expirar (teste com Redis 7, 20 reivindicações concorrentes) | `RedisIdempotencyService.cs:20,31` | 6 |
| AO-05 | P2 | ~~`GetById` retorna `204` para recurso inexistente; rotas sem versão; sem paginação~~ **Resolvido na Fase 5:** `GET /api/v1/alert-orchestrator/alert-windows/{id}` responde 404 Problem Details; listagem paginada ordenada por `OpenedAt` desc e `Id` | `AlertWindowsController.cs:23` | 5 |
| AO-06 | P2 | ~~DbContext sem migrations EF versionadas~~ **Resolvido na Fase 6:** migration de baseline `InitialBaseline` com os nomes do script original; a seção `alert_orchestrator` do `2-schema.sql` é o script idempotente gerado dela. Bancos criados pelo script anterior: `platform/init-db/postgres/baselines/alert_orchestrator-InitialBaseline-align.sql` (verificado: o schema resultante só difere na ordem física das colunas, dados preservados, reexecutável) | `Infrastructure/Persistence` | 6 |
| AO-07 | P2 | ~~Tracing sem métricas; integration tests vazios~~ **Resolvido na Fase 7:** métricas Prometheus e instrumentação ASP.NET Core/HttpClient/runtime | `Program.cs`; `tests/AlertOrchestrator.IntegrationTests` | 7 / 8 |
| AO-08 | P3 | ~~Enums sem sufixo `Enum` (3); 6 violações de formatação~~ **Resolvido na Fase 8:** enums sem sufixo mantidos (D-1); a formatação segue no commit de X-12 | | 8 |
| AO-09 | P2 | ~~Estágio de build do Dockerfile usa `sdk:10.0-preview` (SDK preview, diferente do `global.json` 9.0.115)~~ **Resolvido na Fase 7:** imagens `sdk:9.0` e `aspnet:9.0` | `services/alert-orchestrator/Dockerfile:7` | 7 |
| AO-10 | P3 | ~~O filtro de tenant das consultas de janelas é aplicado em memória, depois da leitura do repositório; filtrar na consulta ao banco reduziria leitura desnecessária~~ **Resolvido na Fase 6:** `SearchAsync` filtra região, status e tenant (sem diferenciar maiúsculas) na consulta ao banco, com ordenação e paginação no SQL | `AlertWindowQueryService.cs` | 6 |
| AO-11 | P0 | ~~Com Postgres, qualquer atualização de uma janela com sinais lançava `InvalidOperationException` (chave sombra `Signal.Id` perdida com `AsNoTracking` + `Update`): a janela não recebia o segundo sinal e nenhum alerta era disparado. `metadata` era gravado como texto numa coluna `jsonb`~~ **Resolvido na Fase 6:** leituras que alimentam atualizações carregam entidades rastreadas; `metadata` mapeado como `jsonb` (teste com Postgres 15.5 e o schema real) | `PostgreSqlAlertWindowRepository.cs`, `AlertWindowConfiguration.cs` | 6 |
| AO-12 | P1 | ~~No compose, alert-orchestrator recebia chaves que a aplicação não lê (`ConnectionStrings__PostgreSql`, `Kafka__RiskUpdatedTopic`...): rodava com repositórios em memória, consumia `risk-evaluated` em vez de `risk-updated` e publicava em `alert-orchestrator-event`, que o `kafka-init` não cria~~ **Resolvido na Fase 6:** variáveis `Database__*`, `Cache__*`, `Kafka__Topics__*` e `Services__TenantsBillingBaseUrl` | `platform/docker-compose.yml` | 6 |

### 5.7 channels

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| CH-01 | P0 | ~~Consumer com `EnableAutoCommit = true`: falha após o commit perde a notificação~~ **Resolvido na Fase 6 (ADR-0003):** consumidor confiável com commit manual e DLQ de consumo `channels-dead-letter`; reprocessamento não reenvia canais já entregues para o mesmo `EventId` e tenant | `HostedServices/NotificationConsumerHostedService.cs:49` | 6 |
| CH-02 | P0 | ~~API sem autenticação; `POST api/dlq/reprocess` e `POST api/notifications` disparam envios reais (SMS/WhatsApp/e-mail)~~ **Resolvido na Fase 4:** políticas de leitura/escrita; envio, reprocessamento e consulta de tentativas conferem o tenant | `DeadLetterController.cs:19`; `NotificationsController.cs:33` | concluído |
| CH-03 | P1 | ~~Controllers recebem `NotificationEvent` (Domain) e devolvem entidades/repositório de domínio~~ **Camadas resolvidas na Fase 3:** `INotificationDispatchService` (entrega + DLQ, antes duplicada no controller e no consumer) e `IDeliveryAttemptQueryService`. **Resolvido na Fase 5:** `/api/v1/channels` recebe `SendNotificationRequest` e devolve `DeliveryAttemptDTO`/`DeliveryResultDTO` | `NotificationsController.cs:2-3,29`; `DeadLetterController.cs:20` | 5 |
| CH-04 | P1 | ~~Retry repete qualquer exceção (incl. não transitórias) em envio não idempotente (risco de SMS duplicado); `maxAttempts` é número de *retries*~~ **Resolvido na Fase 6:** retentativa só para falhas transitórias (provedores marcam `IsTransient` por status 5xx/408/429 e por exceção); `MaxRetries` passa a ser o total de tentativas, limitado a 3; circuit breaker por provedor mantido entre chamadas (antes era recriado a cada envio e nunca abria); circuito aberto devolve falha transitória e o fallback segue para o próximo canal | `Application/Services/RetryPolicyEngine.cs:24-29` | 6 |
| CH-05 | P1 | ~~Falha de entrega retorna `500` com o resultado interno no body~~ **Resolvido na Fase 5:** falha de entrega em v1 responde 502 `NOTIFICATION_DELIVERY_FAILED` sem detalhes do provedor (teste garante que o erro do provedor não vaza) | `NotificationsController.cs:42` | 5 |
| CH-06 | P2 | ~~Senha em connection string no `appsettings.json`~~ **Resolvido na Fase 4:** senha removida da connection string | `ChannelsService.Api/appsettings.json:23` | concluído |
| CH-07 | P2 | ~~Polly na camada Application~~ **Resolvido na Fase 3:** `RetryPolicyEngine` (Polly) movido para `ChannelsService.Infrastructure/Services`; registro duplicado na Application removido | `ChannelsService.Application.csproj:13` | concluído |
| CH-08 | P2 | ~~Sem projeto de IntegrationTests; diretório órfão `services/channels-service/` (removido, nunca foi versionado); spec duplicada `services/channels/ChannelsService.Spec.md` × `docs/services/Channels-Service.md`~~ **Resolvido nas fases 6 a 8:** `ChannelsService.IntegrationTests` com 27 testes (contrato v1, autorização, persistência). Pendente (Fase 9): a spec duplicada | estrutura | 8 / 9 |
| CH-09 | P2 | ~~Sem exception handler; sem sink Loki~~ **Resolvido nas fases 5 e 7:** exception handler v1 (Problem Details) e sink Loki. Corrigido também o `/metrics`, que derrubava toda requisição do processo com `PrometheusExporter could not be found` porque o exporter não era registrado (coberto por teste) | `Program.cs`, `appsettings.json` | 5 / 7 |
| CH-10 | P2 | ~~`ChannelProviderBase.ResilienceOptions` nunca era atribuída, então o timeout e o circuit breaker configurados por provedor eram ignorados~~ **Resolvido na Fase 6:** devolve as opções do provedor | `Providers/ChannelProviderBase.cs` | 6 |
| CH-11 | P1 | ~~A propriedade `Status` virou `StatusEnum` sem mapeamento de coluna: o modelo divergia da migration e toda gravação em `delivery_attempts` falharia no banco criado por ela~~ **Resolvido na Fase 6:** `HasColumnName("Status")`, sem migration nova; a seção `channels` do `2-schema.sql` é gerada das migrations (teste com Postgres 15.5) | `ChannelsServiceDbContext.cs` | 6 |

### 5.8 reporting

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| REP-01 | P0 | ~~`tenantId` vem da rota sem autenticação: qualquer cliente lê summary/analytics/export de qualquer tenant~~ **Resolvido na Fase 4:** GETs exigem `ReportingRead` e tenant da rota presente nas claims | `ReportingController.cs:36-77` | concluído |
| REP-02 | P0 | ~~`POST events`, `events/envelope` e `events/replay` sem autenticação permitem injetar eventos e publicar no tópico de replay~~ **Resolvido na Fase 4:** `events` e `events/envelope` exigem `ReportingWrite` e conferem o tenant do evento; `events/replay` restrito a `Reporting.Admin` | `ReportingController.cs:86-144` | concluído |
| REP-03 | P1 | ~~Controller usa Infrastructure (`ReportingKafkaConsumerHostedService`), repositório de domínio e cria `ProducerBuilder` do Kafka por requisição~~ **Resolvido na Fase 3:** replay via porta `IReportingReplayPublisher` (Kafka em `Infrastructure/Messaging`), `POST events` via `ReportingEventIngestionService`; controller depende só de Application/Domain | `ReportingController.cs:1,8,19,110-119` | concluído |
| REP-04 | P1 | ~~Contrato expõe entidades de domínio (`DashboardSummary`, `AnalyticsReport`, `ReportingEvent`)~~ **Resolvido na Fase 5:** `/api/v1/reporting` usa `DashboardSummaryDTO`, `AnalyticsReportDTO`, `ProcessingStatusDTO`, `ReplayResultDTO` e requests próprios | `ReportingController.cs:37,44,87` | 5 |
| REP-05 | P2 | ~~`GET health` recebe `tenantId` sem binding definido e duplica o papel de health check~~ **Resolvido na Fase 5:** em v1 virou `GET tenants/{tenantId}/processing-status` com tenant na rota e verificação de acesso; o health check segue em `/health` | `ReportingController.cs:79-84` | 5 / 7 |
| REP-06 | P2 | ~~Sem exception handler, sem Scalar, sem Loki; 4 fallbacks `localhost`~~ **Resolvido nas fases 5 e 7:** exception handler v1; Scalar (Development), Loki e fallbacks `localhost` removidos | `Program.cs` | 5 / 7 |
| REP-07 | P2 | `CLAUDE.md` referencia `docs/reporting-service-status.md`, que não existe | `CLAUDE.md` | 9 |
| REP-08 | P1 | ~~Falha ao publicar no DLQ ou no tópico de replay era engolida e o offset era confirmado (perda); outra exceção no laço deixava a mensagem sem commit, mas o commit seguinte avançava além dela~~ **Resolvido na Fase 6:** a falha propaga e o consumidor volta ao offset e pausa. Sem teste automatizado: o consumidor cria o cliente Kafka internamente | `ReportingKafkaConsumerHostedService.cs`, `ReportingDeadLetterConsumerHostedService.cs` | 6 |

### 5.9 tenants-billing

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| TB-01 | P0 | ~~API sem autenticação: qualquer cliente cria tenants e planos e registra uso; outros serviços confiam nesse serviço para decidir se um tenant está ativo~~ **Resolvido na Fase 4:** políticas de leitura/escrita; consulta e registro de uso conferem o tenant; ingestion, alert-orchestrator e channels chamam com token de serviço (`TenantsBilling.Read`) | `TenantsBilling.Api/Program.cs:36-72` | concluído |
| TB-02 | P1 | ~~Projetos fora da `Sentinel.sln` e `TenantsBilling.sln` próprio~~ **Resolvido na Fase 2:** 6 projetos registrados na `Sentinel.sln` (pasta `services/tenants-billing`), `TenantsBilling.sln` removido; `dotnet sln list` cobre os 55 `.csproj` do repositório | `Sentinel.sln` | concluído |
| TB-03 | P1 | ~~`GET /api/v1/tenants/{id}` devolve a entidade de domínio; endpoints acessam `ITenantRepository` direto~~ **Camadas resolvidas na Fase 3:** `GET` via `TenantManagementService.GetTenantAsync`. **Resolvido na Fase 5:** `GET /api/v1/tenants-billing/tenants/{id}` devolve `TenantDTO` com `status` como string | `Program.cs:50-54` | 5 |
| TB-04 | P2 | ~~Api referencia Confluent.Kafka e monta producer/publisher no `Program.cs`~~ **Resolvido na Fase 3:** producer/publisher Kafka registrados em `AddTenantsBillingInfrastructure`; Api sem Confluent.Kafka | `TenantsBilling.Api.csproj:10`; `Program.cs:18-22` | concluído |
| TB-05 | P2 | ~~Rotas `/api/v1/tenants` e `/api/v1/plans` sem o nome do serviço. **Atenção:** ingestion, alert-orchestrator e channels consomem `GET /api/v1/tenants/{id}`~~ **Resolvido na Fase 5:** rotas `/api/v1/tenants-billing/{tenants,plans}`; `/api/v1/tenants` e `/api/v1/plans` depreciadas. Consumidores internos migrados (passo 2 do TB-09) | `TenantBillingGateway` em cada serviço | 5 |
| TB-06 | P2 | ~~Sem Serilog, OTel, exception handler, Scalar e validação de entrada; `UseHttpsRedirection` ausente~~ **Resolvido nas fases 5 e 7:** exception handler, validação de entrada, Serilog/Loki, OTel, Scalar (Development) e `UseHttpsRedirection` | `Program.cs` | 5 / 7 |
| TB-07 | P0 | ~~Consumer de usage: em exceção não commita nem publica em DLQ e segue o loop, então o próximo commit avança além da mensagem (perda silenciosa, mesmo padrão de AO-01); payload inválido é commitado e descartado sem DLQ. O consumer de `tenant-usage-events-dlq` existe, mas não há produtor identificado~~ **Resolvido na Fase 6 (ADR-0003):** consumidor de uso com commit manual e DLQ no formato lido pelo consumidor de DLQ (payload inválido como "Invalid payload structure", terminal); o consumidor de DLQ não engole mais a falha ao publicar o replay; o consumidor de uso também lê `tenant-usage-events-replay`, que não tinha consumidor | `TenantUsageEventConsumerHostedService.cs:67-72,96-99` | 6 |
| TB-08 | P2 | ~~Apenas 1 integration test~~ **Resolvido nas fases 4 a 7:** 31 integration tests (autorização, contrato v1, persistência, consumo de uso) | `tests/TenantsBilling.IntegrationTests` | 8 |
| TB-09 | P1 | ~~Contrato `GET /api/v1/tenants/{id}` quebrado entre serviços: `Status` é serializado como número (sem `JsonStringEnumConverter`), mas o `TenantBillingGateway` do ingestion o lê como `string` → `JsonException` capturada → **toda validação de tenant do ingestion retorna falso**. Os gateways de alert-orchestrator e channels só checam o HTTP 200 → **tenant suspenso/cancelado é aceito**~~ **Passo 1 resolvido na Fase 5 (expand):** os 3 `TenantBillingGateway` aceitam o corpo legado (`status` numérico) e o envelope v1 (`data.status` string) e passam a validar o status (antes alert-orchestrator e channels só checavam HTTP 200; o gateway do alert-orchestrator também tinha um parâmetro `baseUrl` sem registro no DI). **Passo 2 (switch):** os 3 gateways chamam `/api/v1/tenants-billing/tenants/{id}` (teste de rota em cada serviço). Implantar depois do tenants-billing com v1; a rota antiga continua respondendo até o sunset, então o rollback do consumidor é seguro | `TenantsBilling.Api/Program.cs`; `*/TenantBillingGateway.cs` | 5 |
| TB-10 | P1 | ~~`RecordUsage` avalia cada evento isoladamente contra limites fixos (80/100), sem acumular uso nem consultar os limites do plano; o uso não é persistido. Requer decisão de domínio (agregação por período e limites do plano)~~ **Resolvido na Fase 6 (decisão: mês no fuso do tenant):** `TenantUsage` acumula consumo por tenant e mês civil no fuso do tenant (fuso vazio ou inválido usa `America/Sao_Paulo`); o status é o maior percentual entre os quatro recursos contra os limites do plano e `SoftLimitPercentage`/`HardLimitPercentage`, e não regride dentro do período. Eventos de cota saem uma vez por nível e período, com os campos atuais e os aditivos `Period`, `Usage` e `UsagePercentage`. Cada `SourceEventId` é contado uma vez, na mesma transação do incremento (`recorded_usage_events`), com concorrência otimista em `xmin`. Tenant suspenso ou cancelado responde `HardLimit` sem acumular. Chamadas diretas à API não são deduplicadas | `Domain/Aggregates/Tenant.cs`, `PostgresTenantRepository.cs` | 6 |
| TB-11 | P1 | ~~As migrations do tenants-billing nunca eram aplicadas (nem no startup nem pelo `2-schema.sql`), então no compose o banco `tenants_billing` subia sem tabelas~~ **Resolvido na Fase 6:** seção `tenants_billing` do `2-schema.sql` gerada das migrations (inclui `AddTenantUsage`), testada com Postgres 15.5 | `platform/init-db/postgres/2-schema.sql` | 6 |

### 5.10 audit-compliance

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| AUD-01 | P2 | ~~6 integration tests instáveis no run paralelo da solution (passam isolados)~~ **Causa identificada e corrigida na Fase 8:** com usuário e senha, o entrypoint do `mongo:7` sobe um `mongod` temporário para criar o root e depois o definitivo; sob a carga da solution inteira o temporário ainda segurava a porta e o definitivo saía com código 48 ("Address already in use"). Afetava também o ingestion. Os contêineres Mongo dos testes (audit-compliance, ingestion, risk-evaluation) sobem sem credenciais, o que pula a fase temporária; nenhum desses testes exercita autenticação do Mongo | baseline | 8 |
| AUD-02 | P3 | ~~2 warnings CS0618 (API obsoleta)~~ **Resolvido na Fase 8:** construtores do Testcontainers com a imagem | build | 8 |

## 6. Conflitos entre regras e decisões pendentes

| # | Conflito | Fontes | Recomendação |
|---|---|---|---|
| D-1 | Sufixo `Enum` obrigatório × proibido | constituição §5.1 × `coding-standarts.md` | **Decidido (2026-10-28):** a constituição vale para código novo; os 9 enums existentes não são renomeados (X-14) |
| D-2 | Constantes UPPER_SNAKE_CASE × PascalCase | constituição §5.1 × `coding-standarts.md` | Nenhuma constante UPPER_SNAKE existe (96 PascalCase). A constituição condiciona a "quando esse padrão for adotado": manter PascalCase |
| D-3 | `ResponseErrorBaseDTO` × RFC 9457 Problem Details | `api-design.md` × `error-handling.md` | Problem Details (já adotado em audit-compliance) |
| D-4 | Migração das rotas para `/api/v1/<service>` é *breaking change* | `api-design.md` × `backward-compatibility.md` | Expand-and-contract: rotas novas + antigas marcadas `deprecated`, 90 dias, atualização do `ocelot.json` e dos gateways internos |
| D-5 | Emissor e configuração JWT dos serviços sem auth | `security.md` (não inventar settings do IdP) | Reaproveitar o modelo de `JwtOptions` do audit-compliance; emissor/audiência a confirmar |
| D-6 | Correções de teste antes ou depois da auditoria | — | Corrigir RE-06, ING-04 e RC-03 cedo (início da Fase 2) para ter baseline verde nas fases seguintes |

## 7. Ordem de execução proposta

1. **Fase 2 (monorepo):** TB-02, X-06, X-07 (remover `CodeGeneration.Design` e atualizar MongoDB.Driver/Testcontainers/OTel), CH-08 (diretório órfão), GEO-07, GW-04. Incluir as correções de testes de D-6 se aprovado.
2. **Fase 3 (camadas):** ING-02, RE-03, REP-03, GEO-01, CH-03, CH-07, RC-04, RC-05, TB-03, TB-04.
3. **Fase 4 (segurança):** X-01, ING-01, REP-01, REP-02, TB-01, CH-02, RE-02, AO-02, RC-01, GEO-02, segredos (ING-03, CH-06, GEO-04, RE-08), X-08. Depende de D-5.
4. **Fase 5 (contratos):** X-02, X-03, X-04, X-05 e os achados de contrato por serviço. Depende de D-3 e D-4.
5. **Fase 6 (mensageria e persistência):** AO-00 (candidato a correção imediata), RE-01, AO-01, CH-01, CH-04, TB-07, AO-03, AO-04, ING-06, ING-07, RC-06, RC-08, AO-06, RE-07.
6. **Fase 7 (observabilidade):** X-09, X-10, GW-03, e os itens de observabilidade por serviço.
7. **Fase 8 (testes e qualidade):** X-11, X-12, X-13, integration tests vazios, CH-08, AUD-01.
8. **Fase 9 (documentação):** REP-07, atualização de `docs/services/*`, `CLAUDE.md`, `backward-compatibility.md` e ADRs para D-4 e D-5.
