# ADR-0012: Destino dos tópicos restantes do pipeline (X-24)

- **Status:** Proposto
- **Data:** 2026-11-02
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (X-24), `ADR-0007` (depreciação), `ADR-0008` (contratos do pipeline), `ADR-0009`, `ADR-0010`, `ADR-0011`

## Contexto

Depois dos ADRs 0008 a 0011, restavam cinco tópicos sem produtor ou sem consumidor:

- `sensor-event-detected`: o geospatial o consome para indexar os eventos de sensor, mas ninguém publica nele.
- `region-intersected`: o alert-orchestrator o consome, mas ninguém publica nele.
- `risk-events`, `alert-orchestrator-events` e `tenants-billing-events`: são publicados, mas ninguém os consome.

## Decisões (2026-11-02)

1. **Sensor → geospatial.** O geospatial passa a assinar `disaster-event-detected` e `climatic-event-detected`, com o próprio grupo de consumidor, em vez de `sensor-event-detected` (`KafkaConsumerOptions:Topics`). Os documentos indexados mantêm o `OperationType` `sensor-event-detected`, então as buscas devolvem o mesmo valor. `sensor-event-detected` fica depreciado; o `KafkaConsumerOptions:TopicName` antigo só vale quando `Topics` está vazio.
2. **`region-intersected` depreciado.** O consumidor do alert-orchestrator continua funcionando e registra um aviso ao subir. Será removido depois de **2027-01-31** (90 dias), quando o tópico e o dead-letter estiverem confirmados vazios, junto com o `RegionIntersectedEvent` e o handler.
3. **`risk-events` depreciado.** O tópico repete o que `risk-updated` leva ao alert-orchestrator e não tem consumidor conhecido. Continua sendo publicado até **2027-01-31**, com um aviso no log ao subir. `Kafka:PublishRiskEvents=false` (`RISK_EVALUATION_PUBLISH_RISK_EVENTS` no compose) para de publicar antes disso. Depois do sunset, o padrão passa a ser não publicar, e o código sai.
4. **`alert-orchestrator-events` e `tenants-billing-events`** ficam como tópicos de saída para consumidores externos ou futuros, sem consumidor interno por decisão.

## Alternativas consideradas

- **Ingestion publicar `sensor-event-detected`.** Rejeitada: cada evento seria publicado duas vezes.
- **Manter `region-intersected` como integração reservada.** Rejeitada: um consumidor sem produtor previsto é código morto.
- **Parar de publicar `risk-events` já.** Rejeitada: um consumidor externo desconhecido quebraria sem aviso. A política de depreciação pede 90 dias.

## Consequências

- **Primeira subida do geospatial.** O grupo `geospatial-service-consumer` lê os dois tópicos desde o início (`Earliest`) e indexa o histórico deles. A indexação é idempotente pelo id do evento.
- Contrato do evento de sensor testado dos dois lados com o mesmo JSON: `SensorEventDetectedContractTests` (ingestion) e `SensorEventDetectedConsumerTests` (geospatial).
- Os tópicos depreciados continuam no `kafka-init` até a remoção.
- **Remoção depois de 2027-01-31:**
  1. Confirmar que `region-intersected`, `sensor-event-detected` e `risk-events` não têm mensagens novas nem consumidores externos.
  2. Remover o consumidor, o publisher e os tópicos.
  3. Registrar a remoção neste ADR.
