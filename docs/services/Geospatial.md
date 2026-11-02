# **Geospatial Service**

## **1. Visão Geral**

O **Geospatial Service** é responsável pelo processamento, validação e disponibilização de dados geoespaciais utilizados pelos demais serviços da plataforma.

Seu objetivo é centralizar operações relacionadas à localização, garantindo consistência, precisão e reuso das regras espaciais em todo o ecossistema.

O serviço atua como um componente especializado, oferecendo suporte para análises baseadas em:

- localização geográfica
- área de impacto
- relações espaciais entre entidades

---

## **2. Contexto**

Diversos domínios do sistema dependem de informações geoespaciais para tomada de decisão, incluindo:

- avaliação de risco com base em localização
- detecção de eventos em regiões específicas
- análise de interseção e sobreposição de áreas
- correlação espacial entre eventos e critérios de severidade

A ausência de um serviço dedicado levaria à duplicação de lógica, aumento de acoplamento e inconsistência nos cálculos espaciais.

---

## **3. Objetivo e Escopo**

O Geospatial Service foi projetado como um microsserviço especializado em operações geográficas, com foco em:

- isolamento de responsabilidades
- alta precisão nos cálculos
- performance em operações intensivas
- reutilização por múltiplos serviços

---

## **4. Responsabilidades**

O serviço é responsável por:

- Validar e normalizar dados geoespaciais
- Executar cálculos espaciais
- Expor endpoints para consultas geográficas
- Suportar regras de negócio baseadas em localização
- Garantir consistência nos resultados entre diferentes consumidores

---

## **5. Funcionalidades Principais**

### **5.1 Processamento Geoespacial**

- Manipulação de coordenadas geográficas (latitude e longitude)
- Suporte a diferentes representações:
  - pontos
  - polígonos
  - regiões geográficas

- Conversão e validação de formatos geoespaciais

---

### **5.2 Consultas Espaciais**

- Verificação de pertencimento de ponto em área
- Detecção de interseção entre regiões
- Identificação de sobreposição parcial ou total
- Cálculo de distância entre coordenadas
- Consulta por raio (eventos dentro de uma área circular)

---

### **5.3 Integração com Domínio de Risco**

- Fornecimento de dados espaciais para avaliação de risco
- Aplicação de critérios de severidade baseados em localização
- Suporte ao **Risk Catalog Service** em decisões espaciais

---

## **6. Arquitetura**

### **Características**

- Microsserviço independente
- Comunicação via HTTP/REST
- Stateless
- Foco em operações computacionalmente intensivas isoladas do domínio principal

### **Fluxo de Processamento**

1. Serviço consumidor envia dados ou critérios geoespaciais
2. O Geospatial Service valida os dados recebidos
3. Executa os cálculos espaciais necessários
4. Retorna o resultado ao serviço solicitante

---

## **7. Integrações**

### **Serviços Consumidores**

- Ingestion Service
- Risk Catalog Service
- Outros serviços que dependam de análises geográficas

### **Responsabilidade sobre Dados**

- Não atua como fonte de verdade de eventos
- Funciona como serviço de apoio computacional
- Persistência limitada a cenários de otimização (cache ou pré-processamento)

---

## **8. Persistência e Performance**

- Utilização de bancos com suporte a operações geoespaciais, quando necessário
- Uso de cache para consultas recorrentes
- Estratégias para evitar recomputação de cálculos já realizados
- Possibilidade de pré-processamento de áreas complexas

---

## **9. Considerações Técnicas**

- Validação rigorosa de coordenadas inválidas ou fora de padrão
- Controle de precisão numérica em cálculos espaciais
- Tratamento explícito de casos de borda:
  - pontos em limites de polígonos
  - sobreposições parciais

- Design orientado à extensibilidade para novos tipos de consultas

---

## **10. Benefícios**

- Centralização da lógica geoespacial
- Redução de acoplamento entre serviços
- Consistência nos cálculos e resultados
- Facilidade de manutenção e evolução
- Base sólida para análises avançadas de risco geográfico

---

## **11. Status**

- Serviço definido
- Responsabilidades claramente delimitadas
- Arquitetura preparada para expansão conforme novos requisitos geoespaciais surgirem
