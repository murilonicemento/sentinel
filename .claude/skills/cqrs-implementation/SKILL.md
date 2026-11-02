---
name: cqrs-implementation
description: Implementa commands, queries, handlers, validação e DTOs na camada Application de um serviço do Sentinel, integrando com repositórios por portas, aplicando CQRS só onde agrega valor. Use ao criar um caso de uso de escrita ou leitura.
argument-hint: <serviço> <NomeDoCasoDeUso>
---

# Implementar caso de uso (CQRS quando aplicável)

## 1. Decida o formato antes de escrever

Leia `docs/constitution.md` §4.2 e §16: CQRS e MediatR entram quando trazem benefício, não por padrão.

| Situação do serviço | Formato |
| --- | --- |
| Já usa MediatR (ingestion, risk-evaluation, alert-orchestrator) | `IRequest<T>` + `IRequestHandler` no padrão existente |
| Usa serviços de aplicação (channels, tenants-billing, risk-catalog, reporting, audit-compliance, geospatial) | Método em serviço de aplicação com command/query como record; não introduza MediatR |
| Leitura com modelo próprio (ex.: ingestion Infrastructure.Read em MongoDB) | Query separada lendo a projeção de leitura |

Confirme olhando `services/<serviço>/src/*Application/` antes de decidir.

## 2. Estrutura

- `Commands/<Nome>Command.cs` e `Queries/<Nome>Query.cs`: `sealed record` imutável com só os dados de entrada.
- `Handlers/` (ou o serviço de aplicação): orquestra o caso de uso; regras de negócio ficam no Domain.
- `DTOs/`: saída de queries e resultado de commands; nunca devolva a entidade.
- Portas (`Interfaces/` ou `Ports/`): repositórios, publicadores, relógio, gateways externos. Implementação em
  Infrastructure, registrada em `Add<Serviço>Infrastructure`.

## 3. Regras

- **Command** altera estado e retorna o mínimo (id, resultado). Carrega o agregado, chama o comportamento do
  domínio, persiste. Efeitos externos (eventos Kafka) saem pelo mecanismo do serviço: outbox no ingestion,
  publisher após persistir nos demais, com idempotência por id determinístico.
- **Query** não altera estado, lê só os campos necessários, pagina coleções e recebe o escopo de tenant como
  parâmetro (ex.: `AlertWindowAccessScope`), aplicado na consulta ao repositório.
- **Validação** explícita no início do handler (formato, faixa, obrigatórios), sem biblioteca de validação externa,
  retornando o erro no padrão do serviço (resultado tipado ou exceção de aplicação traduzida pelo
  `ApiExceptionHandler`). Invariantes de negócio ficam no domínio, não duplicadas aqui.
- **Cancelamento**: todo método assíncrono recebe e propaga `CancellationToken`.
- **Idempotência**: command que pode ser repetido (retry HTTP, mensagem reentregue) usa chave estável.
- **Transação**: uma por command no armazenamento do próprio serviço; nada de transação distribuída.
- **Logs**: estruturados, sem payload completo nem segredos.

## 4. Testes

Unitários do handler com Moq nas portas: caminho feliz, validação, recurso inexistente, conflito de estado,
isolamento de tenant na query, idempotência na repetição. Integração quando a consulta depende do banco real.
Use a skill `test-generation`.
