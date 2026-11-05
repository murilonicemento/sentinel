# RFC - Arquitetura do Projeto **Sentinel** (Atualizado)

> **Revisão de 2026-11-04.** O rascunho de 2025-08-24 descrevia a arquitetura pretendida (análise de logs, RabbitMQ ou
> Kafka, gRPC, ELK, serviços de Query/Analytics). Esta revisão alinha o texto ao que está implementado e aponta os ADRs
> que registram cada decisão. As regras obrigatórias continuam em `docs/constitution.md`.

## 1. Contexto

O projeto **Sentinel** é uma plataforma distribuída e multi-tenant de **detecção, avaliação de risco e alerta de
eventos naturais** (chuvas extremas, enchentes, deslizamentos, incêndios e terremotos). Ela recebe leituras de
sensores e de fontes externas, calcula o risco por região, abre janelas de alerta e notifica por vários canais, com
trilha de auditoria. Inspirado em **DDD, CQRS, Clean Architecture e Microsserviços**, o sistema foi projetado para:

- Suportar grandes volumes de leituras.
- Manter separação clara de responsabilidades entre contextos.
- Facilitar evolução contínua e manutenção modular.

## 2. Objetivos

1. Capturar leituras de sensores e de fontes externas de forma confiável, com deduplicação e idempotência.
2. Avaliar o risco por região a partir de um catálogo de riscos versionado.
3. Orquestrar alertas com quorum, cooldown, escalonamento e expiração.
4. Entregar notificações por vários canais com retry, fallback e dead-letter.
5. Garantir resiliência por mensageria assíncrona e consumo confiável (ADR-0003).
6. Registrar uso por tenant, relatórios e auditoria a partir dos eventos da plataforma.
7. Suportar evolução contínua com arquitetura modular e contratos versionados (ADR-0007).

## 3. Visão Geral da Arquitetura

A arquitetura é baseada em **microsserviços**, cada um responsável por um **bounded context**, com banco de dados
próprio e sem código compartilhado. O diagrama está em `docs/diagrams/arch.mmd`.

- Comunicação: **eventos no Kafka** entre os serviços e **APIs HTTP síncronas** (`/api/v1/<serviço>`) quando é
  preciso resposta imediata, com token de serviço (ADR-0002).
- Entrada: **api-gateway** (Ocelot) com rate limit por IP de origem.
- Cache e coordenação: **Redis** para cache, deduplicação, idempotência e cooldown.
- Indexação e busca: **Elasticsearch** (geospatial e reporting).
- Persistência: **PostgreSQL** para dados relacionais, **MongoDB** para documentos e **MinIO** para payloads brutos.

## 4. Domínios e Subdomínios

- **Domínio Principal**: Monitoramento e Alertas de Eventos Naturais

**Subdomínios e Bounded Contexts** (com o serviço que os implementa):

1. **Detecção e Ingestão** (`ingestion`)
   - Ingestão por HTTP e polling de fontes externas (USGS, NASA FIRMS, NOAA CDO, Stormglass; ADR-0005)
   - Normalização, validação e deduplicação de dados

2. **Catálogo de Riscos** (`risk-catalog`)
   - Tipos de Evento e Severidade
   - Matrizes de Risco, Curvas IDF e parâmetros regionais (`regionCode`)

3. **Geoespacial** (`geospatial`)
   - Operações geométricas e busca de eventos por raio ou polígono
   - Zonas de Risco, Rotas de Evacuação e Dispositivos de Alerta: previstos, ainda não implementados

4. **Avaliação e Score de Risco** (`risk-evaluation`)
   - Cálculo de risco com a cópia local do catálogo publicado
   - Modelos de Predição: previstos, ainda não implementados

5. **Orquestração de Alertas** (`alert-orchestrator`)
   - Regras de Disparo
   - Escalonamento e Quorum
   - Integração com **Channels** para envio multi-canal e com **Reporting** para dashboards e KPIs

6. **Entrega de Notificações** (`channels`)
   - SMS e WhatsApp (Twilio), e-mail (SendGrid), push, MQTT e sirenes

7. **Relatórios** (`reporting`)
   - Dashboards, análises e exportações a partir dos alertas e entregas

8. **Tenancy e Governança** (`tenants-billing`)
   - Gestão de Tenants e Planos
   - Limites e uso mensal (ADR-0009)

9. **Auditoria e Conformidade** (`audit-compliance`)
   - Trilha de Auditoria imutável, consultas e exportações
   - Retenção e Conformidade: política de retenção ainda pendente (ADR-0001)

10. **Observabilidade Operacional** (plataforma, sem serviço próprio)
    - Telemetria, dashboards e alertas (ADR-0004, ADR-0006)
    - Saúde das Integrações (health checks `/health/live` e `/health/ready`)

> Observação: cada bounded context é responsável por suas regras de negócio, armazenamento e eventos específicos,
> garantindo desacoplamento e escalabilidade.

## 5. Comunicação

- **Event-driven (Kafka)**: tópicos criados pelo `kafka-init` de `platform/docker-compose.yml`; consumo com commit
  só após o tratamento, retentativas limitadas e dead-letter por serviço (ADR-0003); contexto W3C nos headers.
- **HTTP**: chamadas síncronas entre serviços com token de serviço (ingestion e risk-catalog → geospatial;
  risk-evaluation → risk-catalog; ingestion, alert-orchestrator e channels → tenants-billing) e acesso externo pelo
  api-gateway.
- **Cache (Redis)**: scores recentes, configurações de tenant, cache do catálogo, claims de idempotência.

**Fluxo de Alertas** (ADR-0008; `docs/diagrams/sequence-diagram.mmd`):

1. Ingestion publica `SensorEventDetected` em `disaster-event-detected` ou `climatic-event-detected` pelo outbox.
2. Geospatial indexa cada evento; Risk Evaluation calcula o score com o snapshot local do catálogo.
3. Risk Evaluation publica `RiskUpdatedEvent` em `risk-updated` para eventos com tenant dentro de uma região do catálogo.
4. Alert Orchestrator avalia regras, quorum e escalonamento e publica `TriggerAlertCommand` em `alert-commands`.
5. **Channels** entrega os alertas multi-canal (SMS, WhatsApp, e-mail, push, MQTT, sirenes).
6. **Reporting** consolida alertas e entregas a partir de `reporting-events` (ADR-0010).
7. **Tenants Billing** contabiliza o uso a partir de `tenant-usage-events` (ADR-0009).
8. **Audit Compliance** registra os eventos auditáveis de `audit-events`; hoje só o tenants-billing publica (ADR-0011).

> Observação: Channels e Reporting são essenciais para garantir entrega confiável e rastreabilidade de alertas.

## 6. Tecnologias

- **Backend**: ASP.NET Core (C#/.NET 9, SDK fixado em `global.json`)
- **Mensageria**: Kafka (Confluent.Kafka)
- **Gateway**: Ocelot
- **Cache**: Redis
- **Indexação & Busca**: Elasticsearch 8 (NEST no geospatial, Elastic.Clients.Elasticsearch no reporting)
- **Banco Relacional**: PostgreSQL 15 (EF Core; Dapper no ingestion)
- **Banco NoSQL**: MongoDB 7
- **Object storage**: MinIO
- **Containerização**: Docker + Docker Compose
- **CI**: GitHub Actions (`.github/workflows/ci.yml`)
- Futuro: Kubernetes para orquestração

## 7. Padrões Arquiteturais

- **DDD (Domain-Driven Design)**
- **CQRS (Command Query Responsibility Segregation)**: completo no ingestion (escrita PostgreSQL + outbox, leitura
  MongoDB); MediatR também em risk-evaluation e alert-orchestrator
- **Clean Architecture**
- **Event-Driven Architecture** com Transactional Outbox no ingestion
- **SOLID Principles**

## 8. Fluxo de Alto Nível

1. Sensor ou fonte externa envia uma leitura → **Detecção e Ingestão**.
2. Evento normalizado → **Geoespacial** (indexação) e **Avaliação e Score de Risco**, que usa o **Catálogo de Riscos**
   publicado.
3. Risco atualizado → **Orquestração de Alertas**.
4. **Channels** envia notificações multi-canal.
5. **Reporting** consolida alertas e entregas para dashboards.
6. **Tenancy**, **Auditoria** e **Observabilidade** recebem uso, eventos auditáveis, logs, métricas e traces em
   paralelo.

## 9. Observabilidade

Conforme o ADR-0004 e o ADR-0006:

- **Logging centralizado**: Serilog → Loki.
- **Health Checks**: `/health/live`, `/health/ready` e `/health` em cada serviço.
- **Tracing distribuído**: OpenTelemetry via OTLP → Tempo, com contexto W3C também nas mensagens Kafka.
- **Metrics & Dashboards**: Prometheus (`/metrics`) e Grafana, com dashboards e alertas provisionados por arquivo.

## 10. Próximos Passos

Concluídos desde o rascunho: contratos de eventos (ADR-0008 a ADR-0012), CI com build, formatação e testes,
retry e DLQ no Kafka (ADR-0003), monitoramento e alertas (ADR-0006).

1. Deploy em Kubernetes (Helm, autoescala).
2. Bibliotecas compartilhadas para o código hoje copiado entre serviços.
3. Transactional Outbox nos demais produtores de eventos.
4. Provedor de identidade definitivo (ADR-0002).
5. Remoção dos tópicos depreciados após 2027-01-31 (ADR-0012).
6. Política de retenção da auditoria (ADR-0001).

---

**Autor**: Murilo
**Data**: 2025-08-24 (revisado em 2026-11-04)
**Status**: Draft
