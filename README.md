# Sentinel — Plataforma de Risco e Alerta de Eventos Naturais

[![Status](https://img.shields.io/badge/status-em%20desenvolvimento-yellow)]()
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)]()
[![License](https://img.shields.io/badge/license-MIT-blue)](./LICENSE)

Plataforma distribuída para **detectar, avaliar e alertar** sobre eventos naturais: chuvas extremas, enchentes, deslizamentos, incêndios e terremotos. Ela recebe leituras de sensores e de fontes externas, calcula o risco por região, abre janelas de alerta e notifica por vários canais. É multi-tenant, com trilha de auditoria.

- **Arquitetura:** microsserviços, um por contexto delimitado, com DDD, Clean Architecture, CQRS onde se aplica e integração por eventos no Kafka.
- **Stack:** .NET 9, PostgreSQL, MongoDB, Redis, Kafka, Elasticsearch, MinIO, e Grafana, Prometheus, Loki e Tempo para observabilidade.

## Serviços

Cada serviço expõe seu contrato em `/api/v1/<serviço>`, com autenticação JWT. A porta é a publicada pelo `platform/docker-compose.yml`.

| Serviço | Porta | Responsabilidade |
|---|---|---|
| `api-gateway` | 7000 | Roteia `/gateway/v1/<serviço>` para cada API (Ocelot), com rate limit por IP |
| `ingestion` | 7001 | Recebe coletas de sensores por HTTP e consulta fontes externas (USGS, NASA FIRMS, NOAA CDO, Stormglass); publica os eventos detectados pelo outbox transacional |
| `risk-catalog` | 7002 | Tipos de evento, severidades, curvas IDF, matrizes de risco e parâmetros regionais; publica cada versão do catálogo |
| `geospatial` | 7003 | Operações geométricas (NetTopologySuite) e busca de eventos por raio ou polígono (Elasticsearch) |
| `risk-evaluation` | 7004 | Calcula o risco de cada evento com a cópia local do catálogo publicado e o envia ao alert-orchestrator |
| `alert-orchestrator` | 7005 | Janelas de alerta: quórum de sinais, cooldown, escalonamento e expiração |
| `tenants-billing` | 7006 | Tenants, planos, status do tenant e uso mensal contra os limites do plano |
| `audit-compliance` | 7007 | Registros de auditoria imutáveis, consultas e exportação em CSV ou JSON |
| `channels` | 7008 | Entrega de notificações (SMS e WhatsApp via Twilio, e-mail via SendGrid, push, MQTT, sirene) com retry, fallback e DLQ |
| `reporting` | 7009 | Dashboards, análises e exportações a partir dos alertas e entregas |

O fluxo principal é **sensor → avaliação de risco → janela de alerta → notificação**. Uso, relatórios e auditoria são alimentados por eventos (ADRs 0008 a 0012).

## Pré-requisitos

- Docker e Docker Compose.
- .NET SDK **9.0.115**, fixado em `global.json`. É necessário só para compilar e testar fora dos containers.

## Como rodar localmente

1. Copie o arquivo de ambiente e preencha as chaves:

   ```bash
   cp platform/.env.example platform/.env
   ```

   Os itens obrigatórios são `JWT_SIGNING_KEY` e as chaves `*_SERVICE_TOKEN_SIGNING_KEY`, uma por serviço, com pelo menos 32 caracteres e todas diferentes. As fontes externas de polling vêm desligadas (`POLLING_<FONTE>_ENABLED`) e os provedores de notificação (SendGrid, Twilio) são opcionais. Nunca coloque chaves em `appsettings*.json`.

2. Suba a plataforma:

   ```bash
   docker compose -f platform/docker-compose.yml up -d
   ```

   O container `kafka-init` cria os tópicos. Os scripts de `platform/init-db` criam os bancos e os esquemas. As migrations não rodam na subida dos serviços.

3. Para parar:

   ```bash
   docker compose -f platform/docker-compose.yml down
   ```

### Acessos locais

| O quê | Endereço |
|---|---|
| APIs | `http://localhost:7001` a `7009`, ou pelo gateway em `http://localhost:7000/gateway/v1/<serviço>` |
| Health checks | `/health/live` e `/health/ready` em cada serviço, sem autenticação |
| Documentação da API (Scalar) | `/scalar` em cada serviço, em ambiente `Development` |
| Grafana | `http://localhost:3000` (admin / admin), com dashboards e alertas já provisionados |
| Prometheus | `http://localhost:9090` |
| Kafka UI | `http://localhost:8080` |
| Kibana | `http://localhost:5601` |
| MinIO | `http://localhost:9001` |

### Autenticação

As APIs aceitam tokens JWT HS256 assinados com `JWT_SIGNING_KEY`, com o emissor e o público de `JWT_ISSUER` / `JWT_AUDIENCE`. Os papéis ficam na claim `role` (`<Serviço>.Admin`, `.Read`, `.Write`), e os tenants na claim `tenant_id`, que pode ter vários valores. O repositório não inclui um provedor de identidade: para testar localmente, gere um token com essas claims e essa chave. O modelo completo está no [ADR-0002](./docs/adr/ADR-0002-service-authentication.md).

## Build e testes

O repositório tem uma única solução, `Sentinel.sln`, com todos os projetos.

```bash
dotnet build Sentinel.sln
dotnet test Sentinel.sln

# um projeto ou um teste
dotnet test services/geospatial/tests/Geospatial.UnitTests
dotnet test services/geospatial/tests/Geospatial.UnitTests --filter "FullyQualifiedName~GeoPointTests"
```

- **Unitários:** xUnit e Moq.
- **Integração:** `WebApplicationFactory` e Testcontainers (PostgreSQL, MongoDB, Redis, Kafka, MinIO), portanto precisam do Docker.
- **Contrato entre serviços:** o mesmo exemplo de JSON é testado no produtor e no consumidor.

As versões de pacotes NuGet ficam só em `Directory.Packages.props`.

O CI (`.github/workflows/ci.yml`) roda em pushes e pull requests para `develop` e `master`: restore, `dotnet build -c Release -warnaserror`, `dotnet format whitespace --verify-no-changes` e `dotnet test` com resultados TRX e cobertura Cobertura publicados no artifact `test-results`.

## Estrutura do repositório

```text
sentinel/
├─ services/<serviço>/
│  ├─ src/<Serviço>.{Api,Application,Domain,Infrastructure}
│  ├─ tests/<Serviço>.{UnitTests,IntegrationTests}
│  └─ Dockerfile
├─ platform/
│  ├─ docker-compose.yml, .env.example
│  ├─ config/                 # assinaturas de polling do ingestion
│  ├─ init-db/{postgres,mongodb}
│  └─ observability/{grafana,prometheus,tempo}
├─ docs/
│  ├─ constitution.md         # regras arquiteturais obrigatórias
│  ├─ RFC-Arquitetura.md
│  ├─ services/               # especificação e estado de cada serviço
│  ├─ adr/                    # decisões de arquitetura
│  ├─ compliance/             # relatório de validação dos serviços
│  ├─ claude-code/            # hooks, agents e skills do Claude Code
│  └─ diagrams/               # diagramas Mermaid
├─ Directory.Packages.props, global.json
└─ Sentinel.sln
```

O `ingestion` é a exceção do CQRS completo: tem `Infrastructure.Write` (PostgreSQL, outbox, Kafka, MinIO, Redis) e `Infrastructure.Read` (MongoDB). Os serviços não compartilham código nem bancos de dados.

## Documentação

- [Constituição do projeto](./docs/constitution.md): regras arquiteturais obrigatórias.
- [RFC de arquitetura](./docs/RFC-Arquitetura.md).
- [Especificações dos serviços](./docs/services/): cada uma descreve o estado da implementação verificado no código (seção "Estado da implementação"; em channels, as próprias seções da especificação; em geospatial, a seção "Status").
- [ADRs](./docs/adr/): audit-compliance, autenticação, consumo Kafka, observabilidade, polling, alertas, versionamento de API e contratos do pipeline (uso, relatórios e auditoria).
- [Relatório de validação](./docs/compliance/services-validation.md): achados e decisões por serviço.
- [CLAUDE.md](./CLAUDE.md): guia técnico condensado do repositório.
- [Claude Code](./docs/claude-code/README.md): hooks, subagents e skills que aplicam a constituição no desenvolvimento assistido.

## Roadmap

| Situação | Itens |
|---|---|
| Feito | Os 10 serviços com contrato `/api/v1`, autenticação JWT e isolamento por tenant; consumo Kafka confiável com DLQ; outbox no ingestion; polling de fontes externas; pipeline sensor → risco → alerta → notificação; medição de uso, relatórios e auditoria por eventos; observabilidade (logs, métricas, traces, dashboards, alertas); CI com build, formatação, testes e cobertura |
| Planejado | Deploy em Kubernetes (Helm, autoescala); bibliotecas compartilhadas (hoje cada serviço tem sua cópia do código comum); outbox nos demais produtores; provedor de identidade; remoção dos tópicos depreciados após 2027-01-31 (ADR-0012) |

## Licença

[MIT](./LICENSE)
