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
| X-02 | P1 | `ExceptionHandlingMiddleware` devolve `exception.Message` em respostas 400 e **500** | `*/Middlewares/ExceptionHandlingMiddleware.cs:31,44` em ingestion, risk-catalog, geospatial, risk-evaluation, alert-orchestrator | error-handling.md (Unexpected Errors), constituição §7.3 | 5 |
| X-03 | P1 | Erros não usam RFC 9457 Problem Details (exceto audit-compliance); não há códigos de erro estáveis nem `traceId` | middlewares acima; channels, reporting e tenants-billing sem handler | error-handling.md (Public HTTP Error Response) | 5 |
| X-04 | P2 | Nenhuma rota segue `/api/v1/<service-name>` exceto audit-compliance; tenants-billing usa `/api/v1/tenants` e `/api/v1/plans` | seção 5 de cada serviço | api-design.md (API Conventions) | 5 |
| X-05 | P2 | Nenhum endpoint de coleção tem paginação `page`/`pageSize` (exceto audit-compliance) | risk-catalog, alert-orchestrator, risk-evaluation, ingestion, channels, reporting | api-design.md (Pagination) | 5 |
| X-06 | P2 | ~~Sem `Directory.Packages.props`; 33 pacotes com versões divergentes~~ **Resolvido na Fase 2:** versões centralizadas em `Directory.Packages.props` (71 pacotes, ver seção 4.1). `Directory.Build.props` não foi criado: só traria ganho removendo as propriedades repetidas dos `.csproj` | `Directory.Packages.props` | constituição §2.2, §15.1 | concluído |
| X-07 | P1 | ~~Pacotes vulneráveis (diretos e transitivos), ver tabela abaixo~~ **Resolvido na Fase 2:** `dotnet list Sentinel.sln package --vulnerable --include-transitive` não reporta nenhum pacote (ver tabela) | `dotnet list package --vulnerable` | dependencies.md (Vulnerability Assessment) | concluído |
| X-08 | P2 | Nenhuma opção validada no startup (`ValidateOnStart`) exceto audit-compliance; fallbacks fixos para `localhost` em reporting (4), risk-evaluation (3), alert-orchestrator, channels, tenants-billing | `?? "localhost..."` em DI / `Program.cs` | configuration.md (Configuration Validation) | 4 |
| X-09 | P2 | Health checks ausentes em geospatial, ingestion, risk-catalog, risk-evaluation; tenants-billing tem só `/health` estático; nenhum serviço além de audit-compliance separa liveness/readiness | `Program.cs` | observability.md (Health Checks) | 7 |
| X-10 | P2 | Observabilidade heterogênea: OTel tracing ausente em geospatial, ingestion, risk-catalog, risk-evaluation, tenants-billing; ingestion e risk-catalog usam sink Serilog do Elasticsearch em vez de Loki; channels e reporting sem Loki; tenants-billing sem Serilog | `Program.cs`, `appsettings.json` | CLAUDE.md (Conventions), observability.md | 7 |
| X-11 | P2 | Testes de autorização e isolamento entre tenants só existem em audit-compliance (e 401 em risk-catalog) | `services/*/tests` | testing.md (Critical Flows), constituição §9.1 | 8 |
| X-12 | P3 | `dotnet format --verify-no-changes` falha em 6 serviços: risk-catalog (142), ingestion (82), risk-evaluation (82), alert-orchestrator (6), geospatial (4), channels (2) | saída do comando | coding-standarts.md (Formatting) | 8 |
| X-13 | P3 | 95 warnings CS8618 (ingestion 38, risk-catalog 44, geospatial 9, risk-evaluation 3, channels 1) | build | coding-standarts.md (Validation) | 8 |
| X-14 | P3 | Enums sem sufixo `Enum` em alert-orchestrator (3), geospatial (2), risk-evaluation (2), tenants-billing (2); demais serviços usam o sufixo | `public enum` | constituição §5.1 × coding-standarts.md (conflito, ver §6) | decisão |
| X-15 | P0 | ~~Consumers Kafka que chamam `Consume()` bloqueante no `ExecuteAsync` antes de qualquer `await`: no .NET 9 o loop roda dentro do `StartAsync` e o host não termina de subir enquanto não houver mensagens~~ **Resolvido:** `await Task.Yield()` antes do loop em alert-orchestrator (AO-00), tenants-billing (2), channels (`NotificationConsumerHostedService`) e reporting (`ReportingKafkaConsumerHostedService`, `ReportingDeadLetterConsumerHostedService`), com testes de regressão que falham sem a correção. Reproduzido no tenants-billing: sem "Application started" em 40 s. No geospatial (`SensorEventDetectedConsumer`) o impacto era menor (o `Task.Delay` do fim do loop libera o startup após o primeiro poll, até 5 s de atraso); corrigido da mesma forma | `HostedServices/*`, `Messaging/Consumers/*` | concluído |
| X-16 | P2 | O `docker-compose` usa defaults locais para credenciais de infraestrutura (`${POSTGRES_PASSWORD:-postgres}`, `${MINIO_ROOT_PASSWORD:-minioadmin}`, `${MONGO_INITDB_ROOT_PASSWORD:-mongoadmin}`, `${AUDIT_COMPLIANCE_DB_PASSWORD:-audit_compliance_local}`) e a senha do Grafana é literal (`GF_SECURITY_ADMIN_PASSWORD=admin`). As novas chaves JWT e de tokens de serviço foram adicionadas sem default | `platform/docker-compose.yml` | 7 |

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
| GW-01 | P2 | Sem rotas para alert-orchestrator, channels, reporting e tenants-billing | `ocelot.json` | 5 |
| GW-02 | P2 | `climatic` e `disaster` não têm rota no gateway. (O endpoint `sensor-collection`, antes comentado, foi reativado com roteamento automático Climatic/Disaster pelo `MeasurementType` do data source.) | `ocelot.json` | 5 |
| GW-03 | P2 | Sem logging estruturado, tracing, health check ou rate limiting | `ApiGateway.Api/Program.cs` | 7 |
| GW-04 | P2 | Layout fora do padrão (`services/api-gateway/ApiGateway.Api/` sem `src/`) e sem testes | estrutura | 2 / 8 |

### 5.2 ingestion

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| ING-01 | P0 | ~~`TenantId` vem do body dos comandos e não é verificado contra as claims do JWT; qualquer identidade com `IngestionWrite` escreve em qualquer tenant~~ **Resolvido na Fase 4:** os 4 comandos de escrita conferem o `TenantId` com as claims `tenant_id` (403 se não pertencer) | `Application/Commands/Register*Command.cs:10-11`; `IngestionController.cs:46-77` | concluído |
| ING-02 | P1 | ~~Application referencia `Infrastructure.Read` e usa `MongoDB.Driver` / `Minio` diretamente (inversão de dependência)~~ **Resolvido na Fase 3:** porta `IDetectedEventReadRepository` na Application, implementação Mongo em `Infrastructure.Read` (que passou a referenciar a Application); `FireFirmsApiResponseDTO` e CsvHelper movidos para `Infrastructure.Write`; bucket do MinIO injetado no provider. Application depende só de Domain, MediatR e Logging.Abstractions | `Ingestion.Application.csproj:18`; `Handlers/GetCollectionStatisticsHandler.cs`, `Handlers/GetLatestDetectedEventsHandler.cs`, `Services/SensorCollectionService.cs`; `DTO/CollectionStatisticsResponseDTO.cs` (atributos Bson) | concluído |
| ING-03 | P1 | ~~Credenciais versionadas em `appsettings.json`: senha na connection string, MinIO `minioadmin`, `AuthKey: "changeme"`~~ **Resolvido na Fase 4:** senha, credenciais MinIO, `Jwt:Key` e `Scalar:AuthToken` removidos dos `appsettings*.json`; valores vêm de variáveis de ambiente/User Secrets | `Ingestion.Api/appsettings.json:11,19-20,34` | concluído |
| ING-04 | P1 | Integration tests 0/21. A exceção de serializer Bson duplicado foi corrigida (`TryRegisterSerializer`), mas a suíte segue quebrada por causas independentes: (a) `RemoveAll<OutboxHostedService>()` não remove o hosted service (registrado como `IHostedService`), e o outbox derruba o host porque a tabela `outbox` não existe no Postgres do Testcontainers; (b) nenhum teste envia JWT para endpoints `[Authorize]` (401); (c) os comandos não preenchem `TenantId`, hoje `[Required]`; (d) os testes de `sensor-collection` foram escritos para o handler antigo; o endpoint foi reativado com roteamento automático e os testes precisam ser revistos | `IngestionWebApplicationFactory.cs:50` | 8 |
| ING-05 | P2 | API devolve `SensorEventDetected` (evento de aplicação) e objetos anônimos em vez de DTOs; `limit` sem paginação | `IngestionController.cs:25,66,76` | 5 |
| ING-06 | P2 | Retry HTTP com `retryCount: 3` = 4 tentativas (limite é 3 incluindo a inicial) | `Infrastructure.Write/ServiceCollectionExtension.cs:238-245` | 6 |
| ING-07 | P2 | Deduplicação Redis em duas operações (`KeyExistsAsync` + `StringSetAsync`), não atômica entre instâncias | `Cache/Deduplicators/RedisEventDeduplicator.cs:22,33` | 6 |
| ING-08 | P2 | Sem OTel e health checks; logs via sink do Elasticsearch | `Ingestion.Api/Program.cs` | 7 |
| ING-09 | P3 | 38 warnings CS8618; 82 violações de formatação; CS0114 e CS0618 | build / format | 8 |
| ING-10 | P1 | `GET last-detected-events` e `GET collection-statistics` consultam a coleção Mongo `events`, mas a ingestão grava em `climaticEvents` / `disasterEvents`: as consultas não enxergam eventos ingeridos. Comportamento preservado na refatoração da Fase 3 | `Infrastructure.Read/Persistence/Repositories/MongoDetectedEventReadRepository.cs` | 5 |
| ING-11 | P2 | `GET last-detected-events` e `GET collection-statistics` não são isolados por tenant: os eventos do read model não carregam tenant, então qualquer identidade com `Ingestion.Read` lê dados de todos os tenants | `MongoDetectedEventReadRepository.cs` | 5 |

### 5.3 risk-catalog

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| RC-01 | P1 | ~~`Guid.Parse(User.FindFirst("tenantId")?.Value!)` gera 500 quando a claim falta~~ **Resolvido na Fase 4:** tenant lido de `tenant_id` (fallback para a claim legada `tenantId`); sem claim válida retorna 403 | `RiskModelController.cs:114` | concluído |
| RC-02 | P1 | Falhas de operação viram `Problem(... statusCode: 500)` genérico; not found e listas vazias retornam `204` | `EventTypeController.cs:26-117`; `RiskModelController.cs:35-124` | 5 |
| RC-03 | P1 | 13 integration tests falhando e a suíte não valida comportamento real: GETs usam client sem token; `CreateAuthenticatedClient()` cria uma `new RiskCatalogWebApplicationFactory()` sem `InitializeAsync` (containers parados); nada cria o schema (ver RC-06), por isso as asserções aceitam `500`; `GeospatialIntegrationTests` chama rotas inexistentes (`/api/riskmodel/regional-parameters`) e aceita `404` como sucesso | `EventTypeControllerIntegrationTest.cs:25-33,72,87,100`; `GeospatialIntegrationTests.cs:44,55` | 8 (depende de RC-06) |
| RC-04 | P2 | ~~Application referencia JwtBearer / IdentityModel e lança `BadHttpRequestException` (tipo HTTP)~~ **Resolvido na Fase 3:** pacotes JwtBearer/IdentityModel/Configuration/Http.Json removidos da Application (JwtBearer agora referenciado pelo Api); `BadHttpRequestException` trocada por `ArgumentException` (mesmo 400, ver RC-11) | `RiskCatalog.Application.csproj:10`; `Services/EventTypeService.cs:1,328` | concluído |
| RC-05 | P2 | ~~Api referencia EF Core / Npgsql diretamente~~ **Resolvido na Fase 3:** EF Core e Npgsql removidos do Api (não eram usados) | `RiskCatalog.Api.csproj` | concluído |
| RC-06 | P2 | `RiskCatalogDbContext` sem migrations EF versionadas; schema depende de script externo | `Infrastructure/Persistence` | 6 |
| RC-07 | P2 | Rotas `/api/event-types`, `/api/risk-model` (sem versão e sem nome do serviço) | controllers | 5 |
| RC-08 | P2 | Retry HTTP com 4 tentativas | `InfrastructureServiceCollectionExtension.cs:104-115` | 6 |
| RC-09 | P2 | Sem OTel e health checks; sink do Elasticsearch | `Program.cs` | 7 |
| RC-10 | P3 | 44 CS8618, 142 violações de formatação, xUnit1013 (2) | build / format | 8 |
| RC-11 | P3 | Efeito colateral do RC-04: para o erro de sobreposição de faixa de severidade, o campo `type` do corpo de erro 400 passou de `"BadHttpRequestException"` para `"ArgumentException"` (status inalterado). O campo expõe nome de tipo interno e será substituído por Problem Details (X-03) | `EventTypeService.cs` | 5 |
| RC-12 | P1 | ~~O risk-catalog chamava o geospatial sem nenhuma credencial (o geospatial exigia `X-Api-Key`), então essas chamadas falhavam com 401~~ **Resolvido na Fase 4:** risk-catalog usa token de serviço (`Geospatial.Read`) | `InfrastructureServiceCollectionExtension.cs` | concluído |

### 5.4 geospatial

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| GEO-01 | P1 | Controller usa `IGeospatialEventRepository` (Domain) direto e devolve `GeospatialOperationEvent` (evento de domínio) no contrato **Camadas resolvidas na Fase 3:** buscas via `ISearchGeospatialEventsUseCase`. Pendente (Fase 5): o contrato ainda devolve `GeospatialOperationEvent` | `GeospatialController.cs:5-6,21,39-67` | 5 |
| GEO-02 | P2 | ~~API key única compartilhada, comparação com `string.Equals` (não constante no tempo), sem contexto de tenant~~ **Resolvido na Fase 4:** API key removida; JWT com política `GeospatialRead` e tokens de serviço de ingestion, risk-catalog e risk-evaluation | `Attributes/ApiKeyAuthAttribute.cs:23` | concluído |
| GEO-03 | P2 | Respostas em objetos anônimos e `BadRequest("...")` em texto | `GeospatialController.cs:60,77,89,104,115` | 5 |
| GEO-04 | P2 | ~~Placeholder de chave em `appsettings.json` (`CHANGE_ME_...`), aceito no startup~~ **Resolvido na Fase 4:** `APIKey` removida dos `appsettings` | `Geospatial.Api/appsettings.json:65` | concluído |
| GEO-05 | P2 | Sem OTel, métricas e health checks | `Program.cs` | 7 |
| GEO-06 | P2 | Integration tests vazios | `tests/Geospatial.IntegrationTests` | 8 |
| GEO-07 | P3 | ~~NU1603 (Confluent.Kafka 2.7.0 inexistente)~~ resolvido fixando 2.8.0, a versão já resolvida; restam 9 CS8618 e 2 CS8602 | csproj / build | 8 |

O consumer (`SensorEventDetectedConsumer.cs`) está conforme: commit manual após processamento e envio para `geospatial-dead-letter`.

### 5.5 risk-evaluation

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| RE-01 | P0 | Consumer sem `EnableAutoCommit = false` (padrão `true`): offsets podem ser confirmados antes do processamento; sem DLQ e sem idempotência | `Infrastructure/Messaging/KafkaMessageConsumer.cs:24-30` | 6 |
| RE-02 | P0 | ~~API sem autenticação, incluindo `PUT model` que altera o modelo de risco~~ **Resolvido na Fase 4:** políticas de leitura/escrita e `PUT model` restrito a `RiskEvaluation.Admin` | `RiskEvaluationController.cs:112` | concluído |
| RE-03 | P1 | ~~Domain depende de MediatR (`INotification` em contratos)~~ **Resolvido na Fase 3:** `SensorEventDetected` e `RiskCatalogPublishedEvent` são contratos de mensagens consumidas e foram movidos para `RiskEvaluation.Application/Contracts`; Domain sem dependências | `RiskEvaluation.Domain.csproj:10`; `Domain/Contracts/*.cs` | concluído |
| RE-04 | P1 | Resposta de `UpdateRiskModel` inclui `ex.Message` | `Application/Handlers/UpdateRiskModelCommandHandler.cs:81` | 5 |
| RE-05 | P1 | Validação rejeita latitude/longitude `0` (coordenadas válidas); `history` recebe lat/lon como `int` | `RiskEvaluationController.cs:28-38,67-82` | 5 |
| RE-06 | P1 | ~~Unit tests 0/10 (mock de classe concreta)~~ **Corrigido:** os testes faziam `Setup` de método não virtual e de método `static`; passaram a usar o `RiskCalculationService` real com entradas derivadas dos pesos padrão (10/10) | `RiskEvaluationServiceTests.cs` | concluído |
| RE-07 | P2 | HttpClients para risk-catalog e geospatial sem timeout explícito (padrão 100 s > limite de 60 s) e sem retry; API key vazia aceita | `Infrastructure/ServiceCollectionExtension.cs:44-55` | 6 |
| RE-08 | P2 | ~~Placeholder `"sua-api-key-aqui"` versionado; 3 fallbacks `localhost`~~ **Resolvido na Fase 4 (chave):** `ApiKey` removida; os fallbacks `localhost` seguem pendentes em X-08 | `appsettings.json:74` | concluído |
| RE-09 | P2 | Sem OTel e health checks; integration tests vazios | `Program.cs`; `tests/RiskEvaluation.IntegrationTests` | 7 / 8 |
| RE-10 | P3 | 82 violações de formatação, 3 CS8618, enums sem sufixo | format / build | 8 |

### 5.6 alert-orchestrator

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| AO-00 | P0 | ~~Quando `Consume` retorna `null` após 5 s sem mensagens, o `ExecuteAsync` faz `return`: o consumer encerra definitivamente (sem `Close()`) e o serviço deixa de processar `risk-updated` e `region-intersected`~~ **Corrigido:** `continue` no poll vazio e `await Task.Yield()` antes do loop (sem ele, o `Consume` bloqueante rodaria dentro do `StartAsync` e travaria o host); testes de regressão em `UnitTests/Messaging/EventConsumerPollingTests.cs` | `Messaging/Consumers/RiskUpdatedEventConsumer.cs`; `RegionIntersectedEventConsumer.cs` | concluído |
| AO-01 | P0 | Em erro de processamento o offset não é commitado nem a mensagem vai para DLQ; o próximo commit bem-sucedido avança além dela (perda silenciosa) | `Messaging/Consumers/RiskUpdatedEventConsumer.cs:65-74`; idem `RegionIntersectedEventConsumer.cs` | 6 |
| AO-02 | P0 | ~~API sem autenticação; consultas por região/status retornam janelas de todos os tenants~~ **Resolvido na Fase 4:** política de leitura; consultas filtradas pelos tenants da identidade na Application; janela de outro tenant por id é tratada como inexistente | `AlertWindowsController.cs` | concluído |
| AO-03 | P1 | `AlertWindow` sem token de concorrência; quórum/escalonamento com várias instâncias pode sobrescrever atualizações | `AlertOrchestratorDbContext.cs` (sem `IsConcurrencyToken`/`xmin`) | 6 |
| AO-04 | P2 | Idempotência Redis em get-then-set não atômico | `RedisIdempotencyService.cs:20,31` | 6 |
| AO-05 | P2 | `GetById` retorna `204` para recurso inexistente; rotas sem versão; sem paginação | `AlertWindowsController.cs:23` | 5 |
| AO-06 | P2 | DbContext sem migrations EF versionadas | `Infrastructure/Persistence` | 6 |
| AO-07 | P2 | Tracing sem métricas; integration tests vazios | `Program.cs`; `tests/AlertOrchestrator.IntegrationTests` | 7 / 8 |
| AO-08 | P3 | Enums sem sufixo `Enum` (3); 6 violações de formatação | | 8 |
| AO-09 | P2 | Estágio de build do Dockerfile usa `sdk:10.0-preview` (SDK preview, diferente do `global.json` 9.0.115) | `services/alert-orchestrator/Dockerfile:7` | 7 |
| AO-10 | P3 | O filtro de tenant das consultas de janelas é aplicado em memória, depois da leitura do repositório; filtrar na consulta ao banco reduziria leitura desnecessária | `AlertWindowQueryService.cs` | 6 |

### 5.7 channels

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| CH-01 | P0 | Consumer com `EnableAutoCommit = true`: falha após o commit perde a notificação | `HostedServices/NotificationConsumerHostedService.cs:49` | 6 |
| CH-02 | P0 | ~~API sem autenticação; `POST api/dlq/reprocess` e `POST api/notifications` disparam envios reais (SMS/WhatsApp/e-mail)~~ **Resolvido na Fase 4:** políticas de leitura/escrita; envio, reprocessamento e consulta de tentativas conferem o tenant | `DeadLetterController.cs:19`; `NotificationsController.cs:33` | concluído |
| CH-03 | P1 | Controllers recebem `NotificationEvent` (Domain) e devolvem entidades/repositório de domínio **Camadas resolvidas na Fase 3:** `INotificationDispatchService` (entrega + DLQ, antes duplicada no controller e no consumer) e `IDeliveryAttemptQueryService`. Pendente (Fase 5): contrato ainda usa `NotificationEvent` e `DeliveryAttempt` | `NotificationsController.cs:2-3,29`; `DeadLetterController.cs:20` | 5 |
| CH-04 | P1 | Retry repete qualquer exceção (incl. não transitórias) em envio não idempotente (risco de SMS duplicado); `maxAttempts` é número de *retries* | `Application/Services/RetryPolicyEngine.cs:24-29` | 6 |
| CH-05 | P1 | Falha de entrega retorna `500` com o resultado interno no body | `NotificationsController.cs:42` | 5 |
| CH-06 | P2 | ~~Senha em connection string no `appsettings.json`~~ **Resolvido na Fase 4:** senha removida da connection string | `ChannelsService.Api/appsettings.json:23` | concluído |
| CH-07 | P2 | ~~Polly na camada Application~~ **Resolvido na Fase 3:** `RetryPolicyEngine` (Polly) movido para `ChannelsService.Infrastructure/Services`; registro duplicado na Application removido | `ChannelsService.Application.csproj:13` | concluído |
| CH-08 | P2 | Sem projeto de IntegrationTests; ~~diretório órfão `services/channels-service/`~~ (removido, nunca foi versionado); spec duplicada `services/channels/ChannelsService.Spec.md` × `docs/services/Channels-Service.md` | estrutura | 2 / 8 |
| CH-09 | P2 | Sem exception handler; sem sink Loki | `Program.cs`, `appsettings.json` | 5 / 7 |

### 5.8 reporting

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| REP-01 | P0 | ~~`tenantId` vem da rota sem autenticação: qualquer cliente lê summary/analytics/export de qualquer tenant~~ **Resolvido na Fase 4:** GETs exigem `ReportingRead` e tenant da rota presente nas claims | `ReportingController.cs:36-77` | concluído |
| REP-02 | P0 | ~~`POST events`, `events/envelope` e `events/replay` sem autenticação permitem injetar eventos e publicar no tópico de replay~~ **Resolvido na Fase 4:** `events` e `events/envelope` exigem `ReportingWrite` e conferem o tenant do evento; `events/replay` restrito a `Reporting.Admin` | `ReportingController.cs:86-144` | concluído |
| REP-03 | P1 | ~~Controller usa Infrastructure (`ReportingKafkaConsumerHostedService`), repositório de domínio e cria `ProducerBuilder` do Kafka por requisição~~ **Resolvido na Fase 3:** replay via porta `IReportingReplayPublisher` (Kafka em `Infrastructure/Messaging`), `POST events` via `ReportingEventIngestionService`; controller depende só de Application/Domain | `ReportingController.cs:1,8,19,110-119` | concluído |
| REP-04 | P1 | Contrato expõe entidades de domínio (`DashboardSummary`, `AnalyticsReport`, `ReportingEvent`) | `ReportingController.cs:37,44,87` | 5 |
| REP-05 | P2 | `GET health` recebe `tenantId` sem binding definido e duplica o papel de health check | `ReportingController.cs:79-84` | 5 / 7 |
| REP-06 | P2 | Sem exception handler, sem Scalar, sem Loki; 4 fallbacks `localhost` | `Program.cs` | 5 / 7 |
| REP-07 | P2 | `CLAUDE.md` referencia `docs/reporting-service-status.md`, que não existe | `CLAUDE.md` | 9 |

### 5.9 tenants-billing

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| TB-01 | P0 | ~~API sem autenticação: qualquer cliente cria tenants e planos e registra uso; outros serviços confiam nesse serviço para decidir se um tenant está ativo~~ **Resolvido na Fase 4:** políticas de leitura/escrita; consulta e registro de uso conferem o tenant; ingestion, alert-orchestrator e channels chamam com token de serviço (`TenantsBilling.Read`) | `TenantsBilling.Api/Program.cs:36-72` | concluído |
| TB-02 | P1 | ~~Projetos fora da `Sentinel.sln` e `TenantsBilling.sln` próprio~~ **Resolvido na Fase 2:** 6 projetos registrados na `Sentinel.sln` (pasta `services/tenants-billing`), `TenantsBilling.sln` removido; `dotnet sln list` cobre os 55 `.csproj` do repositório | `Sentinel.sln` | concluído |
| TB-03 | P1 | `GET /api/v1/tenants/{id}` devolve a entidade de domínio; endpoints acessam `ITenantRepository` direto **Camadas resolvidas na Fase 3:** `GET` via `TenantManagementService.GetTenantAsync`. Pendente (Fase 5): resposta ainda é a entidade (ver TB-09) | `Program.cs:50-54` | 5 |
| TB-04 | P2 | ~~Api referencia Confluent.Kafka e monta producer/publisher no `Program.cs`~~ **Resolvido na Fase 3:** producer/publisher Kafka registrados em `AddTenantsBillingInfrastructure`; Api sem Confluent.Kafka | `TenantsBilling.Api.csproj:10`; `Program.cs:18-22` | concluído |
| TB-05 | P2 | Rotas `/api/v1/tenants` e `/api/v1/plans` sem o nome do serviço. **Atenção:** ingestion, alert-orchestrator e channels consomem `GET /api/v1/tenants/{id}` | `TenantBillingGateway` em cada serviço | 5 |
| TB-06 | P2 | Sem Serilog, OTel, exception handler, Scalar e validação de entrada; `UseHttpsRedirection` ausente | `Program.cs` | 5 / 7 |
| TB-07 | P0 | Consumer de usage: em exceção não commita nem publica em DLQ e segue o loop, então o próximo commit avança além da mensagem (perda silenciosa, mesmo padrão de AO-01); payload inválido é commitado e descartado sem DLQ. O consumer de `tenant-usage-events-dlq` existe, mas não há produtor identificado | `TenantUsageEventConsumerHostedService.cs:67-72,96-99` | 6 |
| TB-08 | P2 | Apenas 1 integration test | `tests/TenantsBilling.IntegrationTests` | 8 |
| TB-09 | P1 | Contrato `GET /api/v1/tenants/{id}` quebrado entre serviços: `Status` é serializado como número (sem `JsonStringEnumConverter`), mas o `TenantBillingGateway` do ingestion o lê como `string` → `JsonException` capturada → **toda validação de tenant do ingestion retorna falso**. Os gateways de alert-orchestrator e channels só checam o HTTP 200 → **tenant suspenso/cancelado é aceito** | `TenantsBilling.Api/Program.cs`; `*/TenantBillingGateway.cs` | 5 |

### 5.10 audit-compliance

| ID | Sev. | Achado | Evidência | Fase |
|---|---|---|---|---|
| AUD-01 | P2 | 6 integration tests instáveis no run paralelo da solution (passam isolados) | baseline | 8 |
| AUD-02 | P3 | 2 warnings CS0618 (API obsoleta) | build | 8 |

## 6. Conflitos entre regras e decisões pendentes

| # | Conflito | Fontes | Recomendação |
|---|---|---|---|
| D-1 | Sufixo `Enum` obrigatório × proibido | constituição §5.1 × `coding-standarts.md` | Seguir a constituição (precedência declarada em §1) apenas para código novo; não renomear em massa os 9 enums existentes sem pedido explícito |
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
