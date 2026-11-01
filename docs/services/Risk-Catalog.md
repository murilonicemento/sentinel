# **Risk Catalog Service**

## **1. Visão Geral**

O **Risk Catalog Service** é responsável por centralizar, padronizar e versionar definições técnicas relacionadas a eventos naturais, severidade e modelos de risco.

Ele atua como a fonte de verdade para classificação e interpretação de eventos dentro da plataforma, garantindo consistência semântica e alinhamento entre os serviços.

O serviço **não processa eventos em tempo real**, nem executa avaliações dinâmicas. Seu papel é fornecer **referências técnicas confiáveis** para consumo por outros domínios.

---

## **2. Estrutura de Domínio**

O serviço é dividido em dois contextos principais, com responsabilidades bem definidas e independentes:

- **Tipos de Evento e Severidade**
- **Matrizes de Risco e Curvas IDF**

Esses contextos são complementares, porém desacoplados.

---

## **3. Contexto: Tipos de Evento e Severidade**

### **Objetivo**

Definir e padronizar a classificação de eventos naturais e seus respectivos níveis de severidade, estabelecendo uma linguagem ubíqua compartilhada entre todos os serviços da plataforma.

Este contexto responde:

- “Qual é o tipo do evento?”
- “Qual o nível de severidade associado?”

---

### **Responsabilidades**

- Manter o catálogo oficial de tipos de eventos naturais
- Definir níveis de severidade associados a cada tipo
- Estabelecer critérios técnicos de classificação
- Versionar regras de severidade
- Garantir consistência semântica entre serviços consumidores

---

### **Tipos de Evento (Exemplos)**

- Enchente
- Deslizamento
- Tempestade severa
- Seca
- Onda de calor

---

### **Níveis de Severidade**

- Leve
- Moderado
- Severo
- Extremo

---

### **Agregados**

| Agregado               | Descrição                                   |
| ---------------------- | ------------------------------------------- |
| **TipoEvento**         | Define o tipo de evento e suas propriedades |
| **Severidade**         | Representa níveis de classificação          |
| **CriterioSeveridade** | Define regras quantitativas ou qualitativas |

---

### **Observações**

- A classificação é baseada em critérios técnicos definidos no catálogo
- Não há interpretação subjetiva por serviços consumidores
- Este contexto é consultado de forma síncrona ou via cache

---

## **4. Contexto: Matrizes de Risco e Curvas IDF**

### **Objetivo**

Fornecer modelos técnicos para análise de risco, correlacionando intensidade de eventos com probabilidade de impacto.

Este contexto responde:

- “Qual o risco associado ao evento?”
- “Qual a probabilidade de impacto relevante?”

---

### **Responsabilidades**

- Definir e manter matrizes de risco configuráveis
- Gerenciar curvas IDF (Intensidade, Duração, Frequência)
- Versionar modelos de risco
- Permitir regionalização de parâmetros
- Fornecer dados para cálculo de risco e score

---

### **Agregados**

| Agregado              | Descrição                                                  |
| --------------------- | ---------------------------------------------------------- |
| **MatrizRisco**       | Define níveis de risco com base em probabilidade e impacto |
| **CurvaIDF**          | Modela comportamento pluviométrico                         |
| **ParametroRegional** | Ajusta critérios conforme localidade                       |

**Identidade da região (RC-14, ADR-0005):** cada parâmetro regional tem um `regionCode` único, sem diferenciar maiúsculas, com 1 a 64 letras, dígitos, `.`, `_`, `:` ou `-` (ex.: código IBGE `3550308` ou `sp-capital`). Duas regiões são a mesma quando o código coincide; o fator de ajuste é só um valor e pode se repetir entre regiões. O código é obrigatório no v1; a rota legada gera `legacy-<id>` quando ele não é informado.

---

### **Características**

- Baseado em modelos estatísticos e históricos
- Sensível a contexto geográfico
- Independente de eventos em tempo real
- Otimizado para leitura e consulta

---

## **5. Relação entre os Contextos**

Os contextos possuem responsabilidades distintas e complementares:

| Contexto                           | Responsabilidade        |
| ---------------------------------- | ----------------------- |
| **Tipos de Evento e Severidade**   | Classificação do evento |
| **Matrizes de Risco e Curvas IDF** | Avaliação de risco      |

Separação clara de responsabilidades:

- Não compartilham lógica interna
- Não possuem acoplamento direto
- Interagem apenas via contratos bem definidos

---

## **6. Papel na Arquitetura**

O Risk Catalog Service é classificado como:

- **Core Supporting Domain**
- **Read-heavy**
- **Versionado**
- **Altamente estável**

---

## **7. Características Operacionais**

- Baixa frequência de mudanças
- Alto impacto em serviços downstream
- Forte dependência de consistência e versionamento
- Ideal para uso com estratégias de cache

---

## **8. Considerações de Design**

- Separação explícita entre classificação e avaliação de risco
- Versionamento como requisito fundamental
- Modelagem orientada à extensibilidade
- Evita duplicação de regras em serviços consumidores

---

## **9. Impacto no Ecossistema**

Qualquer alteração neste serviço pode impactar diretamente:

- regras de classificação de eventos
- cálculos de risco
- decisões automatizadas downstream

Por esse motivo, mudanças devem ser controladas, versionadas e auditáveis.

---

## **10. Estado da implementação (verificado em 2026-10-29)**

**API.** Base `/api/v1/risk-catalog`, contrato comum de `docs/compliance/services-validation.md` §4.2 e autenticação JWT (ADR-0002). Leitura com `RiskCatalog.Read`, escrita com `RiskCatalog.Write`.

| Grupo | Rotas |
|---|---|
| Tipos de evento e severidade | `GET/POST /event-types`, `GET /event-types/{code}`, `GET /event-types/{code}/severity-criteria`, `PATCH /event-types/{id}/status`, `POST /severities`, `POST /severity-criteria` |
| Modelo de risco | `GET/POST /risk-matrices`, `GET/POST /idf-curves`, `GET /regional-risk-parameters/{regionId}`, `POST /regional-risk-parameters` (com `regionCode` obrigatório, RC-14), `POST /catalog-publications` |
| Listagens de uma versão publicada | `GET /regional-risk-parameters`, `GET /risk-matrix-entries`, `GET /severity-criteria` (paginadas, usadas pelo risk-evaluation) |

As rotas antigas `/api/event-types/*` e `/api/risk-model/*` estão depreciadas até 2027-01-23 (ADR-0007). Os códigos de erro do serviço estão no §4.2 do relatório.

**Publicação.** `POST /catalog-publications` publica `{ Version, Notes, PublishedAt, PublishedBy }` em `risk-catalog-published`. O risk-evaluation reage copiando a versão pelas listagens.

**Validação geográfica.** Ao criar um parâmetro regional, o serviço chama o geospatial: `contains-point` para o centro e `intersects` para o polígono, contra os limites do Brasil. Usa a rota antiga (X-25).

**Persistência.** PostgreSQL `risk_catalog` (`event_type`, `severity`, `severity_criterion`, `idf_curve`, `regional_parameter`, `risk_matrix`, `users`; migrations EF Core, seção do `2-schema.sql`) e Redis como cache de leitura de matrizes e curvas IDF (TTL de 1 hora).
