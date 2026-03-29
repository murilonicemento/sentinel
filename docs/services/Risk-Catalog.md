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
