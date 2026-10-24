# Auditoria & Conformidade Service

## 1. Contexto

O **Auditoria & Conformidade Service** é responsável por receber, persistir, proteger e disponibilizar registros de auditoria referentes a ações administrativas, operações de negócio e eventos relevantes dos serviços do Sentinel.

O serviço fornece uma trilha de auditoria centralizada, permitindo reconstruir acontecimentos, identificar atores envolvidos, correlacionar operações distribuídas e fornecer evidências para investigações técnicas, auditorias internas e requisitos de conformidade.

Como parte de uma arquitetura de microserviços, o serviço mantém seu próprio domínio e armazenamento, sem assumir a responsabilidade pelas regras de negócio ou pelo estado interno dos demais serviços.

A auditoria deve ser confiável, rastreável e resiliente. Falhas de processamento precisam ser detectáveis e recuperáveis, evitando que a indisponibilidade do serviço provoque perda silenciosa de eventos.

## 2. Objetivos

1. Registrar ações e eventos relevantes para auditoria.
2. Garantir a integridade e a proteção contra alterações não autorizadas dos registros.
3. Permitir a rastreabilidade de operações, usuários, serviços e tenants.
4. Fornecer evidências para investigações, auditorias internas e processos de conformidade.
5. Integrar-se à observabilidade e aos mecanismos de segurança do Sentinel.
6. Disponibilizar consultas e exportações controladas dos registros.
7. Garantir processamento idempotente e recuperação de falhas na ingestão.
8. Aplicar políticas de retenção, proteção de dados e controle de acesso.

## 3. Escopo e Responsabilidades

### 3.1. Responsabilidades

O serviço deve:

- Consumir eventos auditáveis publicados pelos demais serviços.
- Validar o envelope e a estrutura dos eventos recebidos.
- Persistir os registros de auditoria de maneira durável.
- Detectar e tratar mensagens duplicadas.
- Preservar a identidade do ator, a origem e o contexto da operação.
- Correlacionar eventos por identificadores de rastreamento.
- Disponibilizar consultas paginadas e autorizadas.
- Gerar exportações em formatos suportados.
- Aplicar políticas de retenção e arquivamento.
- Expor métricas, logs operacionais e traces para observabilidade.
- Detectar falhas de ingestão, inconsistências e atrasos no processamento.

### 3.2. Fora do escopo

O serviço não deve:

- Executar ou alterar operações de negócio dos serviços de origem.
- Manter ou administrar os dados cadastrais de tenants e usuários.
- Substituir o sistema de logs operacionais, métricas ou tracing distribuído.
- Ser utilizado como mecanismo de autorização em tempo real.
- Modificar registros históricos para corrigir informações incorretas.
- Garantir conformidade regulatória apenas pela existência de registros.
- Armazenar indiscriminadamente payloads completos, credenciais ou dados pessoais.

Correções em registros devem ser representadas por novos eventos, quando aplicável, preservando a relação com o registro original e o histórico das correções.

## 4. Bounded Context

O serviço pertence ao domínio de **Auditoria e Conformidade** e possui responsabilidades independentes dos demais bounded contexts do Sentinel.

Seu modelo de domínio deve representar evidências de ações e acontecimentos, e não as entidades de negócio completas dos serviços de origem.

### 4.1. Isolamento

- O serviço deve possuir banco de dados próprio.
- Nenhum outro serviço deve acessar diretamente seu armazenamento.
- O acesso aos registros deve ocorrer por contratos de API ou mecanismos de exportação autorizados.
- As dependências entre serviços devem ser estabelecidas por contratos explícitos.
- O serviço não deve exigir consultas síncronas aos serviços de origem para persistir um evento já recebido.

### 4.2. Consistência

A ingestão será predominantemente assíncrona.

O serviço não garante consistência imediata entre a ocorrência de uma ação e sua disponibilização para consulta. O atraso entre publicação, consumo, persistência e indexação deve ser monitorado.

Quando um requisito exigir garantia de auditoria antes da confirmação de uma operação de negócio, esse requisito deverá ser tratado explicitamente pelo serviço de origem e por seu mecanismo de publicação. A arquitetura assíncrona, isoladamente, não fornece essa garantia.

## 5. Modelo de Domínio

### 5.1. AuditRecord

`AuditRecord` representa um registro imutável de uma ação ou evento relevante para auditoria.

A estrutura proposta contém:

| Campo           | Descrição                                                                      |
| --------------- | ------------------------------------------------------------------------------ |
| `Id`            | Identificador único do registro de auditoria.                                  |
| `TenantId`      | Identificador do tenant ao qual o evento pertence, quando aplicável.           |
| `EventId`       | Identificador estável do evento original, utilizado na deduplicação.           |
| `EventType`     | Tipo semântico do evento.                                                      |
| `EventVersion`  | Versão do contrato do evento.                                                  |
| `Action`        | Descrição padronizada da ação registrada.                                      |
| `Actor`         | Identidade do usuário, serviço ou sistema responsável pela ação.               |
| `SourceService` | Serviço responsável pela emissão do evento.                                    |
| `OccurredAt`    | Instante em que a ação ocorreu na origem.                                      |
| `RecordedAt`    | Instante em que o registro foi persistido pela auditoria.                      |
| `CorrelationId` | Identificador para correlacionar operações relacionadas.                       |
| `CausationId`   | Identificador do evento ou comando que originou a operação, quando disponível. |
| `ResourceType`  | Tipo do recurso afetado, quando aplicável.                                     |
| `ResourceId`    | Identificador do recurso afetado, quando aplicável.                            |
| `Metadata`      | Metadados adicionais permitidos pelo contrato do evento.                       |
| `BeforeState`   | Estado anterior, somente quando necessário e autorizado.                       |
| `AfterState`    | Estado posterior, somente quando necessário e autorizado.                      |
| `IntegrityHash` | Evidência criptográfica de integridade, quando implementada.                   |
| `SchemaVersion` | Versão do esquema de persistência do registro.                                 |

Os campos opcionais devem ser representados explicitamente nos contratos e nas regras de validação.

Os timestamps devem utilizar uma representação consistente, preferencialmente UTC, com precisão definida pelo contrato.

### 5.2. Actor

O ator identifica quem ou o que realizou a ação.

Tipos previstos:

- `User`: ação iniciada por um usuário autenticado.
- `Service`: ação iniciada por outro serviço autenticado.
- `System`: ação automática ou interna do sistema.

A estrutura pode conter um identificador estável, o tipo do ator e atributos mínimos necessários à auditoria.

A identidade do ator deve ser derivada de uma origem confiável. O serviço não deve aceitar uma identidade arbitrária enviada pelo cliente como prova de autoria.

### 5.3. Metadata e estados

`Metadata`, `BeforeState` e `AfterState` devem conter somente informações relevantes para o objetivo de auditoria.

Regras:

- Não registrar senhas, tokens, chaves secretas ou credenciais.
- Evitar a cópia integral de entidades ou requisições.
- Mascarar ou excluir dados pessoais desnecessários.
- Restringir tamanho, profundidade e tipos de valores permitidos.
- Versionar os contratos que definem a estrutura dos dados.
- Aplicar controle de acesso apropriado a informações sensíveis.

### 5.4. Agregados e invariantes

`AuditRecord` é o principal candidato a Aggregate Root do domínio.

Os invariantes devem garantir que:

- Todo registro possua identificador e tipo de evento válidos.
- O evento possua origem e contexto temporal identificáveis.
- A identidade do evento original seja preservada.
- Registros persistidos não sejam atualizados ou removidos por operações comuns da aplicação.
- Eventos duplicados não produzam múltiplos registros lógicos.
- Os metadados respeitem os limites e as políticas de proteção de dados.

A estratégia de deduplicação deve considerar o escopo de unicidade do `EventId`, incluindo a origem do evento quando necessário.

## 6. Catálogo de Eventos Auditáveis

Os eventos abaixo constituem o catálogo inicial proposto:

| Evento               | Contexto                                          |
| -------------------- | ------------------------------------------------- |
| `TenantCreated`      | Criação de tenant.                                |
| `TenantUpdated`      | Alteração de dados relevantes do tenant.          |
| `PlanChanged`        | Alteração de plano ou assinatura.                 |
| `AlertTriggered`     | Disparo de alerta.                                |
| `AlertDispatched`    | Despacho de alerta para um destino.               |
| `NotificationSent`   | Envio de notificação.                             |
| `NotificationFailed` | Falha no envio de notificação.                    |
| `RiskUpdated`        | Alteração de avaliação ou classificação de risco. |

O catálogo poderá ser ampliado para contemplar ações administrativas, alterações de permissões, operações críticas e falhas relevantes de segurança.

### 6.1. Contratos de eventos

Cada evento publicado deve conter um envelope padronizado com, no mínimo:

- Identificador único do evento.
- Tipo e versão do evento.
- Identificador do serviço de origem.
- Timestamp da ocorrência.
- Identificador de correlação, quando disponível.
- Identificador de causalidade, quando disponível.
- Contexto do tenant, quando aplicável.
- Identidade do ator, quando aplicável.
- Payload específico do evento.

Os contratos devem ser versionados e possuir regras explícitas de compatibilidade.

A ausência de campos opcionais não deve invalidar eventos quando o contrato permitir sua omissão. Campos obrigatórios ausentes, versões incompatíveis e payloads inválidos devem seguir uma política definida de rejeição, quarentena ou encaminhamento para tratamento de falhas.

## 7. Ingestão e Processamento

### 7.1. Fluxo de ingestão

1. Um serviço de origem executa uma operação relevante.
2. O serviço de origem publica o evento auditável.
3. O mecanismo de mensageria entrega o evento ao consumidor de auditoria.
4. O consumidor valida o envelope e o contrato.
5. O serviço verifica se o evento já foi processado.
6. O registro é persistido no armazenamento de auditoria.
7. A mensagem é confirmada conforme as garantias de persistência do mecanismo utilizado.
8. O registro fica disponível para consulta, respeitando o atraso de processamento e indexação.

A confirmação da mensagem deve ocorrer somente depois que a persistência durável estiver assegurada.

### 7.2. Idempotência

O processamento deve ser idempotente.

A repetição da entrega do mesmo evento não pode gerar registros lógicos duplicados.

A implementação deve utilizar uma chave de deduplicação estável e uma restrição de unicidade persistente. A verificação em memória, isoladamente, não é suficiente.

### 7.3. Falhas e recuperação

O serviço deve prever:

- Retentativas com backoff exponencial e limite de tentativas.
- Tratamento de falhas transitórias e permanentes.
- Dead-letter queue ou mecanismo equivalente para mensagens não processáveis.
- Registro de erros operacionais sem exposição de dados sensíveis.
- Reprocessamento controlado de mensagens.
- Monitoramento de mensagens pendentes e do tempo de processamento.
- Procedimentos para recuperação de indisponibilidade do armazenamento.

Mensagens inválidas não devem ser descartadas silenciosamente.

### 7.4. Garantias de entrega

A arquitetura deve priorizar entrega pelo menos uma vez, com processamento idempotente.

Não se deve declarar garantia de processamento exatamente uma vez sem comprovar que ela é suportada de ponta a ponta, incluindo mensageria, persistência e efeitos associados.

## 8. Persistência

### 8.1. Armazenamento principal

**MongoDB é a opção inicial proposta**, considerando a estrutura variável dos eventos e a possibilidade de armazenar metadados com esquemas diferentes.

A escolha deverá ser validada considerando:

- Volume e taxa de ingestão.
- Padrões de consulta.
- Requisitos de indexação.
- Garantias de durabilidade.
- Estratégia de particionamento.
- Custos operacionais e de retenção.
- Necessidade de recursos específicos de integridade e auditoria.

A tecnologia definitiva e as configurações de produção devem ser documentadas após a validação arquitetural.

### 8.2. Índices e consultas

Os índices devem ser definidos com base nos padrões de consulta previstos, considerando, inicialmente:

- `TenantId` e `OccurredAt`.
- `TenantId` e `EventType`.
- `TenantId` e `Actor`.
- `TenantId` e `ResourceId`.
- `EventId` ou a chave composta utilizada para deduplicação.

A combinação exata dos índices deverá ser validada por testes de desempenho e análise de cardinalidade.

Consultas devem utilizar paginação, limites de resultados e filtros explícitos para evitar varreduras desnecessárias em grandes volumes de dados.

### 8.3. Imutabilidade e integridade

Os registros devem ser tratados como append-only pela aplicação.

A proteção contra adulteração deve combinar controles apropriados, como:

- Ausência de operações de atualização e exclusão nos repositórios comuns.
- Permissões de banco restritas ao necessário.
- Separação de privilégios administrativos.
- Trilhas de acesso e monitoramento de operações privilegiadas.
- Hashes criptográficos para verificação de integridade.
- Encadeamento de hashes ou assinaturas digitais, quando exigido pelo nível de garantia.
- Backups protegidos e testes periódicos de restauração.

Um hash isolado não impede a adulteração: alguém com privilégios suficientes pode alterar o registro e recalcular seu hash.

Quando houver necessidade de evidência contra administradores com acesso privilegiado, deverá ser avaliado o uso de armazenamento imutável, ancoragem externa de hashes ou mecanismos equivalentes.

## 9. Retenção e Conformidade

### 9.1. Política de retenção

A retenção deve ser definida de acordo com requisitos legais, contratuais, operacionais e de negócio.

Os períodos de 30 dias, 90 dias e um ano ou mais são propostas iniciais de configuração, não prazos universais de conformidade.

A política deverá especificar:

- Categorias de eventos e seus períodos de retenção.
- Regras por tenant, quando aplicáveis.
- Critérios de arquivamento.
- Condições de eliminação ou anonimização.
- Tratamento de obrigações legais de preservação.
- Responsabilidades e autorização para execução das políticas.
- Evidências de execução e verificação das rotinas.

### 9.2. LGPD e proteção de dados

O serviço deve observar os princípios aplicáveis da LGPD, incluindo finalidade, adequação, necessidade, segurança e responsabilização.

Regras:

- Coletar apenas dados necessários para a finalidade de auditoria.
- Evitar dados pessoais em metadados livres.
- Aplicar controle de acesso e criptografia apropriados.
- Definir critérios para atendimento de solicitações relacionadas a dados pessoais.
- Avaliar anonimização, pseudonimização ou eliminação quando aplicável.
- Preservar dados sujeitos a obrigação legítima de retenção somente pelo período necessário.

A imutabilidade não deve ser interpretada como justificativa automática para conservar indefinidamente todos os dados pessoais.

Sempre que possível, a trilha de auditoria deve referenciar recursos por identificadores estáveis, evitando duplicar informações pessoais que possam ser alteradas ou eliminadas no serviço de origem.

### 9.3. Consultas e exportações

Consultas e exportações devem respeitar as mesmas regras de autorização, finalidade e minimização aplicáveis ao acesso direto aos registros.

Exportações devem possuir limites de volume, auditoria de acesso e controles contra exposição indevida de dados.

## 10. API de Consulta

A API será destinada à consulta de registros de auditoria e à geração de exportações autorizadas.

Base path:

`/api/audit-compliance`

Endpoints propostos:

| Método | Endpoint                             | Finalidade                                   |
| ------ | ------------------------------------ | -------------------------------------------- |
| `GET`  | `/api/audit-compliance/records`      | Consultar registros com filtros e paginação. |
| `GET`  | `/api/audit-compliance/records/{id}` | Consultar um registro específico.            |
| `POST` | `/api/audit-compliance/exports`      | Solicitar uma exportação.                    |
| `GET`  | `/api/audit-compliance/exports/{id}` | Consultar o estado de uma exportação.        |

Os endpoints são propostas de contrato e ainda precisam ser validados.

### 10.1. Filtros de consulta

A consulta poderá aceitar filtros por:

- Tenant.
- Intervalo de ocorrência.
- Tipo de evento.
- Ator.
- Serviço de origem.
- Tipo e identificador do recurso.
- Identificador de correlação.

As consultas devem utilizar intervalos temporais explícitos, limites máximos de paginação e ordenação determinística.

A API não deve confiar em um `TenantId` fornecido pelo cliente como prova de autorização. O acesso deve ser validado com base na identidade autenticada, nas permissões e no contexto de tenant autorizado.

Para operações que exigem contexto de tenant, esse contexto deve ser validado conforme as regras de segurança do Sentinel. Consultas não devem exigir `TenantId` no corpo de uma requisição `GET`.

### 10.2. Exportações

Formatos previstos:

- CSV.
- JSON.

Exportações de grande volume devem ser processadas de forma assíncrona.

Os arquivos gerados devem ter acesso restrito, prazo de validade e proteção adequada durante armazenamento e transferência. A geração e o download de exportações também devem produzir registros de auditoria quando forem relevantes para segurança ou conformidade.

## 11. Segurança

O serviço deve adotar os padrões de segurança definidos pelo Sentinel.

Requisitos:

- Autenticação de usuários e serviços consumidores.
- Autorização baseada em permissões explícitas.
- Isolamento entre tenants em todas as operações aplicáveis.
- Validação dos contratos de entrada.
- Proteção contra acesso horizontal indevido.
- Criptografia em trânsito e em repouso.
- Gerenciamento seguro de segredos.
- Mascaramento e minimização de dados sensíveis.
- Limitação de taxa e proteção contra consultas abusivas.
- Auditoria de operações administrativas e acessos sensíveis.
- Privilégios mínimos para aplicações, consumidores e operadores.

Eventos recebidos de outros serviços devem ser autenticados ou protegidos pelo mecanismo de confiança estabelecido na plataforma. A presença de um campo `SourceService` no payload não constitui, isoladamente, prova da origem da mensagem.

## 12. Observabilidade

A observabilidade deve permitir verificar a saúde do pipeline de ingestão, a integridade operacional e a disponibilidade das consultas.

### 12.1. Métricas

Monitorar, no mínimo:

- Quantidade de eventos recebidos e persistidos.
- Taxa de falhas de validação e persistência.
- Quantidade de eventos duplicados.
- Quantidade de mensagens em retentativa ou quarentena.
- Idade da mensagem mais antiga pendente.
- Latência entre ocorrência, recebimento e persistência.
- Latência e taxa de erro das consultas.
- Duração e falhas das exportações.
- Volume de armazenamento e crescimento dos índices.

### 12.2. Alertas

Configurar alertas para:

- Falhas persistentes de gravação.
- Crescimento anormal da fila de mensagens.
- Aumento de mensagens em dead-letter.
- Atraso excessivo na ingestão.
- Indisponibilidade do armazenamento.
- Falhas de exportação.
- Suspeitas de acesso não autorizado.
- Divergências identificadas por mecanismos de reconciliação.

### 12.3. Logs e tracing

Logs operacionais devem ser estruturados e não devem duplicar indiscriminadamente os dados armazenados nos registros de auditoria.

Quando disponíveis, `CorrelationId` e `CausationId` devem facilitar a correlação entre serviços.

O tracing distribuído deve complementar a trilha de auditoria, sem ser utilizado como substituto dela.

## 13. Escalabilidade e Desempenho

A arquitetura deve priorizar:

- Ingestão assíncrona.
- Processamento concorrente controlado.
- Escritas append-only.
- Índices alinhados aos padrões de consulta.
- Paginação e limites de resultados.
- Separação entre ingestão e consultas quando necessário.
- Arquivamento de registros históricos.
- Backpressure para evitar sobrecarga do armazenamento.

O particionamento por tempo ou tenant poderá ser adotado após a análise do volume e dos padrões de acesso.

A implementação deve evitar cardinalidade excessiva em métricas, crescimento descontrolado de índices e consultas sem limites.

## 14. Integrações

### 14.1. Tenancy Service

Fornece o contexto necessário para identificar tenants e validar informações de tenancy, conforme os contratos da plataforma.

A ingestão não deve depender de uma consulta síncrona ao Tenancy Service para cada evento quando o contexto já estiver disponível em uma origem confiável.

### 14.2. Serviços de origem

Os serviços responsáveis pelas ações de negócio devem publicar os eventos auditáveis de acordo com os contratos definidos.

Quando a perda de eventos for inaceitável, a publicação deve considerar o padrão Transactional Outbox ou mecanismo equivalente para evitar a inconsistência entre a gravação da operação de negócio e a publicação da mensagem.

### 14.3. Reporting Service

Poderá consumir dados históricos de auditoria por meio de contratos autorizados, APIs ou exportações.

O Reporting Service não deve consultar diretamente o banco de dados da auditoria.

### 14.4. Plataforma de mensageria

Kafka ou RabbitMQ são alternativas para transporte assíncrono.

A escolha deve considerar as tecnologias já adotadas pelo Sentinel, o volume esperado, a ordenação necessária, as garantias de entrega, a retenção de mensagens e a complexidade operacional.

### 14.5. Armazenamento de longo prazo

S3, Azure Blob Storage ou serviço equivalente poderá ser utilizado para arquivamento de registros históricos.

A integração deverá definir formato, criptografia, controle de acesso, política de retenção, verificação de integridade e procedimento de recuperação.

## 15. Arquitetura e Organização do Código

O serviço deve seguir os padrões de Clean Architecture e DDD adotados pelo Sentinel.

Estrutura proposta:

```text
services/audit-compliance/
├── src/
│   ├── AuditCompliance.Api/
│   ├── AuditCompliance.Application/
│   ├── AuditCompliance.Domain/
│   ├── AuditCompliance.Infrastructure/
│   └── AuditCompliance.Integration/
└── tests/
    ├── AuditCompliance.UnitTests/
    └── AuditCompliance.IntegrationTests/
```

A estrutura final deve seguir as convenções já estabelecidas no repositório.

### 15.1. Responsabilidades das camadas

**Domain**

- Entidades, agregados, value objects e invariantes.
- Regras de domínio independentes de infraestrutura.
- Contratos de domínio quando necessários.

**Application**

- Casos de uso de ingestão, consulta e exportação.
- Orquestração das operações.
- Validação de regras de aplicação.
- Abstrações de persistência e mensageria.

**Infrastructure**

- Implementações de persistência.
- Integração com MongoDB ou armazenamento escolhido.
- Consumidores de mensageria.
- Implementações de hash, arquivamento e serviços externos.

**Api**

- Endpoints REST.
- Autenticação, autorização e tratamento de requisições.
- Mapeamento entre contratos HTTP e casos de uso.

**Integration**

- Adaptadores e contratos de integração, quando a complexidade justificar uma camada específica.
- Transformação de mensagens externas para modelos internos.

A camada Domain não deve depender de Application, Infrastructure ou Api. Application depende de Domain; Infrastructure implementa as abstrações exigidas pela aplicação; Api depende dos componentes necessários para expor os casos de uso.

A camada Integration é opcional e não deve ser criada apenas para aumentar a quantidade de projetos.

### 15.2. Testes

O serviço deve possuir:

- Testes unitários das regras de domínio.
- Testes dos casos de uso.
- Testes de integração com persistência e mensageria.
- Testes de autorização e isolamento entre tenants.
- Testes de idempotência e entrega duplicada.
- Testes de recuperação após falhas.
- Testes de contratos de eventos.
- Testes de carga e concorrência para os fluxos críticos.

## 16. Regras de Negócio e Invariantes Operacionais

1. Registros persistidos são imutáveis pelas operações comuns da aplicação.
2. Eventos auditáveis devem possuir contratos explícitos e versionados.
3. Eventos duplicados não podem gerar registros lógicos duplicados.
4. A persistência deve preceder a confirmação de processamento da mensagem.
5. Falhas não podem resultar em descarte silencioso de eventos.
6. O processamento deve permitir recuperação e reprocessamento controlado.
7. Todo acesso aos registros deve respeitar autenticação, autorização e isolamento entre tenants.
8. Dados sensíveis devem ser minimizados e protegidos.
9. A retenção deve seguir políticas explícitas e verificáveis.
10. O serviço não deve alterar o estado de negócio dos serviços de origem.
11. Os registros devem preservar os identificadores de rastreamento disponíveis.
12. A indisponibilidade da auditoria deve ser tratada conforme a criticidade da operação de origem.

### 16.1. Política de falha

O requisito de não interromper o fluxo principal não implica que toda falha de auditoria possa ser ignorada.

Cada categoria de operação deve definir seu comportamento:

- **Operações não críticas:** podem prosseguir com publicação assíncrona e recuperação posterior, desde que exista um mecanismo confiável de persistência ou publicação.
- **Operações críticas:** podem exigir confirmação durável da intenção de auditoria antes de serem consideradas concluídas.
- **Falhas de mensageria ou persistência:** devem ser detectadas, monitoradas e tratadas por mecanismos de recuperação.

A classificação das operações e as garantias exigidas devem ser definidas pelos responsáveis pelos serviços de origem.

## 17. Decisões Arquiteturais Pendentes

Antes da implementação em produção, definir:

1. Tecnologia e configuração do armazenamento principal.
2. Tecnologia de mensageria e estratégia de publicação.
3. Contratos e versões dos eventos auditáveis.
4. Escopo de unicidade dos identificadores de eventos.
5. Estratégia de deduplicação e reprocessamento.
6. Requisitos de integridade criptográfica e proteção contra adulteração privilegiada.
7. Categorias de eventos e dados que podem ser armazenados.
8. Política de retenção por categoria e tenant.
9. Critérios de arquivamento e eliminação de dados.
10. Modelo de autorização das consultas e exportações.
11. Requisitos de disponibilidade, durabilidade e atraso máximo de ingestão.
12. Estratégia de particionamento e dimensionamento.
13. Critérios de reconciliação entre eventos de origem e registros persistidos.

As decisões devem ser documentadas e revisadas conforme os requisitos reais do Sentinel.

## 18. Plano de Implementação

### Fase 1: Contratos e domínio

- Definir o catálogo inicial de eventos.
- Definir os envelopes e as versões dos contratos.
- Implementar `AuditRecord` e seus invariantes.
- Definir a estratégia de deduplicação.
- Estabelecer as regras de minimização de dados.

### Fase 2: Ingestão e persistência

- Implementar o consumidor de eventos.
- Implementar a persistência durável.
- Implementar idempotência e restrições de unicidade.
- Configurar retentativas e dead-letter.
- Implementar testes de integração e recuperação.

### Fase 3: API e segurança

- Implementar endpoints de consulta.
- Implementar filtros, paginação e ordenação.
- Implementar autenticação, autorização e isolamento entre tenants.
- Definir e implementar exportações autorizadas.

### Fase 4: Operação e conformidade

- Implementar métricas, logs e traces.
- Configurar alertas e dashboards.
- Definir retenção e arquivamento.
- Validar backup, restauração e integridade.
- Executar testes de carga e resiliência.
- Documentar procedimentos operacionais.

## 19. Critérios de Aceitação

O serviço poderá ser considerado funcionalmente preparado para uma primeira implantação quando:

- Os eventos definidos no escopo forem aceitos e validados.
- Os registros persistidos puderem ser consultados por filtros autorizados.
- A repetição de mensagens não gerar duplicações lógicas.
- Falhas de persistência não provocarem confirmação prematura das mensagens.
- Mensagens inválidas ou irrecuperáveis forem identificadas e tratadas.
- O isolamento entre tenants for validado por testes.
- Os dados sensíveis forem tratados conforme as políticas definidas.
- As consultas tiverem paginação e limites de resultados.
- As métricas e os alertas essenciais estiverem disponíveis.
- Os testes de unidade e integração cobrirem os fluxos críticos.
- As políticas de retenção e recuperação estiverem documentadas.

Requisitos avançados de imutabilidade criptográfica, arquivamento de longo prazo e certificação de conformidade deverão possuir critérios de aceitação específicos antes de serem considerados implementados.

## 20. Considerações Finais

O Auditoria & Conformidade Service é um componente de infraestrutura de confiança do Sentinel. Seu valor não está apenas em armazenar eventos, mas em preservar evidências úteis, íntegras, protegidas e recuperáveis ao longo do tempo.

A qualidade da auditoria depende da confiabilidade dos serviços emissores, dos contratos de integração, da persistência durável, dos controles de acesso e da capacidade de detectar e corrigir falhas.

O objetivo é permitir que uma operação relevante possa ser investigada com clareza, identificando o que aconteceu, quando aconteceu, qual ator esteve envolvido e quais recursos foram afetados, sem transformar a auditoria em uma dependência indevida dos fluxos de negócio.

## 21. Estado da Implementação

A primeira versão está implementada em `services/audit-compliance`. As decisões tomadas para as pendências da seção 17 estão registradas em `docs/adr/ADR-0001-audit-compliance-service.md`, com status Proposto. O estado detalhado, os contratos, a configuração e os procedimentos operacionais estão em `docs/audit-compliance-service-status.md`.

Diferenças em relação às propostas deste documento:

- A rota base implementada é `/api/v1/audit-compliance`, seguindo a regra de versionamento de `.claude/rules/api-design.md`. Também foi adicionado o endpoint `GET /api/v1/audit-compliance/exports/{id}/download`.
- A camada opcional `AuditCompliance.Integration` não foi criada, porque não há complexidade de integração que a justifique.
