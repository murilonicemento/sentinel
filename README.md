# Natural Events Risk & Alerting Platform

[![Status](https://img.shields.io/badge/status-in%20development-yellow)]()
[![Docs](https://img.shields.io/badge/docs-ready-blue)]()
[![Tech](https://img.shields.io/badge/tech-.NET%20%7C%20Docker%20%7C%20K8s-lightgrey)]()
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Coverage](https://img.shields.io/badge/coverage-85%25-blue)]()

Plataforma distribuída para **detecção, análise e alerta de eventos naturais**, com:

- Eventos: chuvas extremas, enchentes, deslizamentos, incêndios, sismos
- Arquitetura: **Microsserviços, CQRS, DDD, Clean Architecture**
- Processamento em tempo real de múltiplas fontes (APIs, IoT, satélites)
- Alertas multicanal: SMS, push, WhatsApp, IoT/sirene
- Suporte **multi-tenant** com trilha auditável

## Documentação

- [RFC de Arquitetura Técnica](./docs/RFC-Arquitetura.md)

## Estrutura do Repositório

```

sentinel/
├─ services/
│  ├─ api-gateway/...
│  ├─ ingestion/
│  │   ├─ src/{Domain,Application,Infrastructure.Read,Infrastructure.Write,Api}
│  │   └─ tests/{UnitTests,IntegrationTests}
│  ├─ risk-catalog/...
│  ├─ geospatial/...
│  ├─ risk-evaluation/...
│  ├─ alert-orchestrator/...
│  ├─ channels/...
│  ├─ reporting/...
│  ├─ tenants-billing/...
│  └─ compliance-audit/...
├─ platform/
|  |─ init-db/
│  ├─ k8s/
│  ├─ helm/
│  ├─ docker-compose.yml
│  └─ observability/{grafana,prometheus,loki,otel-collector}
├─ libs/
│  ├─ BuildingBlocks/{Messaging,Outbox,Observability}
│  └─ SharedKernel/
├─ docs/
│  ├─ RFC-Arquitetura.md
│  └─ diagrams/{architecture.mmd,classDiagram.mmd}
└─ README.md

```

## Como rodar localmente

### Pré-requisitos

- Docker
- Docker Compose

### Subindo a stack mínima

```bash
docker compose up -d
```

### Parando e removendo containers

```bash
docker compose down
```

### Serviços inclusos

- Kafka + Zookeeper
- PostgreSQL
- MongoDB
- Redis
- MinIO
- Elasticsearch + Kibana
- Grafana + Prometheus + Loki

## Roadmap Resumido

| Iteração | Funcionalidades                                                                                                                                                                       |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1        | Serviços iniciais (ingestion, risk-catalog, geospatial, risk-scoring, alert-orchestrator, tenancy-governance, audit-conformity, observability); CQRS + Event Sourcing; Docker Compose |
| 2        | Geoprocessamento (PostGIS/Elasticsearch), Outbox/CDC, Dashboards e mapas de calor                                                                                                     |
| 3        | Kubernetes (HPA, KEDA), observabilidade OTEL, Multi-tenant e RBAC                                                                                                                     |
| 4        | Machine Learning no risk-scoring, Integração IoT (sirenes) e WhatsApp Business API                                                                                                    |

## Testes

- Unitários: regras de domínio
- Contract Tests: Pact
- Integração: Testcontainers
- E2E em Kubernetes (kind/minikube)

## Diferenciais

- Reprodutibilidade via event sourcing e auditoria legal
- Geoprocessamento real com PostGIS e Elasticsearch
- Escalonamento orientado a eventos com KEDA
- Multi-tenant completo com limites e billing
- Integração IoT com sirenes físicas
