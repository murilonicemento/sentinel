# Ingestion Integration Tests

Os testes sobem as próprias dependências com Testcontainers; basta ter Docker disponível.

```bash
dotnet test services/ingestion/tests/Ingestion.IntegrationTests
```

| Pasta | O que cobre | Dependências |
|---|---|---|
| `Stack/` | Caminho de escrita de ponta a ponta (rotas legadas e v1): Postgres com a seção `ingestion` de `platform/init-db/postgres/2-schema.sql`, MongoDB com os índices únicos de `2-schema.js`, Redis, MinIO e Kafka. Cobre a transação do outbox, a idempotência de coletas repetidas, o relay para o Kafka no trace da requisição e o isolamento de tenant | Postgres 15.5, Mongo 7, Redis 7, Kafka 7.6.0, MinIO (mesma imagem do compose) |
| `V1/` | Contrato HTTP v1 (status, Problem Details, paginação, autorização) com o mediator simulado | nenhuma |
| `ReadModel/` | Repositório de leitura no Mongo | Mongo 7 |
| `Messaging/` | Propagação de trace pelos headers do Kafka | nenhuma |

Em `Stack/`, só as chamadas a outros serviços (tenants-billing e geospatial) são substituídas por dublês. Todas as classes compartilham um conjunto de contêineres (`IngestionStackCollection`), e cada teste usa uma fonte de dados nova para não depender da ordem de execução.
