---
name: implement-multitenancy
description: Implementa ou corrige o isolamento multi-tenant em um serviço do Sentinel - tenant da claim tenant_id validada, principal de serviço, escopo aplicado em consultas, caches, índices, consumers e chamadas entre serviços, com testes de acesso cruzado. Use ao criar dados ou operações por tenant ou ao investigar risco de acesso entre tenants.
argument-hint: <serviço> [operação ou recurso]
---

# Isolamento multi-tenant

Princípio (constituição §7.4, §8.3): o `tenantId` enviado pelo cliente nunca autoriza nada sozinho. O servidor
decide a partir da identidade autenticada.

## 1. Modelo existente (não reinvente)

Leia `docs/adr/ADR-0002-service-authentication.md` e `Api/Security` do serviço:

- Token de usuário: emissor `Jwt:Issuer`, tenants na claim multivalorada `tenant_id`, papéis na claim `role`.
- Token de serviço: emissor próprio listado em `Jwt:TrustedServices`, papéis limitados a `AllowedRoles`, sem
  tenant (atua em nível de plataforma).
- `TenantAccessEvaluator`: `IsServicePrincipal(user)`, `CanAccessTenant(user, tenantId)`, `TenantIds(user)`.
  Cada serviço tem sua cópia; mantenha as cópias em sincronia ao alterar uma delas.
- Serviços que dependem do estado do tenant consultam tenants-billing pelo `TenantBillingGateway`.

## 2. Onde aplicar

| Ponto | Regra |
| --- | --- |
| Endpoint | Resolva o escopo no controller (`AccessScope()` = plataforma se principal de serviço, senão `ForTenants(TenantIds)`); recurso por id de outro tenant → 404 no padrão do serviço; tenant explícito na rota/query/header → `CanAccessTenant`, senão `TENANT_ACCESS_DENIED` |
| Application | Queries e commands recebem o escopo como parâmetro e o repassam; nenhum caso de uso lê `HttpContext` |
| Repositório / SQL / Mongo / Elasticsearch | Filtro de tenant dentro da consulta, antes de paginar e contar; nunca filtrar em memória depois |
| Cache Redis | Tenant na chave quando o dado é por tenant (ex.: `reporting:events:{tenantId}`) |
| Consumer Kafka | Valide `TenantId` presente e bem formado no evento; evento sem tenant obrigatório vai para DLQ; efeitos colaterais usam o tenant do evento |
| Chamadas entre serviços | Token de serviço via `ServiceTokenHandler`; o tenant é dado do contrato, não credencial |
| Exportações e jobs | Escopo de tenant gravado no job e reaplicado na execução |
| Logs e métricas | Tenant pode ir em log estruturado; nunca como label de métrica (cardinalidade) |

## 3. Implementação

1. Mapeie todas as leituras e escritas do recurso (controller → caso de uso → repositório → cache/índice).
2. Introduza ou reutilize um tipo de escopo explícito (ex.: `AlertWindowAccessScope.Platform` /
   `.ForTenants(ids)`), em vez de `string? tenantId` opcional que significa "todos" quando nulo.
3. Aplique o filtro em cada ponto da tabela.
4. Não exponha a existência de recursos de outro tenant em mensagens de erro.

## 4. Testes obrigatórios

- Usuário do tenant A: lista só A; GET/PUT/DELETE de recurso de B → 404 (ou 403 conforme o padrão); filtro
  explícito por B → negado.
- Usuário com dois tenants vê os dois; sem claim `tenant_id` não vê nada.
- Principal de serviço com papel permitido atua em plataforma; com papel não permitido é barrado.
- Paginação e contagem não incluem itens de outros tenants.
- Consumer: evento sem tenant → dead-letter; evento de B não altera dados de A.
- Cache: mesma chave lógica para tenants diferentes não colide.

Ao terminar, peça ao subagent `security-auditor` a revisão do isolamento.
