# ADR-0007: Versionamento das APIs e depreciação das rotas antigas

- **Status:** Proposto
- **Data:** 2026-10-29 (registra a decisão D-4 aplicada na Fase 5, em 2026-10-25)
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (D-3, D-4, §4.2, X-02 a X-05, TB-05, TB-09, GW-01, RC-14), `.claude/rules/api-design.md`, `.claude/rules/backward-compatibility.md`

## Contexto

A auditoria encontrou só o audit-compliance seguindo a rota `/api/v1/<serviço>` exigida por `api-design.md`. Os outros serviços expunham rotas sem versão (`/api/ingestion`, `/api/event-types`, `/api/notifications`...) ou com versão sem o nome do serviço (`/api/v1/tenants`, `/api/v1/plans`). As respostas de erro também variavam por serviço e algumas expunham a mensagem da exceção (X-02, X-03).

Mover essas rotas é uma mudança incompatível pelos critérios de `api-design.md`: muda o caminho, o corpo de erro e, em alguns casos, a paginação. As rotas antigas têm consumidores conhecidos dentro da plataforma (o `TenantBillingGateway` de ingestion, alert-orchestrator e channels chama `GET /api/v1/tenants/{id}`) e podem ter consumidores externos não identificados.

## Decisões

### 1. Contrato novo em `/api/v1/<serviço>`

Cada serviço expõe o contrato público em `/api/v1/<serviço>`, com nomes de recurso em kebab-case. As regras comuns do v1 estão em `docs/compliance/services-validation.md` §4.2:

- envelope `ResponseBaseDTO<T>` no sucesso e `204` sem corpo;
- RFC 9457 Problem Details nos erros (D-3), com as extensões `code` e `traceId`;
- paginação `page`/`pageSize` (padrão 10, máximo 50);
- tenant validado contra a claim `tenant_id`.

O gateway expõe `/gateway/v1/<serviço>/{everything}` para os 9 serviços.

### 2. Expand-and-contract para as rotas antigas

As rotas antigas não foram removidas nem alteradas:

1. **Expand:** as rotas v1 foram criadas ao lado das antigas.
2. **Congelamento:** as rotas antigas mantêm o contrato que tinham (corpo, códigos de status e formato de erro). A exceção são as correções de segurança, como a do X-02, que deixou de devolver a mensagem da exceção. Comportamento novo entra só no v1.
3. **Depreciação:** cada rota antiga leva o atributo `DeprecatedRoute` (uma cópia por Api, em `Contracts/DeprecatedRouteAttribute.cs`), que acrescenta à resposta:
   - `Deprecation: @<unix>` com a data do anúncio;
   - `Sunset` com a data de remoção;
   - `Link: <rota v1>; rel="successor-version"`.

   O OpenAPI marca a operação com `deprecated: true`.
4. **Contract:** a remoção só acontece depois do sunset e depois de confirmar que nenhum consumidor conhecido ainda chama a rota antiga.

### 3. Prazo

O prazo é o padrão de 90 dias de `api-design.md`. O anúncio foi em 2026-10-25 e o sunset é em 2027-01-23. As datas estão em `DeprecatedRouteAttribute.AnnouncedAt` e `SunsetAt`; um novo anúncio exige mudar as cópias juntas.

### 4. Consumidores internos migram antes do sunset

Um consumidor dentro da plataforma passa para o v1 antes do sunset, em dois passos implantáveis separadamente (TB-09):

1. O cliente aceita os dois formatos de resposta.
2. O cliente passa a chamar a rota v1.

Como a rota antiga segue respondendo até o sunset, voltar o consumidor para a versão anterior é seguro.

### 5. Mudança incompatível dentro do v1

Uma mudança incompatível em uma rota v1 exige aprovação explícita e registro no relatório de validação, com o motivo e o efeito sobre os clientes. Sem essa aprovação, ela vira uma rota nova ou um `v2`. O único caso até agora é o `regionCode` obrigatório em `POST /api/v1/risk-catalog/regional-risk-parameters`, aprovado em 2026-10-29 (RC-14, ADR-0005). Um cliente v1 que não envia o código recebe 400, e a rota legada continua gerando o código.

## Alternativas consideradas

- **Remover as rotas antigas na migração.** Rejeitada: quebra os consumidores internos no mesmo deploy e os externos sem aviso.
- **Versionar por cabeçalho ou media type.** Rejeitada: `api-design.md` determina versionamento por caminho.
- **Corrigir as rotas antigas no lugar (paginação, Problem Details).** Rejeitada: muda o contrato que os consumidores atuais usam, que é justamente o que a depreciação evita.

## Consequências

- Cada serviço mantém dois conjuntos de controllers até o sunset. Correções de segurança valem para os dois; comportamento novo vai só para o v1.
- As datas de anúncio e sunset estão duplicadas nas cópias de `DeprecatedRouteAttribute` (não há biblioteca compartilhada).
- A remoção depois de 2027-01-23 ainda precisa ser planejada: listar as rotas, confirmar a ausência de chamadas (logs do gateway e de cada serviço) e retirar as rotas do `ocelot.json` junto com os controllers.
- Ainda não há verificação automática de compatibilidade de contrato no CI (`backward-compatibility.md` recomenda). Por enquanto, a proteção são os testes de contrato v1 de cada serviço.
