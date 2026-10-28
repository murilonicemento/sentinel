# ADR-0002: Autenticação de usuários e de serviços nas APIs do Sentinel

- **Status:** Proposto
- **Data:** 2026-10-24
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (achados X-01, ING-01, ING-03, REP-01, REP-02, TB-01, CH-02, RE-02, AO-02, GEO-02), `ADR-0001` (seção 7, autenticação do audit-compliance)

## Contexto

A auditoria de conformidade encontrou seis serviços e o gateway sem autenticação, um serviço protegido apenas por uma API key compartilhada (geospatial) e chaves de assinatura JWT versionadas em `appsettings.json` (ingestion e risk-catalog). As regras do projeto (`.claude/rules/security.md`, `.claude/rules/api-design.md`) exigem autenticação no servidor, negação por padrão, tenant derivado da identidade autenticada e nenhum segredo versionado.

A plataforma não tem provedor de identidade nem emissor de tokens. Cinco serviços chamam outros de forma síncrona:

| Chamador | Chamado |
|---|---|
| ingestion | tenants-billing, geospatial |
| alert-orchestrator | tenants-billing |
| channels | tenants-billing |
| risk-catalog | geospatial |
| risk-evaluation | risk-catalog (geospatial removido em 2026-10-27, ver abaixo) |

## Decisões

### 1. Tokens de usuário: JWT HS256 com o modelo do audit-compliance

Todos os serviços validam JWT com `Jwt:Issuer`, `Jwt:Audience` e `Jwt:SigningKey` (mínimo de 32 caracteres), carregados em opções tipadas com `ValidateOnStart`. A validação exige assinatura, emissor, audiência e expiração, aceita apenas HS256, desliga o mapeamento de claims de entrada e usa as claims `sub` (nome) e `role` (papéis). O tenant da identidade vem da claim multivalorada `tenant_id`.

A chave de assinatura é fornecida por variável de ambiente, User Secrets ou cofre, nunca por arquivo versionado.

Para não invalidar tokens emitidos antes desta decisão, papéis recebidos na claim longa `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` (`ClaimTypes.Role`) são copiados para a claim `role` durante a validação. A forma canônica é `role`.

### 2. Negação por padrão

Cada serviço registra uma política de fallback que exige identidade autenticada. Só ficam anônimos, de forma explícita, os endpoints de health check e a documentação OpenAPI/Scalar em ambiente de desenvolvimento.

### 3. Tokens de serviço: chave própria por chamador

Cada serviço chamador assina seu próprio JWT de serviço com uma chave exclusiva (`ServiceToken:SigningKey`), um emissor próprio (`ServiceToken:Issuer`, por exemplo `sentinel-service:ingestion`) e os papéis de que precisa (`ServiceToken:Roles`). O token tem vida curta (5 minutos), é reutilizado até perto da expiração e é anexado por um `DelegatingHandler` no `HttpClient` do chamador.

O serviço chamado registra os chamadores confiáveis em `Jwt:TrustedServices`, cada um com `Issuer`, `SigningKey` e `AllowedRoles`. A chave de validação é escolhida pelo emissor do token.

Cada chamador envia o mesmo token a todos os serviços que chama, então o token carrega papéis de vários serviços. O serviço chamado descarta todo papel que não esteja em `AllowedRoles` para aquele chamador. Um papel de outro serviço não tem efeito, e um papel não concedido (por exemplo `Admin`) também não, o que impede a escalada sem quebrar a chamada.

**Alternativas consideradas:**

- **Chave única compartilhada por todos os serviços.** Mais simples, mas qualquer serviço comprometido poderia emitir tokens de usuário de qualquer tenant e tokens de qualquer outro serviço.
- **Provedor de identidade com client credentials e chaves assimétricas** (por exemplo Keycloak no `docker-compose`). É a solução correta de longo prazo, mas introduz nova infraestrutura e depende de aprovação própria.
- **API key por serviço.** Um segundo mecanismo de autenticação para manter e rotacionar, sem expiração nem papéis.

### 4. Papéis e políticas

Cada serviço define papéis `<Serviço>.Admin`, `<Serviço>.Read` e `<Serviço>.Write` (os nomes já usados por ingestion, risk-catalog e audit-compliance são mantidos) e políticas de leitura e escrita. Tokens de serviço recebem apenas os papéis estritamente necessários, por exemplo `TenantsBilling.Read` para consultar tenants.

### 5. Contexto de tenant

Operações sobre dados de um tenant conferem o tenant informado (rota, query, cabeçalho ou corpo, conforme o contrato atual) com as claims `tenant_id` da identidade. Tenant não pertencente à identidade resulta em `403`. Consultas que retornam coleções filtram o resultado pelos tenants da identidade; um recurso de outro tenant consultado por id é tratado como inexistente, para não revelar sua existência.

Tokens de serviço não carregam tenant e atuam em escopo de plataforma: passam pela checagem de tenant, mas só dentro dos papéis que o serviço chamado lhes concedeu.

### 6. Ativação imediata

A autenticação passa a valer em todos os endpoints de uma vez, porque ainda não há consumidores externos. Os chamadores internos são ajustados no mesmo trabalho. É uma mudança incompatível para quem chamava as APIs sem token, inclusive pelo `api-gateway`, que apenas repassa o cabeçalho `Authorization`.

### 7. Código replicado por serviço

O repositório ainda não tem bibliotecas compartilhadas (`CLAUDE.md`), então cada serviço recebe sua própria cópia da configuração de autenticação e do emissor de tokens de serviço, como já acontece com `TenantBillingGateway` e `ExceptionHandlingMiddleware`.

## Consequências

- Chamadas sem token ou com token inválido passam a receber `401`, e chamadas sem o papel ou o tenant necessários recebem `403`.
- O `api-gateway` não valida tokens; a validação continua em cada serviço.
- Cada chamador e cada serviço chamado ganham segredos novos, que precisam existir em todos os ambientes. A falta de qualquer um deles impede o serviço de subir. No `docker-compose`, eles vêm de `platform/.env` (`JWT_SIGNING_KEY` e `<SERVIÇO>_SERVICE_TOKEN_SIGNING_KEY`); para execução local fora do compose, de User Secrets.
- O `audit-compliance` passa a usar `JWT_SIGNING_KEY` quando `AUDIT_COMPLIANCE_JWT_SIGNING_KEY` não estiver definida, para que um token de usuário valha em todos os serviços.
- A chave de um chamador comprometido permite se passar apenas por ele, e só com os papéis que o serviço chamado lhe concede.
- Com HS256, quem valida tokens de usuário também consegue assiná-los. Esse risco é aceito temporariamente e motiva a migração para um provedor de identidade com chaves assimétricas.
- A configuração de autenticação replicada em cada serviço precisa ser mantida em sincronia até existir uma biblioteca compartilhada.

## Decisões ainda pendentes

- Provedor de identidade da plataforma e migração para chaves assimétricas.
- Procedimento e periodicidade de rotação das chaves de usuário e de serviço.
- Biblioteca compartilhada para a configuração de autenticação.
- Autenticação e limitação de taxa no `api-gateway`.

## Atualização (2026-10-27)

risk-evaluation deixou de chamar outros serviços (RE-14): a única chamada usada ia para um endpoint inexistente do geospatial e os métodos do cliente de risk-catalog nunca eram chamados. O token de serviço do risk-evaluation (`ServiceToken:*`, `RISK_EVALUATION_SERVICE_TOKEN_SIGNING_KEY`) e as entradas `Jwt:TrustedServices` que confiavam nele em risk-catalog e geospatial foram removidos, para não manter uma credencial confiável sem uso. Se a avaliação voltar a chamar outro serviço, siga a seção 2 novamente.

## Atualização (2026-10-27, RE-15)

risk-evaluation voltou a chamar o risk-catalog, apenas para copiar a versão publicada do catálogo (listagens paginadas de regiões, critérios de severidade e matriz). O token de serviço volta com um único papel, `RiskCatalog.Read`, e só o risk-catalog confia nele; o geospatial continua sem essa entrada.
