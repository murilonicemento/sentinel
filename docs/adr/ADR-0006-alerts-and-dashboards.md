# ADR-0006: Alertas e dashboards da plataforma

- **Status:** Proposto
- **Data:** 2026-10-29
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `ADR-0004` (observabilidade), `ADR-0003` (consumo Kafka), `ADR-0005` (polling), X-21

## Contexto

A plataforma coletava logs, traces e métricas (ADR-0004), mas não tinha nenhum alerta e só tinha um dashboard, o do channels. Esse dashboard apontava para um uid de datasource que deixou de existir na Fase 7. Também faltavam sinais para alertar:

- Os consumidores Kafka não expunham resultado nem retentativas, com exceção do audit-compliance.
- O meter do alert-orchestrator não era exportado.
- Não havia medida do backlog do outbox.

## Decisões

### 1. Grafana Alerting provisionado em arquivo

As regras ficam em `platform/observability/grafana/provisioning/alerting/sentinel-rules.yml`, na pasta Sentinel, com avaliação a cada minuto:

| Regra | Condição inicial | Severidade |
|---|---|---|
| Serviço fora do ar | `up == 0` por 2 min (sem dados também alerta) | critical |
| Taxa de erro 5xx alta | > 5% das respostas de API por 10 min | warning |
| Latência p95 alta | p95 > 2 s por 10 min | warning |
| Mensagens enviadas ao DLQ | qualquer `dead_lettered` em 10 min, por serviço e tópico | warning |
| Consumidor pausado | `not_durable` por 10 min | critical |
| audit-compliance: DLQ | qualquer evento no `audit-events-dlq` em 10 min | warning |
| Outbox acumulando | `ingestion_outbox_pending > 100` por 15 min | warning |
| Polling com falhas | mais de 3 falhas de provedor por fonte em 1 h | warning |
| Leituras de polling rejeitadas | qualquer `rejected` em 1 h | warning |
| Falhas de entrega de notificações | > 20% das notificações por 15 min | critical |

As rotas `/health*` e `/metrics` ficam fora das regras de HTTP. Os limiares são valores iniciais. `observability.md` não permite inventar limiar definitivo sem baseline, então eles devem ser calibrados por ambiente depois de algumas semanas de dados.

### 2. Um ponto de contato por webhook

O ponto de contato `sentinel-operators` é um webhook configurado por `ALERT_WEBHOOK_URL` (no `.env`). As notificações são agrupadas por `alertname` e `service`, com repetição a cada 4 h. Sem a URL, o compose usa um endereço `.invalid`: os alertas aparecem no Grafana e a entrega falha. Alertmanager e outros canais ficam para quando houver um destino de plantão definido.

### 3. Dashboards provisionados

Na pasta Sentinel:

- **Visão geral da plataforma:** serviços no ar, requisições, 5xx, p95, memória, GC e rate limit do gateway.
- **Mensageria:** resultado por serviço e tópico, DLQ, pausas e retentativas.
- **Ingestion:** outbox pendente, requisições aos provedores e leituras por resultado.
- **Pipeline de alertas:** alertas disparados, confirmados, escalados e expirados; janelas; quórum; latência; entregas do channels.

O dashboard do channels foi corrigido para o datasource `prometheus`.

### 4. Métricas novas para os alertas

- `sentinel_messages_handled_total{topic,outcome}` e `sentinel_message_retries_total{topic}`. O meter `Sentinel.Messaging` fica em cada cópia de `Messaging/Reliability/MessagingMetrics` e no consumidor do reporting, e é exportado pelo kit de observabilidade de todas as Apis.
- `ingestion_outbox_pending`, atualizado a cada passada do relay.
- O meter `AlertOrchestrator` passa a ser exportado.

Os rótulos são de baixa cardinalidade (serviço, tópico, resultado, fonte). Nenhum tenant aparece como rótulo.

## Consequências

- Uma dependência fora do ar aparece como consumidor pausado e 5xx. Uma configuração errada de polling aparece como leituras rejeitadas.
- As cópias de `MessagingMetrics` precisam ficar em sincronia, como o resto do kit.
- Regras e dashboards são arquivos versionados. Edições feitas pela interface do Grafana não persistem, e mudanças devem voltar para o repositório.
