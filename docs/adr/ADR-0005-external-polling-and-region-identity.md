# ADR-0005: Polling de fontes externas por assinatura e identidade de região

- **Status:** Proposto
- **Data:** 2026-10-29
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (ING-21, ING-22, ING-23, RC-14), `ADR-0003` (deduplicação), `ADR-0004` (métricas)

## Contexto

**Polling (ING-21).** O `SensorPollingHostedService` do ingestion estava fora do DI. Os clientes tinham vários problemas:

- **Amarração fixa e configuração de exemplo:** cada fonte era amarrada a um tenant e a uma fonte de dados fixos, com GUIDs inválidos, URLs `*.example.com` e um placeholder de chave versionado.
- **Intervalo e falhas:** todas as fontes usavam um intervalo único de 30 s, acima das cotas do Stormglass (10 requisições por dia no plano gratuito) e do FIRMS. Uma fonte com falha interrompia as demais.
- **Leituras rejeitadas ou perdidas:**
  - Um poll do USGS traz vários eventos para a mesma fonte de dados, e a regra de frequência (`Hourly`) rejeitava todos depois do primeiro.
  - O Stormglass gravava todas as horas com o mesmo `CollectedAt = UtcNow`, e a deduplicação descartava todas menos a primeira.
- **Unidades e consultas erradas:**
  - As unidades enviadas (`mww`, `Kelvin`, `Celsius`, `Percent`) não eram aceitas pelo domínio.
  - A NOAA era consultada numa estação fixa de Nebraska, com datas em formato local.
  - O Stormglass recebia a chave como `ApiKey <chave>` num caminho que perdia o `/v2`.

**Região (RC-14).** O risk-catalog rejeitava como "já existe" qualquer parâmetro regional com o mesmo fator de ajuste de outro, de modo que duas regiões diferentes com fator 1,5 não podiam coexistir.

## Decisões

### 1. Assinaturas por tenant, por fonte

`Polling:<Fonte>` (Earthquake, Fire, Flood, Landslide, TemperatureAnomaly, HumidityAnomaly, WindGust, Rainfall, PressureChange) tem:

| Campo | Significado |
|---|---|
| `Enabled` | A fonte só é consultada quando é `true`; desligada, não exige nenhuma outra chave. |
| `BaseUrl` | Endereço do provedor. |
| `ApiKey` | Map key do FIRMS, token da NOAA CDO ou chave do Stormglass. Segredo: fica em User Secrets ou `.env`, nunca em `appsettings`. |
| `IntervalMinutes` | Intervalo entre polls (padrões: USGS 15, FIRMS e Stormglass 180, NOAA 1440). |
| `LookbackHours` | Janela consultada em cada poll. |
| `Subscriptions[]` | `TenantId`, `DataSourceId`, `Latitude`, `Longitude`, `RadiusKm` e `StationId` (este só para a NOAA). |

Cada assinatura aponta para uma fonte de dados já cadastrada do tenant. A fonte de dados decide o domínio, o tipo de evento e as unidades aceitas. Exemplos: USGS → `Earthquake` (`Mw`); FIRMS → `Fire` (`MW`); NOAA PRCP e Stormglass precipitation → `Rainfall` (`mm`).

Validação no startup: uma fonte ligada sem `BaseUrl`, sem chave, sem assinaturas, com coordenadas inválidas ou, na NOAA, sem estação impede a subida, com mensagem que nomeia a chave.

As assinaturas não são segredo. Ficam num `appsettings.Polling.json` opcional, montado pelo compose a partir de `platform/config/ingestion-polling.json`. O arquivo entra logo depois de `appsettings.{Environment}.json`, então variáveis de ambiente e User Secrets continuam valendo por cima dele.

### 2. A regra de frequência vale só para envios HTTP

Coletas enviadas pela API seguem a `CollectionFrequency` da fonte de dados. No polling, quem controla o ritmo é o intervalo da fonte: cada leitura entra com o horário do próprio provedor e é deduplicada pelo id que o provedor dá ao evento. A chave Redis passa a ser `ing:{tenant}:{dataSource}:src:{id do provedor}`; a chave das coletas HTTP continua com o formato antigo, lido por versões anteriores. Exemplos de id do provedor:

- o id do evento no USGS;
- satélite + posição + horário no FIRMS;
- estação + tipo + dia na NOAA;
- parâmetro + ponto + hora no Stormglass.

Por isso uma janela consultada de novo não ingere nada em dobro. O Stormglass só é consultado no passado, porque horas futuras são previsão e não detecção.

### 3. Falhas isoladas e observáveis

Uma assinatura que falha, ou uma leitura rejeitada, é logada e contada sem interromper as demais. As métricas são:

- `ingestion_polling_requests_total{source,result}`;
- `ingestion_polling_readings_total{source,result}`, com `result` igual a `processed`, `rejected` ou `failed`.

Uma leitura `rejected` indica configuração incompatível. Uma `failed` indica armazenamento indisponível; nesse caso o próximo poll cobre a mesma janela.

### 4. Código de região como identidade

`RegionCode` passa a identificar a região. O código é:

- um código IBGE ou um slug;
- normalizado em minúsculas;
- composto de 1 a 64 letras, dígitos, `.`, `_`, `:` ou `-`;
- único (`IX_regional_parameter_region_code`).

Regras de uso:

- No v1, `regionCode` é obrigatório. É uma mudança de contrato aprovada na decisão.
- A rota legada sem código gera `legacy-<id>`.
- A migration `AddRegionCode` dá esse mesmo código às linhas existentes antes de criar o índice.
- O fator de ajuste deixa de ser identidade, e duas regiões podem compartilhá-lo.
- O código aparece nas respostas e na listagem v1 (campo aditivo).

## Consequências

- Um tenant recebe dados externos ao assinar uma fonte com uma fonte de dados compatível. Fontes sem chave ficam desligadas sem afetar o serviço.
- Uma fonte global (USGS, FIRMS) é consultada uma vez por assinatura. Muitas assinaturas da mesma fonte multiplicam as requisições; agregá-las numa consulta só fica para quando o volume justificar.
- O limiar de incêndio do domínio (`> 100`) recebe FRP em MW, então só incêndios grandes geram evento. Isso foi mantido por decisão de negócio em 2026-10-29 (ING-22).
- O GHCND não tem umidade relativa em todas as estações (`RHAV`), e as fontes Flood e Landslide geram eventos de chuva (`Rainfall`), porque o domínio não tem tipo de medição para inundação ou deslizamento.
- Clientes do v1 que criavam regiões sem código passam a receber 400.

## Decisões ainda pendentes

- Calibração dos limiares por tipo de medição com dados reais (ING-22).
- Agregar assinaturas de uma mesma fonte numa única consulta ao provedor.
