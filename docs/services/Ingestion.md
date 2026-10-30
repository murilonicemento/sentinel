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

---

## **10. Polling de fontes externas**

Decisão em `docs/adr/ADR-0005-external-polling-and-region-identity.md`. O `SensorPollingHostedService` consulta cada fonte ligada (`Polling:<Fonte>:Enabled`) no próprio intervalo e entrega cada leitura à fonte de dados indicada na assinatura do tenant.

| Fonte | Provedor | Chave | Tipo de medição da fonte de dados | Unidade |
|---|---|---|---|---|
| Earthquake | USGS FDSN (`earthquake.usgs.gov`), raio da assinatura | não | Earthquake | Mw |
| Fire | NASA FIRMS, VIIRS S-NPP NRT, caixa da assinatura | map key | Fire | MW (FRP) |
| Flood | NOAA CDO GHCND `PRCP`, estação da assinatura | token | Rainfall | mm |
| Landslide | NOAA CDO GHCND `PRCP` acumulado na janela | token | Rainfall | mm |
| TemperatureAnomaly | NOAA CDO GHCND `TAVG`/`TMAX`/`TMIN` | token | Temperature | C |
| HumidityAnomaly | NOAA CDO GHCND `RHAV` (nem toda estação tem) | token | Humidity | % |
| WindGust / Rainfall / PressureChange | Stormglass v2 `weather/point`, só horas passadas | API key | WindSpeed / Rainfall / Pressure | m/s / mm / hPa |

O FIRMS entrega a potência radiativa (FRP) em MW, e o limiar de incêndio do domínio (`> 100`) se aplica a ela: só incêndios grandes geram evento (decisão de 2026-10-29, ING-22).

Para ligar uma fonte:

1. Cadastre a fonte de dados do tenant (`POST /api/v1/ingestion/data-sources`) com o tipo de medição da tabela e a permissão do evento esperado (ex.: `Disaster`/`Earthquake`).
2. Acrescente a assinatura em `platform/config/ingestion-polling.json` (`Polling:<Fonte>:Subscriptions`). O arquivo não é segredo.
3. No `platform/.env`, ligue a fonte (`POLLING_<FONTE>_ENABLED=true`) e informe a chave do provedor.

Uma fonte ligada com configuração inválida impede o serviço de subir, com a chave nomeada. Leituras rejeitadas pela fonte de dados (unidade, permissão) aparecem na métrica `ingestion_polling_readings_total{result="rejected"}` e no alerta "Leituras de polling rejeitadas" (ADR-0006).
