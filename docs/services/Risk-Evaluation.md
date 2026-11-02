# **Risk Evaluation & Scoring Service**

## **1. Visão Geral**

O **Risk Evaluation & Scoring Service** é responsável por transformar dados operacionais e técnicos em informações acionáveis, por meio do cálculo de score e classificação de risco.

Ele atua como o ponto central de decisão da plataforma, correlacionando:

- dados de eventos e clima
- modelos de risco
- contexto geoespacial

O serviço fornece uma avaliação objetiva do risco associado a uma determinada condição, permitindo a tomada de decisão e acionamento de alertas.

---

## **2. Objetivos**

- Calcular o **score de risco** com base em múltiplas variáveis
- Classificar eventos em níveis de risco padronizados
- Aplicar modelos preditivos para antecipação de cenários críticos
- Publicar eventos para serviços de orquestração de alertas
- Permitir evolução contínua dos modelos sem impacto sistêmico

---

## **3. Responsabilidades**

- Consumir e agregar dados de diferentes domínios:
  - dados climáticos
  - informações geoespaciais
  - definições do catálogo de risco

- Aplicar regras de negócio e modelos matemáticos
- Gerar um **Risk Score normalizado**
- Classificar o nível de risco
- Publicar eventos com os resultados da avaliação

---

## **4. Entradas e Saídas**

### **Entradas**

Eventos consumidos via mensageria (Kafka/RabbitMQ):

- `WeatherDataUpdated`
- `RiskCatalogUpdated`
- `GeoSpatialDataUpdated`

**Exemplo de payload:**

```json
{
  "location": "lat/long",
  "timestamp": "ISO8601",
  "metrics": {
    "gust": 3.55,
    "precipitation": 0.26,
    "pressure": 1012
  }
}
```

---

### **Saídas**

Eventos publicados:

- `RiskEvaluated`
- `RiskScoreUpdated`
- `HighRiskDetected`

**Exemplo de payload:**

```json
{
  "location": "lat/long",
  "riskScore": 0.82,
  "riskLevel": "HIGH",
  "factors": ["wind", "precipitation"],
  "timestamp": "ISO8601"
}
```

---

## **5. Modelo de Domínio**

### **Entidades**

| Entidade           | Descrição                                    |
| ------------------ | -------------------------------------------- |
| **RiskEvaluation** | Representa uma avaliação de risco realizada  |
| **RiskFactor**     | Representa um fator que influencia o cálculo |
| **RiskModel**      | Define o modelo e parâmetros utilizados      |

---

### **Estrutura**

**RiskEvaluation**

- Id
- Location
- Timestamp
- Score
- Level

**RiskFactor**

- Type (ex: wind, precipitation)
- Weight
- Value

**RiskModel**

- Version
- Parameters
- Formula

---

## **6. Cálculo do Score**

O cálculo do score é baseado em uma composição ponderada dos fatores de risco:

RiskScore = \sum (factor_value \cdot factor_weight)

### **Exemplo**

Score = (gust \cdot 0.4) + (precipitation \cdot 0.4) + (pressure \cdot 0.2)

Após o cálculo:

- O valor é normalizado para o intervalo **[0, 1]**
- Classificado conforme thresholds definidos

### **Classificação de Risco**

| Score     | Nível   |
| --------- | ------- |
| 0.0 – 0.3 | Baixo   |
| 0.3 – 0.6 | Médio   |
| 0.6 – 0.8 | Alto    |
| 0.8 – 1.0 | Crítico |

---

## **7. Modelos de Avaliação**

O serviço suporta múltiplas abordagens de modelagem:

- Regras heurísticas (baseline inicial)
- Modelos estatísticos
- Modelos baseados em Machine Learning

### **Estratégias de Evolução**

- Versionamento de modelos
- Feature flags para ativação controlada
- Testes A/B para validação de novos modelos

---

## **8. Arquitetura**

### **Características**

- Microsserviço independente
- Arquitetura orientada a eventos
- Stateless (com exceção de cache e persistência histórica)

### **Dependências**

- **Ingestion Service** → fornecimento de dados brutos
- **Risk Catalog Service** → regras e parâmetros
- **Geospatial Service** → contexto espacial

---

## **9. Persistência**

- **MongoDB**
  - armazenamento de avaliações históricas

- **Redis**
  - cache de scores recentes (baixa latência)

---

## **10. Fluxo de Processamento**

1. Recebimento de evento
2. Enriquecimento com dados de risco e contexto
3. Seleção do modelo aplicável
4. Cálculo do score
5. Classificação do nível de risco
6. Publicação dos eventos resultantes

---

## **11. Regras de Negócio**

- O score deve ser recalculado sempre que:
  - novos dados relevantes forem recebidos
  - o modelo de risco for atualizado

- Eventos classificados como alto ou crítico devem ser publicados imediatamente
- O serviço deve suportar avaliações simultâneas em múltiplas regiões

---

## **12. Observabilidade**

### **Logs**

- Inputs utilizados no cálculo
- Score gerado
- Modelo aplicado

### **Métricas**

- tempo médio de cálculo
- volume de avaliações
- distribuição por nível de risco

### **Tracing**

- rastreamento distribuído do pipeline de avaliação

---

## **13. Evolução**

Possíveis extensões:

- Integração com modelos de Machine Learning
- Ajuste dinâmico de pesos
- Feedback loop com dados históricos
- Simulações preditivas

---

## **14. Considerações Finais**

Este serviço representa o ponto de convergência entre dados e decisão dentro da plataforma.

Sua confiabilidade impacta diretamente:

- a qualidade dos alertas
- a precisão das análises
- a credibilidade do sistema como um todo

Por esse motivo, deve ser tratado como componente crítico, com forte controle de versionamento, observabilidade e governança de mudanças.
