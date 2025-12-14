# Ingestion Integration Tests

Este diretório contém os testes de integração para o serviço Ingestion.

## Pré-requisitos

Para executar os testes de integração, você precisa ter os seguintes serviços em execução:

1. **PostgreSQL** (porta 5432)
   - Database: `ingestion_test`
   - Username: `postgres`
   - Password: `postgres`

2. **MongoDB** (porta 27017)
   - Database padrão: `IngestionReadDatabase`

3. **Redis** (porta 6379)
   - Para deduplicação de eventos

4. **Kafka** (porta 9093 para conexões externas)
   - Para publicação de eventos
   - **Nota**: A porta 9093 é usada para conexões de clientes externos (localhost)

5. **MinIO** (porta 9000)
   - Host: `localhost`
   - AccessKey: `minioadmin`
   - SecretKey: `minioadmin`
   - Bucket será criado automaticamente durante os testes

## Executando os Testes

### Via CLI

```bash
cd services/ingestion/tests/Ingestion.IntegrationTests
dotnet test
```

### Via Visual Studio / Rider

Execute os testes através da interface do IDE, selecionando o projeto `Ingestion.IntegrationTests`.

## Estrutura dos Testes

Os testes estão organizados por funcionalidade:

- **RegisterDataSourceIntegrationTests**: Testa o registro de fontes de dados
- **RegisterSensorCollectionIntegrationTests**: Testa o registro de coleções de sensores
- **GetCollectionStatisticsIntegrationTests**: Testa a obtenção de estatísticas de coleção
- **GetLastDetectedEventsIntegrationTests**: Testa a obtenção dos últimos eventos detectados

## Configuração

As configurações de conexão estão definidas no `IngestionWebApplicationFactory` e podem ser sobrescritas através de variáveis de ambiente ou modificando o factory diretamente.

### Variáveis de Ambiente

Você pode sobrescrever as configurações padrão usando variáveis de ambiente:

```bash
# PostgreSQL
CONNECTIONSTRINGS__INGESTIONWRITEDATABASE=Host=localhost;Port=5432;Database=ingestion_test;Username=postgres;Password=postgres

# MongoDB
CONNECTIONSTRINGS__INGESTIONREADDATABASE=mongodb://localhost:27017

# Redis
CONNECTIONSTRINGS__REDIS=localhost:6379

# Kafka (usar porta 9093 para conexões externas)
CONNECTIONSTRINGS__KAFKA=localhost:9093

# MinIO
MINIO__HOST=localhost
MINIO__PORT=9000
MINIO__ACCESSKEY=minioadmin
MINIO__SECRETKEY=minioadmin
MINIO__BUCKETNAME=ingestion-test
```

### Portas Padrão dos Serviços

- **PostgreSQL**: 5432
- **MongoDB**: 27017
- **Redis**: 6379
- **Kafka**: 9093 (porta externa, 9092 é para conexões internas no Docker)
- **MinIO**: 9000 (API), 9001 (Console)

## Notas

- Os testes utilizam um ambiente de teste isolado através do `WebApplicationFactory`
- Os dados criados durante os testes são persistidos no banco de dados de teste
- É recomendável limpar o banco de dados entre execuções de teste para evitar conflitos

