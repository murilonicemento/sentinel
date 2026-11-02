# **Ingestion Service**

## **1. Visão Geral**

O **Ingestion Service** é responsável pela ingestão e processamento de eventos naturais provenientes de múltiplas fontes externas, incluindo APIs meteorológicas, dispositivos IoT, satélites e entradas via crowdsourcing.

O serviço atua como a porta de entrada do ecossistema, garantindo que todos os dados recebidos sejam:

- validados,
- padronizados,
- deduplicados,
- rastreáveis,
- e disponibilizados em tempo quase real.

Além do processamento, o serviço também mantém um modelo de leitura otimizado em **MongoDB**, seguindo o padrão **CQRS**, permitindo consultas rápidas para monitoramento operacional.

---

## **2. Responsabilidades**

O Ingestion Service é responsável por:

- Integrar-se com fontes externas (APIs REST, MQTT/AMQP, Webhooks)
- Coletar e armazenar dados brutos
- Validar a integridade e consistência dos dados recebidos
- Normalizar os dados para um formato padrão (**EventoClimaticoDTO**)
- Realizar deduplicação de eventos utilizando Redis com TTL configurável
- Publicar eventos normalizados em um barramento de mensageria (Kafka ou RabbitMQ)
- Persistir o payload original para fins de auditoria e reprocessamento
- Atualizar o **Read Model** no MongoDB
- Expor consultas otimizadas para consumo por dashboards e serviços downstream

---

## **3. Modelo de Domínio**

### **Agregados**

| Agregado          | Descrição                                            |
| ----------------- | ---------------------------------------------------- |
| **FonteDeDados**  | Representa a origem de um evento (API, sensor, etc.) |
| **Coleta**        | Representa um evento bruto coletado                  |
| **AmostraSensor** | Representa uma leitura individual de sensor          |

### **Objetos**

| Objeto                 | Descrição                                                   |
| ---------------------- | ----------------------------------------------------------- |
| **EventoClimaticoDTO** | Estrutura padronizada utilizada para distribuição e leitura |

**Campos principais do DTO:**

- `eventoId`
- `tipoEvento`
- `coordenadas`
- `intensidade`
- `coletadoEm`

**Observação:**
O **EventoClimaticoDTO** não faz parte do domínio. Ele pertence à camada de aplicação e também é utilizado como modelo de leitura no MongoDB.

---

## **4. Fluxo de Processamento**

O fluxo de dados segue as seguintes etapas:

1. **Ingestão**
   Recebimento ou coleta de dados a partir de fontes externas.

2. **Validação**
   Verificação da estrutura e integridade do payload.

3. **Deduplicação**
   Verificação de eventos já processados utilizando Redis.

4. **Normalização**
   Conversão do payload para o formato padrão (**EventoClimaticoDTO**), incluindo cálculo de intensidade.

5. **Publicação**
   Envio do evento normalizado para o barramento de mensageria.

6. **Atualização do Read Model**
   Persistência do evento normalizado no MongoDB.

7. **Auditoria**
   Armazenamento do payload original e metadados para rastreabilidade e reprocessamento.

---

## **5. Arquitetura e Tecnologias**

| Componente           | Tecnologia                |
| -------------------- | ------------------------- |
| Mensageria           | Kafka ou RabbitMQ         |
| Cache / Deduplicação | Redis                     |
| Read Model           | MongoDB                   |
| Armazenamento bruto  | S3 ou MinIO               |
| Banco relacional     | PostgreSQL                |
| Plataforma           | .NET 9 / C#               |
| Arquitetura          | Clean Architecture + CQRS |

---

## **6. Padrão CQRS**

### **Commands**

- `RegistrarColetaSensor`
- `RegistrarEventoNatural`

### **Events**

- `EventoClimaticoDetectado`
- `EventoNormalizado` (publicado no barramento)

### **Queries (MongoDB)**

#### **ObterUltimosEventosColetados**

Retorna os eventos mais recentes com:

- ordenação por `collectedAt DESC`
- limite configurável (padrão: 50)
- leitura otimizada via índices no MongoDB

#### **ObterEstatisticasDeColeta**

Fornece métricas agregadas:

- total de eventos
- distribuição por tipo
- intensidade mínima, máxima e média
- filtros opcionais por intervalo de datas

Implementado via **Aggregation Pipeline** do MongoDB.

---

## **7. Estrutura do Projeto**

```
services/ingestion/
├─ src/
│  ├─ Ingestion.Domain/
│  ├─ Ingestion.Application/
│  │   ├─ Commands/
│  │   ├─ Events/
│  │   └─ Queries/
│  ├─ Ingestion.Infrastructure/
│  │   ├─ Redis/
│  │   ├─ Kafka/
│  │   ├─ Outbox/
│  │   └─ MongoReadStore/
│  └─ Ingestion.Api/
├─ tests/
│  ├─ Ingestion.UnitTests/
│  └─ Ingestion.IntegrationTests/
└─ Dockerfile
```

---

## **8. Observabilidade**

O serviço possui suporte a monitoramento e rastreamento através de:

### **Métricas**

- volume de eventos processados
- eventos deduplicados
- latência média de processamento
- tamanho médio dos payloads

### **Logs**

- logs estruturados utilizando Serilog

### **Tracing**

- rastreamento distribuído com OpenTelemetry

### **Visualização**

- dashboards em Grafana

---

## **9. Considerações de Negócio**

- A deduplicação evita geração de alertas redundantes para o mesmo evento
- A normalização permite interoperabilidade com múltiplos serviços downstream
- O uso de MongoDB como Read Model garante baixa latência nas consultas
- A arquitetura suporta multi-tenancy para isolamento por cidade ou organização
- A persistência dos dados brutos possibilita auditoria e reprocessamento
