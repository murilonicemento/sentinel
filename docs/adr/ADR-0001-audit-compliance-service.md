# ADR-0001: Implementação inicial do Audit & Compliance Service

- **Status:** Proposto
- **Data:** 2026-10-24
- **Responsável pela decisão:** a definir (responsável pelo serviço `audit-compliance`)
- **Relacionados:** `docs/services/Compliance-Audit.md` (especificação), `docs/audit-compliance-service-status.md` (estado da implementação)

## Contexto

A especificação do serviço (`docs/services/Compliance-Audit.md`, seção 17) lista decisões arquiteturais pendentes que precisavam de uma escolha para que a primeira versão pudesse ser implementada. Este ADR registra as escolhas adotadas na implementação inicial. Elas devem ser revisadas e aprovadas antes de qualquer implantação em produção.

## Decisões

### 1. Armazenamento principal: MongoDB 7

- Banco próprio `AuditComplianceDatabase`, com as coleções `auditRecords` e `auditExports` e o bucket GridFS `auditExportFiles`.
- MongoDB já faz parte da plataforma (`platform/docker-compose.yml`) e atende à estrutura variável de metadados prevista na especificação.
- `Metadata`, `BeforeState` e `AfterState` são persistidos como **texto JSON**, para preservar exatamente o conteúdo sobre o qual o hash de integridade é calculado. A contrapartida é que não é possível consultar campos internos dos metadados. Os filtros da especificação usam apenas campos de primeiro nível.
- As gravações usam write concern `majority` com journaling. O offset Kafka só é confirmado depois da gravação durável.

**Alternativas consideradas:** PostgreSQL com JSONB. Ofereceria permissões por tabela e constraints relacionais, mas a especificação propõe MongoDB e nenhum requisito medido justificou a troca.

### 2. Mensageria: Kafka, com tópicos dedicados

| Tópico                | Papel                                                                              |
| --------------------- | ---------------------------------------------------------------------------------- |
| `audit-events`        | Entrada principal de eventos auditáveis.                                            |
| `audit-events-replay` | Reprocessamento controlado (mesmo pipeline do tópico principal).                    |
| `audit-events-dlq`    | Mensagens rejeitadas, com o payload original e o código do erro.                    |

Kafka já é o transporte adotado pelo Sentinel. RabbitMQ, citado na especificação, foi descartado para não introduzir nova tecnologia.

### 3. Contrato de evento: envelope v1

Envelope JSON com `eventId`, `eventType`, `eventVersion`, `sourceService` e `occurredAt` obrigatórios. Os campos opcionais são `tenantId`, `correlationId`, `causationId`, `action`, `actor { type, id }`, `resource { type, id }`, `metadata`, `beforeState` e `afterState`. Os detalhes estão em `docs/audit-compliance-service-status.md`.

- `occurredAt` exige offset explícito (`Z` ou `±hh:mm`).
- `eventType` precisa existir no catálogo (`AuditEventCatalog`). `eventVersion` precisa estar entre 1 e a maior versão suportada para o tipo. Hoje todos os tipos suportam somente a versão 1.
- Campos desconhecidos são ignorados, para permitir evolução aditiva.
- Mensagens malformadas, com tipo desconhecido, com versão não suportada ou que violem limites vão para `audit-events-dlq` e nunca são descartadas silenciosamente.

### 4. Deduplicação e idempotência

- O escopo de unicidade é o par **(`SourceService`, `EventId`)**, garantido pelo índice único `ux_source_service_event_id`. Assim, dois serviços podem usar o mesmo `EventId` sem colisão.
- Uma violação de unicidade é tratada como entrega duplicada: a mensagem é confirmada e o registro não é gravado de novo.
- Não há verificação em memória. O índice persistente é a única garantia, e ela vale também para instâncias concorrentes.

### 5. Política de falhas do consumidor

- Entrega **pelo menos uma vez**, com processamento idempotente. Não há garantia de "exatamente uma vez".
- Falhas transitórias (MongoDB indisponível, timeouts, falhas de rede ou do Kafka) são retentadas até 3 vezes, com backoff exponencial e jitter. Se continuarem, o consumidor volta ao mesmo offset e pausa (`Kafka:StorageUnavailablePauseSeconds`), sem confirmar a mensagem e sem pular as seguintes. Com isso a partição fica parada enquanto o armazenamento estiver indisponível, o que é intencional: perder eventos de auditoria é pior do que atrasá-los.
- Erros inesperados e não transitórios vão para o dead-letter com o código `UNEXPECTED_PROCESSING_ERROR`, para não bloquear a partição indefinidamente.

### 6. Imutabilidade e integridade

- O repositório é append-only e não expõe operações de atualização ou exclusão de registros.
- O usuário do banco (`audit_compliance_app`, criado em `platform/init-db/mongodb/1-database.js`) recebe um papel customizado **sem `update`/`remove` em `auditRecords`**. Isso foi verificado manualmente: com esse usuário, `updateOne`, `deleteOne` e `drop` retornaram `Unauthorized`.
- Cada registro recebe um hash SHA-256 (`sha256:<hex>`) calculado sobre uma representação canônica. A leitura detalhada recalcula o hash e retorna `integrityVerified`.
- **Limitação conhecida:** um hash isolado não protege contra quem tem privilégio de escrita no banco, porque essa pessoa pode alterar o registro e recalcular o hash. Encadeamento de hashes, assinatura digital ou ancoragem externa **não** foram implementados e continuam pendentes (especificação, seção 17, item 6).

### 7. Autenticação, autorização e tenant

- JWT Bearer HS256, validando emissor, audiência, assinatura e expiração, no mesmo padrão de `ingestion` e `risk-catalog`. A chave (`Jwt:SigningKey`, com no mínimo 32 caracteres) precisa ser fornecida por User Secrets, variável de ambiente ou cofre, e a aplicação não inicia sem ela.
- Papéis (claim `role`):

  | Papel | Concede |
  | --- | --- |
  | `Audit.Read` | Consulta de registros |
  | `Audit.Sensitive` | Leitura de `beforeState` e `afterState` |
  | `Audit.Export` | Solicitação, consulta e download de exportações |
  | `Audit.Admin` | Todas as permissões acima |

- Contexto de tenant: o cliente informa o cabeçalho `X-Tenant-Id`, e o valor precisa constar nas claims multivaloradas `tenant_id` do token. Caso contrário, a resposta é `403 TENANT_ACCESS_DENIED`. O tenant informado pelo cliente nunca é aceito sem essa validação.
- Toda consulta é filtrada pelo tenant validado. Registros sem `TenantId` (eventos de plataforma) **não** ficam acessíveis pela API nesta versão.
- O nome da claim `tenant_id` foi definido aqui porque a plataforma ainda não tem um provedor de identidade com claims de tenant. Ele deve ser confirmado quando esse provedor existir.

### 8. Exportações

- O processamento é assíncrono (`POST` retorna `202 Accepted`), feito por um worker que reivindica a exportação de forma atômica (`findOneAndUpdate` com lease). O número de tentativas funciona como token de exclusividade, então é seguro rodar várias instâncias.
- Limites configuráveis: até 10.000 registros (`Exports:MaxRecords`) e intervalo máximo de 366 dias. Uma solicitação que ultrapassa o limite é rejeitada com `422 EXPORT_LIMIT_EXCEEDED`.
- O arquivo fica no GridFS do próprio banco, expira em 24 horas e é removido pelo worker. O download envia `Cache-Control: no-store`.
- Solicitação e download geram registros de auditoria (`AuditExportRequested` e `AuditExportDownloaded`) **antes** de o acesso ser concedido.
- Os estados anterior e posterior nunca são exportados. No CSV, valores iniciados por `= + - @` são neutralizados para evitar injeção de fórmulas.

**Alternativa considerada:** MinIO, que já faz parte da plataforma. Exigiria um novo SDK e credenciais adicionais. O GridFS atende ao volume limitado das exportações sem nova dependência e pode ser substituído pela implementação de `IExportFileStorage`.

### 9. Minimização de dados

Em `metadata`, `beforeState` e `afterState`:

- Valores de chaves sensíveis são substituídos por `[REDACTED]`. Isso cobre credenciais (password, secret, token, apiKey, credential, privateKey, authorization, connectionString, cookie) e dados pessoais diretos (email, phone, cpf, rg, cardNumber, cvv).
- Limites aplicados: profundidade 5, 50 propriedades por objeto, 50 itens por array, 1.024 caracteres por string e 16 KB por campo.

A lista de chaves é conservadora e pode mascarar campos legítimos, como `tokenCount`. Ela deve ser revisada com os serviços de origem.

### 10. Versionamento da rota

A rota base é `/api/v1/audit-compliance`, conforme `.claude/rules/api-design.md` ("URL path versioning using `/api/v1/<service-name>`"). A especificação e `docs/constitution.md` §7.1 citam `/api/audit-compliance`. **Esse conflito entre documentos precisa ser resolvido pelo responsável pela arquitetura.**

## Consequências

- O serviço pode receber e consultar eventos, mas **nenhum serviço de origem publica em `audit-events` ainda**. A publicação, de preferência com Transactional Outbox, cabe aos serviços de origem e precisa ser implementada em cada um.
- A autenticação dos produtores no Kafka (SASL/ACL) não existe na plataforma local. Por isso, o campo `sourceService` do envelope não prova a origem da mensagem (especificação, seção 11).
- A retenção e o arquivamento dos registros de auditoria **não** foram implementados. Continuam pendentes por dependerem de definição legal e de negócio (especificação, seção 17, itens 8 e 9).
- Em produção, os índices devem ser provisionados pelo procedimento de implantação, porque `MongoDb:EnsureIndexesOnStartup` fica desabilitado por padrão.

## Decisões ainda pendentes

1. Aprovação deste ADR e definição do responsável.
2. Política de retenção por categoria e por tenant, e critérios de arquivamento e eliminação.
3. Proteção contra adulteração privilegiada (encadeamento de hashes, assinaturas, WORM ou ancoragem externa).
4. Autenticação de produtores no Kafka.
5. Provedor de identidade definitivo, nomes de claims e uso de chaves assimétricas.
6. Acesso a registros sem tenant (eventos de plataforma).
7. Requisitos de disponibilidade e atraso máximo de ingestão, que devem orientar os limiares de alerta.
8. Rota versionada versus não versionada (decisão 10).
