# RFC - Arquitetura do Projeto **Sentinel**

## 1. Contexto

O projeto **Sentinel** fornece uma solução distribuída e escalável para **análise de logs em tempo real**, com foco em **resiliência, observabilidade e flexibilidade de consulta**. Inspirado em **DDD, CQRS, Clean Architecture e Microsserviços**, o sistema foi projetado para:

- Suportar grandes volumes de dados.
- Manter separação clara de responsabilidades.
- Facilitar evolução contínua e manutenção modular.

## 2. Objetivos

1. Capturar logs e eventos de diversas fontes de forma confiável e escalável.
2. Oferecer busca rápida e eficiente sobre os dados coletados.
3. Permitir análises agregadas e preditivas em tempo real.
4. Garantir resiliência via mensageria e processamento assíncrono.
5. Suportar evolução contínua com arquitetura modular.

## 3. Visão Geral da Arquitetura

A arquitetura é baseada em **microsserviços**, cada um responsável por um **bounded context**.

- Comunicação: **eventos via RabbitMQ** e **APIs síncronas** quando necessário.
- Cache: **Redis** para consultas frequentes.
- Indexação e busca: **Elasticsearch**.
- Persistência: **MongoDB** para dados semi-estruturados e **PostgreSQL** para dados relacionais.

> Observação: considerar Kubernetes futuramente para orquestração e escalabilidade automática.

## 4. Domínios e Subdomínios

- **Domínio Principal**: Monitoramento e Alertas de Eventos Naturais

**Subdomínios e Bounded Contexts**:

1. **Detecção e Ingestão**

   - Ingestão de fontes externas
   - Normalização e validação de dados

2. **Catálogo de Riscos**

   - Tipos de Evento e Severidade
   - Matrizes de Risco e Curvas IDF

3. **Geoespacial e Infraestrutura Local**

   - Zonas de Risco e Rotas de Evacuação
   - Dispositivos de Alerta

4. **Avaliação e Score de Risco**

   - Cálculo de Risco
   - Modelos de Predição

5. **Orquestração de Alertas**

   - Regras de Disparo
   - Escalonamento e Quorum

6. **Tenancy e Governança**

   - Gestão de Tenants e Planos
   - Limites e Billing

7. **Auditoria e Conformidade**

   - Trilha de Auditoria
   - Retenção e Conformidade

8. **Observabilidade Operacional**

   - Telemetria e SLIs/SLOs
   - Saúde das Integrações

> Observação: cada bounded context é responsável por suas regras de negócio, armazenamento e eventos específicos, garantindo desacoplamento e escalabilidade.

## 5. Comunicação

- **Event-driven (RabbitMQ)** – entre ingestão, processamento, indexação e orquestração de alertas.
- **HTTP/gRPC** – entre Query/Analytics Services e clientes externos.
- **Cache (Redis)** – otimização de consultas repetidas.

> Observação: definir claramente tópicos/exchanges e estratégias de retry/DLQ.

## 6. Tecnologias

- **Backend**: ASP.NET Core (C#/.NET)
- **Mensageria**: Kafka
- **Cache**: Redis
- **Indexação & Busca**: Elasticsearch (NEST client)
- **Banco Relacional**: PostgreSQL
- **Banco NoSQL**: MongoDB
- **Containerização**: Docker + Docker Compose
- Futuro: Kubernetes para orquestração

## 7. Padrões Arquiteturais

- **DDD (Domain-Driven Design)**
- **CQRS (Command Query Responsibility Segregation)**
- **Clean Architecture**
- **Event-Driven Architecture**
- **SOLID Principles**

## 8. Fluxo de Alto Nível

1. Fonte externa envia log/evento → **Detecção e Ingestão**.
2. Dados normalizados → **Catálogo de Riscos** e **Avaliação e Score de Risco**.
3. Eventos processados → **Orquestração de Alertas** e envio para **Dispositivos de Alerta**.
4. **Observabilidade e Auditoria** coletam métricas, logs e trilhas de auditoria em paralelo.
5. Dados agregados → **Query/Analytics Services** para dashboards e relatórios.

> Observação: considerar event sourcing para auditoria e replay de eventos críticos.

## 9. Observabilidade

- **Logging centralizado** – Sentinel + ELK Stack.
- **Health Checks** – endpoints de saúde dos serviços.
- **Tracing distribuído** – OpenTelemetry.
- **Metrics & Dashboards** – Prometheus/Grafana.

## 10. Próximos Passos

1. Definir **contracts de eventos** para cada bounded context.
2. Configurar pipelines de CI/CD para microsserviços.
3. Implementar **strategies de retry e DLQ** para RabbitMQ.
4. Criar monitoramento e alertas proativos.

---

**Autor**: Murilo
**Data**: 2025-08-24
**Status**: Draft
