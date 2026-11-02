---
name: create-api-endpoint
description: Cria um endpoint REST v1 em um serviço do Sentinel (rota /api/v1/<serviço>/<recurso> em kebab-case, DTOs, envelope ResponseBaseDTO<T>, Problem Details, paginação, autorização por papel e validação de tenant) com testes. Use ao adicionar ou alterar uma rota HTTP pública.
argument-hint: <serviço> <MÉTODO> <recurso>
---

# Criar endpoint REST

## 1. Contexto

1. Leia `docs/constitution.md` §7 e `.claude/rules/api-design.md`; para o serviço, `docs/services/<Serviço>.md`
   e as rotas/códigos de erro em `docs/compliance/services-validation.md` §4.2.
2. Use o controller v1 do próprio serviço como modelo (ex.:
   `services/alert-orchestrator/src/AlertOrchestrator.Api/Controllers/AlertWindowsV1Controller.cs`).
3. Rotas antigas (sem `v1`) estão congeladas e depreciadas (ADR-0007): comportamento novo só em v1.

## 2. Contrato

- Rota: `api/v1/<serviço>/<recurso-kebab>` (constante `BasePath` no controller). Recursos no plural, ids na rota.
- Método HTTP pela semântica: GET sem efeito colateral e sem corpo; POST cria (201 + `Location` quando houver
  recurso); PUT substitui; PATCH altera parcialmente; DELETE remove (204 sem corpo).
- DTOs explícitos de entrada e saída em Application (ou `Api/Contracts` para formas só de transporte). Nunca
  exponha entidade de domínio, modelo de persistência ou tipo de infraestrutura.
- Sucesso com corpo: `ResponseBaseDTO<T> { StatusCode, Success = true, Data }` com `StatusCode` igual ao status
  HTTP. 204 não usa envelope.
- Erro: Problem Details (RFC 9457) via `ApiProblems.Create(HttpContext, status, "CODIGO_ESTAVEL", "mensagem")`
  com extensões `code` e `traceId`. Códigos em `UPPER_SNAKE_CASE`, reutilizando os existentes
  (`VALIDATION_ERROR`, `TENANT_ACCESS_DENIED`, `<RECURSO>_NOT_FOUND`).
- Coleções: `page`/`pageSize` via `Contracts/Pagination.TryResolve` do serviço (se não houver, copie o de
  alert-orchestrator; padrão 10, máximo 50, início em 1), ordenação
  determinística com desempate por id, resposta com página, tamanho, total de itens e de páginas.
- Datas ISO 8601 em UTC; datas sem hora em `YYYY-MM-DD`.

## 3. Segurança

- `[Authorize(Policy = <Serviço>Policies.Read|Write|Admin)]` em cada ação; nada de `AllowAnonymous`.
- Tenant: obtenha da claim validada via `TenantAccessEvaluator` (`CanAccessTenant`, `TenantIds`,
  `IsServicePrincipal`). Um `tenantId` em rota, query ou header só seleciona o tenant; a autorização vem das
  claims. GET nunca recebe tenant no corpo.
- Aplique o escopo de tenant na consulta (repositório/consulta recebe o escopo), não depois de carregar.
  Recurso de outro tenant responde como inexistente (404), conforme o padrão do serviço.
- Valide rota, query e corpo explicitamente (sem biblioteca externa de validação), retornando 400 com erros por
  campo. Detalhes completos na skill `implement-multitenancy`.

## 4. Implementação

1. Caso de uso em Application (serviço de aplicação ou handler MediatR, conforme o serviço; skill
   `cqrs-implementation`). O controller só traduz HTTP ↔ caso de uso: sem regra de negócio, sem repositório.
2. Controller com `[ProducesResponseType]` de cada status (sucesso e `application/problem+json`) e XML doc
   `<summary>`; OpenAPI/Scalar é gerado a partir disso.
3. Se a rota passar pelo gateway, atualize `services/api-gateway/ApiGateway.Api/ocelot.json`.
4. Atualize `docs/services/<Serviço>.md` e `docs/compliance/services-validation.md` §4.2 com rota e códigos.

## 5. Testes (obrigatórios)

Integração com a `WebApplicationFactory` do serviço (`tests/<Serviço>.IntegrationTests/Support`) e tokens de
`TestTokens`:
- 401 sem token; 403 com papel insuficiente; 200/201/204 no caminho feliz com o envelope correto.
- Tenant: usuário do tenant A não lê nem altera recurso do tenant B; principal de serviço conforme a regra.
- 400 com erros por campo; paginação nos limites (0, 1, 50, 51).
- Unitários para a regra do caso de uso.

Para endpoints novos com dados por tenant, peça ao subagent `security-auditor` uma revisão do endpoint.
