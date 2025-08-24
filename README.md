# 🌍 Natural Events Risk & Alerting Platform

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Docker](https://img.shields.io/badge/docker-passing-blue)]()
[![Kubernetes](https://img.shields.io/badge/k8s-passing-lightblue)]()

Plataforma distribuída para **detecção, análise e alerta de eventos naturais** (chuvas extremas, enchentes, deslizamentos, incêndios, sismos), com arquitetura baseada em **microsserviços, CQRS, DDD e Clean Architecture**.

O sistema processa dados em tempo real de múltiplas fontes (APIs, IoT, satélites), calcula risco por região e dispara alertas multicanal (SMS, push, WhatsApp, IoT/sirenes), com suporte **multi-tenant e trilha auditável**.

## 📑 Documentação

- [RFC de Arquitetura Técnica](./docs/RFC-Arquitetura.md)

## 📂 Estrutura do Repositório

```
sentinel/
│
├─ services/
│   ├─ ingestion/
│   │   ├─ src/
│   │   │   ├─ Ingestion.Domain/
│   │   │   ├─ Ingestion.Application/
│   │   │   ├─ Ingestion.Infrastructure/
│   │   │   └─ Ingestion.Api/
│   │   ├─ tests/
│   │   │   ├─ Ingestion.UnitTests/
│   │   │   └─ Ingestion.IntegrationTests/
│   │   └─ Dockerfile
│   │
│   ├─ geospatial/
│   │   ├─ src/
│   │   │   ├─ Geospatial.Domain/
│   │   │   ├─ Geospatial.Application/
│   │   │   ├─ Geospatial.Infrastructure/
│   │   │   └─ Geospatial.Api/
│   │   ├─ tests/
│   │   └─ Dockerfile
│   │
│   ├─ risk-scoring/
│   │   ├─ src/
│   │   │   ├─ RiskScoring.Domain/
│   │   │   ├─ RiskScoring.Application/
│   │   │   ├─ RiskScoring.Infrastructure/
│   │   │   └─ RiskScoring.Api/
│   │   ├─ tests/
│   │   └─ Dockerfile
│   │
│   ├─ alert-orchestrator/
│   │   └─ ...
│   │
│   ├─ channels/
│   │   └─ ...
│   │
│   ├─ reporting/
│   │   └─ ...
│   │
│   ├─ tenants-billing/
│   │   └─ ...
│   │
│   └─ compliance-audit/
│       └─ ...
│
├─ platform/
│   ├─ docker-compose.yml
│   ├─ k8s/
│   │   ├─ ingress.yaml
│   │   ├─ deployments/
│   │   ├─ services/
│   │   └─ hpa/
│   ├─ helm/
│   └─ observability/
│       ├─ grafana/
│       ├─ prometheus/
│       ├─ loki/
│       └─ otel-collector/
│
├─ libs/
│   ├─ BuildingBlocks/
│   │   ├─ Messaging/
│   │   ├─ Outbox/
│   │   └─ Observability/
│   └─ SharedKernel/
│
├─ docs/
│   ├─ RFC-Arquitetura.md
│   └─ diagrams/
│       ├─ architecture.mmd
│       └─ classDiagram.mmd
│
└─ README.md
```

## ▶️ Como rodar localmente

### Pré-requisitos

- [Docker](https://www.docker.com/)
- [Docker Compose](https://docs.docker.com/compose/)

### Subindo a stack mínima

```bash
docker compose up -d
```

### Serviços inclusos

- Kafka + Zookeeper
- PostgreSQL
- Redis
- MinIO
- Elasticsearch + Kibana
- Grafana + Prometheus + Loki

## 🚀 Roadmap Resumido

### Iteração 1

- Serviços iniciais: ingestion, geospatial, risk-scoring, alert-orchestrator, channels, reporting
- CQRS mínimo (3 commands, 4 events, 3 queries)
- Docker Compose com stack central

### Iteração 2

- Geoprocessamento (PostGIS/Elasticsearch)
- Outbox/CDC
- Dashboards e mapas de calor

### Iteração 3

- Kubernetes (HPA, KEDA, observabilidade com OTEL)
- Multi-tenant e RBAC

### Iteração 4

- Machine Learning no risk-scoring
- Integração IoT (sirenes) e WhatsApp Business API

## 🧪 Testes

- Unitários (regras de domínio)
- Contract Tests (Pact)
- Integração (Testcontainers)
- E2E em Kubernetes (kind/minikube)

## 📊 KPIs

- Latência ingestão → alerta (P50/P95)
- Taxa de confirmação de alertas
- Precisão de risco vs ocorrências reais
- Erros por canal de entrega

## ✨ Diferenciais

- Reprodutibilidade via event sourcing + auditoria legal
- Geoprocessamento real com PostGIS/Elasticsearch
- Escalonamento orientado a eventos com KEDA
- Multi-tenant completo com limites e billing
- Integração IoT (sirenes físicas)
