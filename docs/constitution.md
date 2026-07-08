# Sentinel — Constituição do Projeto

## 1. Visão geral

O `sentinel` é um monorepo .NET para uma plataforma distribuída de detecção, análise e alerta de eventos naturais.

A arquitetura é baseada em **microsserviços independentes por bounded context**, com comunicação majoritariamente assíncrona.

---

## Estrutura do repositório

- `README.md`
  Visão geral, arquitetura e instruções básicas.

- `docs/`
  RFCs, diagramas e decisões arquiteturais (ADRs).

- `platform/`
  Infraestrutura local:
  - Docker Compose
  - bancos de dados
  - mensageria
  - observabilidade

- `services/`
  Microsserviços isolados por bounded context.

---

## 2. Serviços existentes

- `api-gateway`
- `ingestion`
- `risk-catalog`
- `geospatial`
- `risk-evaluation`
- `alert-orchestrator`

---

## 3. Princípios arquiteturais

O sistema segue:

- Domain-Driven Design (DDD)
- Clean Architecture
- CQRS (quando aplicável)
- Event-driven architecture
- Microsserviços com isolamento total de dados
- Observabilidade como padrão
- Resiliência obrigatória

---

## 4. Regras fundamentais

- Cada serviço é independente
- Não há compartilhamento de banco entre serviços
- Comunicação preferencialmente assíncrona
- Nenhuma camada pode “furar” boundaries
- Toda decisão arquitetural relevante deve ser registrada em ADR

---

## 5. Estrutura padrão de um serviço

Cada microservice possui **UMA solution única (.sln)**.

### Estrutura:

```bash
services/<nome-servico>/
  <NomeServico>.sln
  src/
    <NomeServico>.Api/
    <NomeServico>.Application/
    <NomeServico>.Domain/
    <NomeServico>.Infrastructure/
  tests/
    <NomeServico>.UnitTests/
    <NomeServico>.IntegrationTests/
```

---

## 6. Regras de criação de serviços (OBRIGATÓRIO)

### Ferramentas permitidas

- `dotnet new`
- `dotnet sln`
- `dotnet sln add`
- `dotnet add reference`

---

## 6.1 Fluxo de criação

### 1. Criar estrutura

```bash
mkdir -p services/<nome-servico>/src
cd services/<nome-servico>
```

---

### 2. Criar solution

```bash
dotnet new sln -n <NomeServico>
```

---

### 3. Criar projetos

```bash
dotnet new webapi -n <NomeServico>.Api -o src/<NomeServico>.Api
dotnet new classlib -n <NomeServico>.Application -o src/<NomeServico>.Application
dotnet new classlib -n <NomeServico>.Domain -o src/<NomeServico>.Domain
dotnet new classlib -n <NomeServico>.Infrastructure -o src/<NomeServico>.Infrastructure
```

---

### (Opcional) CQRS

```bash
src/<NomeServico>.Infrastructure.Read
src/<NomeServico>.Infrastructure.Write
```

---

### 4. Criar testes

```bash
dotnet new xunit -n <NomeServico>.UnitTests -o tests/<NomeServico>.UnitTests
dotnet new xunit -n <NomeServico>.IntegrationTests -o tests/<NomeServico>.IntegrationTests
```

---

### 5. Adicionar tudo na solution (OBRIGATÓRIO)

```bash
dotnet sln add src/<NomeServico>.Api/<NomeServico>.Api.csproj
dotnet sln add src/<NomeServico>.Application/<NomeServico>.Application.csproj
dotnet sln add src/<NomeServico>.Domain/<NomeServico>.Domain.csproj
dotnet sln add src/<NomeServico>.Infrastructure/<NomeServico>.Infrastructure.csproj

dotnet sln add tests/<NomeServico>.UnitTests/<NomeServico>.UnitTests.csproj
dotnet sln add tests/<NomeServico>.IntegrationTests/<NomeServico>.IntegrationTests.csproj
```

---

## 7. Referências entre projetos

### Application → Domain

```bash
dotnet add src/<NomeServico>.Application reference src/<NomeServico>.Domain
```

---

### Infrastructure → Application + Domain

```bash
dotnet add src/<NomeServico>.Infrastructure reference src/<NomeServico>.Application
dotnet add src/<NomeServico>.Infrastructure reference src/<NomeServico>.Domain
```

---

### API → Application

```bash
dotnet add src/<NomeServico>.Api reference src/<NomeServico>.Application
```

---

## 8. Estrutura de camadas

### Domain

Responsável por:

- Entidades
- Value Objects
- Domain Services
- Domain Events

Proibido:

- banco de dados
- frameworks externos
- DTOs

---

### Application

Responsável por:

- Casos de uso
- Commands e Queries
- Orquestração
- Interfaces (ports)

Proibido:

- EF Core
- HTTP direto
- infraestrutura

---

### Infrastructure

Responsável por:

- Banco de dados
- mensageria
- cache
- integrações externas

---

### API

Responsável por:

- Controllers
- Middleware
- autenticação
- validação de entrada

Proibido:

- regra de negócio

---

## 9. Testes

### Estrutura

```bash
tests/
  UnitTests/
  IntegrationTests/
```

---

### Unit Tests

- regras de domínio
- casos de uso isolados
- validações puras

Proibido:

- banco
- HTTP
- mensageria

---

### Integration Tests

- fluxo completo da API
- integração com infraestrutura
- testes de pipeline real

Pode usar:

- Testcontainers
- Docker Compose
- banco de teste

---

### Regra de integridade

Um serviço é inválido se:

- não tiver testes
- testes não estiverem na solution
- dependências estiverem erradas
- arquitetura for violada

---

## 10. Comunicação entre serviços

### Síncrona (limitada)

- API Gateway → serviços

### Assíncrona (preferencial)

- eventos
- mensageria
- integração via contratos

---

## 11. Infraestrutura obrigatória

Todos os serviços devem suportar:

- Docker
- ambiente local via `platform/`
- multi-tenant
- observabilidade:
  - logs estruturados
  - tracing distribuído
  - métricas

---

## 12. Convenções de nomenclatura

### Serviços

- kebab-case: `risk-catalog`

### Projetos

- PascalCase:
  - `RiskCatalog.Api`
  - `RiskCatalog.Domain`

---

## 13. Regras de validação do serviço

Um serviço é considerado inválido se:

- não estiver na `.sln`
- tiver dependência circular
- Domain depender de outras camadas
- Application depender de API
- violar boundaries

---

## 14. Regra de ouro

> Nenhuma camada pode conhecer detalhes de infraestrutura sem abstração.
