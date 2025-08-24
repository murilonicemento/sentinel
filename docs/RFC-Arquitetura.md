# RFC - Arquitetura do Projeto **Sentinel**

## 1. Contexto

O projeto **Sentinel** tem como objetivo fornecer uma solução distribuída e escalável para **análise de logs em tempo real**, com foco em resiliência, observabilidade e flexibilidade de consulta. Inspirado em princípios de **DDD, CQRS, Clean Architecture e Microsserviços**, o sistema foi projetado para suportar grandes volumes de dados, manter separação clara de responsabilidades e facilitar evolução contínua.

## 2. Objetivos

1. Capturar logs de diversas fontes de forma confiável e escalável.
2. Oferecer busca rápida e eficiente sobre os logs.
3. Permitir análises agregadas em tempo real.
4. Garantir resiliência por meio de mensageria e processamento assíncrono.
5. Suportar evolução contínua via arquitetura modular.

## 3. Visão Geral da Arquitetura

A arquitetura é baseada em microsserviços, cada um responsável por um **bounded context**. Comunicação é feita via **eventos em RabbitMQ** e APIs síncronas quando necessário. O sistema conta ainda com suporte de **Redis** para caching, **Elasticsearch** para indexação e busca, e **MongoDB/PostgreSQL** para persistência.

## 4. Domínios e Subdomínios

- **Core Domain**: Gestão e análise de logs.
- **Supporting Domains**:

  - Autenticação e Autorização (Identity)
  - Indexação e Busca
  - Processamento e Agregação

- **Generic Domains**:

  - Observabilidade
  - Monitoramento de Saúde

## 5. Bounded Contexts

1. **Ingestion Service**: Captura e normaliza logs.
2. **Processing Service**: Realiza transformações, enriquecimento e roteamento.
3. **Indexing Service**: Indexa no Elasticsearch.
4. **Query Service**: Expõe API para consultas rápidas.
5. **Analytics Service**: Executa análises agregadas (dashboards/relatórios).
6. **Identity Service**: Gerencia usuários, permissões e autenticação JWT.

## 6. Comunicação

- **Event-driven** (RabbitMQ): entre ingestão, processamento e indexação.
- **HTTP/gRPC**: entre Query Service, Analytics e clientes externos.
- **Cache (Redis)**: otimização de consultas repetidas.

## 7. Tecnologias

- **Backend**: ASP.NET Core (C#/.NET)
- **Mensageria**: RabbitMQ
- **Cache**: Redis
- **Indexação & Busca**: Elasticsearch (NEST client)
- **Banco Relacional**: PostgreSQL
- **Banco NoSQL**: MongoDB
- **Containerização**: Docker + Docker Compose (futuro: Kubernetes)

## 8. Padrões Arquiteturais

- **DDD (Domain-Driven Design)**
- **CQRS (Command Query Responsibility Segregation)**
- **Clean Architecture**
- **Event-Driven Architecture**
- **SOLID Principles**

## 9. Fluxo de Alto Nível

1. Fonte externa envia log → **Ingestion Service**
2. Log validado/enriquecido → evento no **RabbitMQ**
3. **Processing Service** aplica transformações e publica novos eventos
4. **Indexing Service** consome e envia para Elasticsearch
5. **Query Service** expõe APIs para consultas rápidas
6. **Analytics Service** executa agregações e análises em tempo real

## 10. Observabilidade

- **Logging centralizado**: próprio Sentinel + ELK Stack
- **Health Checks**: endpoints de saúde
- **Tracing distribuído**: OpenTelemetry

---

**Autor**: Murilo
**Data**: 2025-08-24
**Status**: Draft
