# ADR-0004: Observabilidade da plataforma

- **Status:** Proposto
- **Data:** 2026-10-27
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (achados X-08, X-09, X-10, X-19, X-20, X-21, GW-03, ING-08, RC-09, GEO-05, RE-09, AO-07, CH-09, REP-06, TB-06), `ADR-0003` (consumidores Kafka)

## Contexto

A auditoria encontrou observabilidade diferente em cada serviço:

- **Logs:** ingestion e risk-catalog mandavam logs para um índice do Elasticsearch; channels e reporting só escreviam no console; tenants-billing e o api-gateway não tinham Serilog. Os logs que chegavam ao Loki não tinham um label de serviço comum.
- **Traces:** só alert-orchestrator, audit-compliance, channels e reporting registravam um `ActivitySource`, e nenhum serviço exportava traces. Não havia backend de traces na plataforma, e o contexto se perdia no Kafka.
- **Métricas:** só channels, reporting e audit-compliance expunham `/metrics`. O `prometheus.yml` raspava `channels-service` (host inexistente) e serviços sem o endpoint.
- **Health checks:** geospatial, ingestion, risk-catalog e risk-evaluation não tinham nenhum; os demais usavam um único `/health`, exceto audit-compliance (`/health/live` e `/health/ready`).
- **Rate limit:** o api-gateway, porta de entrada da plataforma, não tinha limite de requisições.

`.claude/rules/observability.md` pede logs estruturados com contexto de trace, W3C Trace Context nas fronteiras HTTP e de mensageria, liveness separada de readiness e labels de métrica de baixa cardinalidade. Também proíbe introduzir uma plataforma de observabilidade sem decisão explícita. `.claude/rules/api-design.md` pede 429 com `Retry-After` e proíbe confiar em cabeçalhos de origem fornecidos pelo cliente.

## Decisões

### 1. Um kit de observabilidade copiado em cada Api

Cada Api tem `Observability/ObservabilityExtensions.cs` (não há biblioteca compartilhada; ver `CLAUDE.md`):

- `AddSentinelObservability(serviceName, activitySources, meters)`:
  - Serilog com console e Loki. O label Loki `service` e a propriedade `service` usam o nome do container (`risk-evaluation`, `channels`, ...); a propriedade `environment` vem do ambiente. Cada evento leva `TraceId` e `SpanId` da `Activity` corrente. Os níveis continuam na seção `Serilog` do `appsettings.json`. Os sinks não ficam mais lá.
  - OpenTelemetry com recurso `service.name` igual ao nome do serviço:
    - **Traces:** ASP.NET Core (sem `/health` e `/metrics`), HttpClient, a fonte `Sentinel.Messaging` e as fontes próprias do serviço.
    - **Métricas:** ASP.NET Core, HttpClient, runtime e os meters próprios do serviço, expostas para o Prometheus.
- `UseSentinelRequestLogging()`: uma linha por requisição, em `Verbose` para health e `/metrics`, e em `Error` para 5xx e exceções.
- `MapSentinelOperationalEndpoints()`: `/health/live`, `/health/ready`, `/health` e `/metrics`, todos anônimos.

Configuração: `Loki:Url` e `OpenTelemetry:OtlpEndpoint`. Sem elas, os logs ficam no console e os traces não saem do processo. Por isso os testes e uma execução local isolada não dependem da stack de observabilidade. O compose define `Loki__Url=http://loki:3100` e `OpenTelemetry__OtlpEndpoint=http://tempo:4317` em todos os serviços.

### 2. Loki é o destino único de logs

O sink Serilog do Elasticsearch saiu do ingestion e do risk-catalog (pacotes `Serilog.Sinks.Elasticsearch` e enrichers sem uso removidos). Elasticsearch e Kibana continuam no compose para os índices de dados (`geospatial-events`, `reporting-events`). Os índices `ingestion-logs-*` e `risk-catalog-logs-*` deixam de receber dados. Logs antigos que ainda importem precisam ser lidos ou exportados no Kibana antes de expirarem.

### 3. Tempo como backend de traces

O compose ganhou `grafana/tempo:2.6.1` em modo monolítico (`platform/observability/tempo/tempo.yml`):

- Recebe OTLP gRPC (4317) e HTTP (4318) e guarda os traces em volume local por 72 h.
- Um contêiner `tempo-init` ajusta o dono do volume para o uid do Tempo.
- O metrics-generator fica desligado, então não há mapa de serviços.

No Grafana:

- O datasource Tempo liga cada span aos logs do mesmo trace no Loki (`tracesToLogsV2`, filtrando pelo label `service`).
- O datasource Loki tem um campo derivado que abre o trace a partir do `TraceId` da linha.

Essa configuração é para o ambiente local; produção exige armazenamento de objetos e retenção definidos à parte.

### 4. W3C Trace Context nos headers do Kafka

Cada Infrastructure com Kafka tem uma cópia de `Messaging/KafkaTracing`:

- **Produtores de eventos de negócio** abrem um span `<tópico> publish` (Producer) e gravam `traceparent`/`tracestate` nos headers. Um `traceparent` herdado é substituído.
- **Consumidores** continuam o trace com um span `<tópico> process` (Consumer). Isso vale para `ReliableConsumerLoop`, o consumidor do audit-compliance e o do reporting.
- Mensagens sem cabeçalho ou com cabeçalho inválido iniciam um trace novo.

A implementação usa só `System.Diagnostics` (`DistributedContextPropagator`, W3C por padrão), então a camada Infrastructure não depende do OpenTelemetry. Os DLQs e os replays copiam os headers da mensagem original e abrem um span de publicação filho do processamento (X-20). Os headers são aditivos: consumidores antigos os ignoram.

### 5. Liveness, readiness e `/health`

| Endpoint | O que verifica | Uso |
|---|---|---|
| `/health/live` | nada além do processo | reinício do contêiner |
| `/health/ready` | dependências marcadas `ready`, timeout de 3 s cada | receber tráfego |
| `/health` | todas as verificações | compatibilidade com sondas existentes, diagnóstico |

Entram em readiness só as dependências sem as quais a API não atende:

- **PostgreSQL:** ingestion, risk-catalog e tenants-billing; alert-orchestrator e channels quando o adaptador está ligado.
- **MongoDB:** audit-compliance, ingestion e risk-evaluation.
- **Redis:** risk-evaluation; alert-orchestrator e channels quando ligado.
- **Elasticsearch e Redis:** reporting.

Ficam só em `/health`, como degradado: o Redis do ingestion (deduplicação do polling) e do risk-catalog (cache), o Kafka do channels e o Elasticsearch do geospatial (as operações geométricas funcionam sem ele).

As respostas trazem só o status, sem detalhes das dependências. O channels deixou de usar o `UIResponseWriter`, que expunha o nome e o erro de cada verificação.

**Mudança de comportamento:** `/health` agora falha (503) quando uma dependência crítica está fora do ar. Antes, em vários serviços, ele respondia 200 sempre. Sondas que precisam só saber se o processo está vivo devem usar `/health/live`.

### 6. Prometheus raspa todos os serviços

O `prometheus.yml` tem um job `sentinel-services` com os dez serviços na porta interna 8080 e o label `service` derivado do host. `/metrics` é para a rede interna e não tem rota no gateway. Não entram labels por tenant, usuário ou URL crua.

### 7. Rate limit no api-gateway, por IP de origem

O limiter nativo do ASP.NET Core usa janela fixa por endereço TCP do cliente, com padrão de 300 requisições por 60 s (`RateLimiting:PermitLimit` / `WindowSeconds`, validados no startup) e sem fila. Ao exceder, o gateway responde:

- 429 com `Retry-After`;
- corpo RFC 9457 com `code` `RATE_LIMIT_EXCEEDED` e `traceId`.

Regras:

- `X-Forwarded-For` não é usado, porque não há proxy confiável na frente do gateway.
- Health e `/metrics` não contam no limite.
- Os endpoints próprios do gateway rodam antes do Ocelot (`UseRouting`/`UseEndpoints` antes de `UseOcelot`), porque o Ocelot responde a toda requisição que chega a ele.

Alternativas consideradas:

- Limite por identidade ou tenant: o gateway não valida JWT, e confiar em claims não verificadas violaria a regra.
- Rate limit do próprio Ocelot: configuração por rota, sem `Retry-After` padronizado.

## Consequências

- Um log no Loki leva ao trace no Tempo e vice-versa, e um evento pode ser seguido de ingestion até alert-orchestrator pelos headers do Kafka.
- Um serviço sem Loki ou Tempo configurado continua funcionando e loga no console. Os sinks Loki e OTLP são assíncronos e não bloqueiam requisições quando o destino cai.
- Clientes atrás de um mesmo NAT dividem a cota do gateway. Se um proxy confiável for colocado na frente, a política precisa ser revista (`ForwardedHeaders` restrito ao proxy).
- Existem dez cópias de `ObservabilityExtensions` e nove de `KafkaTracing`, que precisam ficar em sincronia até existir uma biblioteca compartilhada.
- Os traces aumentam o volume de dados. Não há amostragem configurada (100%), aceitável no ambiente local; produção deve definir amostragem.

## Decisões ainda pendentes

- ~~Ligar o trace da requisição ao do outbox do ingestion (X-19)~~ resolvido em 2026-10-28: coluna aditiva `outbox.trace_parent`; o relay publica como filho do trace da requisição.
- Alertas e dashboards por serviço, com limiares derivados de baseline (X-21).
- Amostragem, retenção e armazenamento de traces e logs fora do ambiente local.
