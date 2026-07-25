# Status do Reporting Service

Este documento resume o que está implementado, parcialmente implementado e o que ainda falta no Reporting Service com base no arquivo [docs/services/Reporting.md](services/Reporting.md).

## ✅ O que já está implementado

- Estrutura base do microsserviço em API, Application, Domain e Infrastructure.
- Endpoints básicos para:
  - resumo do dashboard
  - analytics
  - exportação
  - health check
  - recebimento de eventos
- Modelos de domínio para:
  - eventos
  - envelope de eventos
  - filtro de relatório
  - resumo do dashboard
  - relatório analítico
- Serviço de consulta com filtros básicos.
- Consumidor Kafka inicial.

## ⚠️ O que está parcialmente implementado

### 1. Consumo de eventos
- Existe um consumidor Kafka, mas ele é bem simples.
- Falta tratamento robusto para:
  - retries
  - dead-letter queue
  - reprocessamento
  - idempotência confiável entre reinicializações

### 2. KPIs e agregações
- Existem algumas agregações básicas, mas ainda não cobrem bem o que a documentação descreve.
- Faltam indicadores como:
  - taxa de entrega por canal
  - taxa de falha das notificações
  - tempo médio de entrega
  - tempo médio de processamento
  - eventos processados por período
  - risco médio por região
  - risco máximo registrado

### 3. Exportação
- CSV e JSON existem.
- Porém ainda falta:
  - validação mais forte
  - tratamento de permissões por tenant
  - suporte a formatos futuros como XLSX e PDF

### 4. Health checks
- Há um endpoint simples de health.
- Ainda não há uma visão completa de readiness/liveness com dependências e métricas reais.

## ❌ O que falta ou está incorreto em relação à documentação

### 1. Persistência real
- O repositório atual é somente em memória.
- A documentação prevê uso de tecnologias como:
  - Elasticsearch
  - PostgreSQL
  - Redis

### 2. Observabilidade
- Ainda não há:
  - OpenTelemetry
  - métricas Prometheus
  - tracing distribuído
  - logs estruturados completos

### 3. Escalabilidade e consistência eventual
- A idempotência atual é muito limitada.
- Não há uma estratégia clara para múltiplas instâncias e reprocessamento seguro.

### 4. Arquitetura do Read Model
- O fluxo ainda está muito ligado a um modelo simples de recebimento de eventos.
- A doc sugere um pipeline mais alinhado com o padrão de consumo assíncrono e consolidação analítica.

### 5. Segurança e isolamento por tenant
- Não há controle claro de permissões e isolamento de dados por tenant nas consultas.

### 6. Normalização e auditoria
- Não há uma camada explícita para:
  - normalização de eventos
  - validação de esquema
  - rastreabilidade do processamento

## Resumo geral

O Reporting Service atual está em um estágio inicial de implementação, com a estrutura base e alguns fluxos básicos funcionando, mas ainda longe de atender de forma completa o que foi descrito na documentação.

## Prioridades recomendadas

1. Implementar persistência real.
2. Melhorar observabilidade e monitoramento.
3. Fortalecer idempotência e reprocessamento.
4. Expandir KPIs e consultas analíticas.
5. Definir estratégia de isolamento por tenant e segurança.
