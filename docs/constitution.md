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
- Event-driven Architecture
- Microsserviços com isolamento total de dados
- Observabilidade como padrão
- Resiliência obrigatória

---

## 4. Regras fundamentais

- Cada serviço é independente.
- Não há compartilhamento de banco de dados entre serviços.
- Comunicação preferencialmente assíncrona.
- Nenhuma camada pode "furar" os boundaries.
- Toda decisão arquitetural relevante deve ser registrada em um ADR.

---

## 5. Estrutura padrão de um serviço

Cada microsserviço possui **uma única Solution (.sln)**.

### Estrutura

```text
services/<nome-servico>/
│
├── Dockerfile
├── .dockerignore
│
├── src/
│   ├── <NomeServico>.Api/
│   ├── <NomeServico>.Application/
│   ├── <NomeServico>.Domain/
│   └── <NomeServico>.Infrastructure/
│
└── tests/
    ├── <NomeServico>.UnitTests/
    └── <NomeServico>.IntegrationTests/
```

---

## 6. Regras de criação de serviços (OBRIGATÓRIO)

### Ferramentas permitidas

Somente as seguintes ferramentas devem ser utilizadas para criação da estrutura do serviço:

- `dotnet new`
- `dotnet new sln`
- `dotnet sln`
- `dotnet sln add`
- `dotnet add reference`

---

## 6.1 Fluxo de criação

### 1. Criar estrutura

```bash
mkdir -p services/<nome-servico>/src
mkdir -p services/<nome-servico>/tests
cd services/<nome-servico>

dotnet new sln -n <NomeServico>
```

---

### 2. Criar projetos

```bash
dotnet new webapi -n <NomeServico>.Api -o src/<NomeServico>.Api

dotnet new classlib -n <NomeServico>.Application -o src/<NomeServico>.Application

dotnet new classlib -n <NomeServico>.Domain -o src/<NomeServico>.Domain

dotnet new classlib -n <NomeServico>.Infrastructure -o src/<NomeServico>.Infrastructure
```

### (Opcional) CQRS

Caso exista separação física de leitura e escrita:

```text
src/
├── <NomeServico>.Infrastructure.Read/
└── <NomeServico>.Infrastructure.Write/
```

---

### 3. Criar projetos de testes

```bash
dotnet new xunit -n <NomeServico>.UnitTests -o tests/<NomeServico>.UnitTests

dotnet new xunit -n <NomeServico>.IntegrationTests -o tests/<NomeServico>.IntegrationTests
```

---

### 4. Adicionar todos os projetos à Solution (OBRIGATÓRIO)

```bash
dotnet sln add src/<NomeServico>.Api/<NomeServico>.Api.csproj

dotnet sln add src/<NomeServico>.Application/<NomeServico>.Application.csproj

dotnet sln add src/<NomeServico>.Domain/<NomeServico>.Domain.csproj

dotnet sln add src/<NomeServico>.Infrastructure/<NomeServico>.Infrastructure.csproj

dotnet sln add tests/<NomeServico>.UnitTests/<NomeServico>.UnitTests.csproj

dotnet sln add tests/<NomeServico>.IntegrationTests/<NomeServico>.IntegrationTests.csproj
```

---

### 5. Configurar as referências entre projetos

#### Application → Domain

```bash
dotnet add src/<NomeServico>.Application reference src/<NomeServico>.Domain
```

#### Infrastructure → Application + Domain

```bash
dotnet add src/<NomeServico>.Infrastructure reference src/<NomeServico>.Application

dotnet add src/<NomeServico>.Infrastructure reference src/<NomeServico>.Domain
```

#### API → Application

```bash
dotnet add src/<NomeServico>.Api reference src/<NomeServico>.Application
```

---

### 6. Criar arquivos de containerização (OBRIGATÓRIO)

Todo serviço deve possuir os arquivos abaixo na raiz do serviço:

```text
services/<nome-servico>/
├── Dockerfile
└── .dockerignore
```

#### Dockerfile

O Dockerfile deve obrigatoriamente:

- utilizar **multi-stage build**;
- utilizar imagens oficiais do .NET;
- restaurar dependências separadamente para otimizar cache;
- publicar apenas o projeto `.Api`;
- gerar uma imagem final contendo apenas os artefatos necessários para execução.

#### .dockerignore

O `.dockerignore` deve conter, no mínimo:

```text
**/bin/
**/obj/

.vs/
.vscode/

.git/
.gitignore

README.md
docs/

tests/
```

---

## 7. Referências entre projetos

A arquitetura deve respeitar rigorosamente as seguintes dependências.

### Domain

Não depende de nenhum projeto.

---

### Application

Depende apenas de:

- Domain

---

### Infrastructure

Depende apenas de:

- Application
- Domain

---

### API

Depende apenas de:

- Application

---

## 8. Estrutura de camadas

### Domain

Responsável por:

- Entidades
- Value Objects
- Aggregates
- Domain Services
- Domain Events
- Interfaces do domínio

Proibido:

- Entity Framework
- Banco de dados
- HTTP
- Mensageria
- DTOs
- Frameworks externos

---

### Application

Responsável por:

- Casos de uso
- Commands
- Queries
- Handlers
- DTOs
- Validações
- Interfaces (Ports)
- Orquestração

Proibido:

- Entity Framework
- Banco de dados
- HTTP direto
- Mensageria direta
- Infraestrutura

---

### Infrastructure

Responsável por:

- Entity Framework
- Persistência
- Cache
- Mensageria
- Integrações externas
- Implementação das interfaces da Application
- Repositórios
- Providers

---

### API

Responsável por:

- Controllers
- Endpoints
- Middleware
- Autenticação
- Autorização
- Configuração de DI
- Health Checks
- Swagger
- Validação de entrada

Proibido:

- Regra de negócio
- Acesso direto ao banco
- Implementação de casos de uso

---

## 9. Testes

### Estrutura

```text
tests/
├── UnitTests/
└── IntegrationTests/
```

---

### Unit Tests

Devem testar:

- regras de domínio;
- casos de uso;
- validações;
- lógica pura.

Não podem utilizar:

- banco de dados;
- HTTP;
- mensageria;
- infraestrutura real.

---

### Integration Tests

Devem validar:

- fluxo completo da API;
- integração com infraestrutura;
- banco de dados;
- mensageria;
- pipelines reais.

Podem utilizar:

- Testcontainers;
- Docker Compose;
- banco de testes.

---

### Regra de integridade

Um serviço é considerado inválido caso:

- não possua testes;
- os testes não estejam adicionados à Solution;
- existam dependências incorretas;
- exista violação da arquitetura.

---

## 10. Comunicação entre serviços

### Comunicação síncrona (limitada)

Permitida apenas quando realmente necessária.

Exemplos:

- API Gateway → Serviços

---

### Comunicação assíncrona (preferencial)

Deve ser utilizada para:

- eventos de domínio;
- integração entre serviços;
- processamento assíncrono;
- publicação de contratos.

---

## 11. Infraestrutura obrigatória

Todo microsserviço deve possuir obrigatoriamente:

- `Dockerfile`
- `.dockerignore`

Além disso, deve oferecer suporte a:

- Docker;
- ambiente local via `platform/`;
- multi-tenancy;
- logs estruturados;
- tracing distribuído;
- métricas;
- Health Checks;
- configuração via variáveis de ambiente.

---

## 12. Convenções de nomenclatura

### Serviços

Utilizar **kebab-case**.

Exemplos:

- `risk-catalog`
- `risk-evaluation`
- `alert-orchestrator`

---

### Projetos

Utilizar **PascalCase**.

Exemplos:

- `RiskCatalog.Api`
- `RiskCatalog.Application`
- `RiskCatalog.Domain`
- `RiskCatalog.Infrastructure`

---

## 13. Regras de validação do serviço

Um serviço será considerado inválido caso:

- não possua uma Solution própria;
- algum projeto não esteja adicionado à Solution;
- exista dependência circular;
- Domain dependa de outra camada;
- Application dependa da API;
- API dependa diretamente da Infrastructure;
- exista compartilhamento de banco de dados entre serviços;
- não possua Dockerfile;
- não possua `.dockerignore`;
- viole os boundaries definidos pela arquitetura.

---

## 14. Regra de ouro

> Nenhuma camada pode conhecer detalhes de infraestrutura sem abstração.

Toda dependência deve apontar para dentro da arquitetura (Dependency Rule da Clean Architecture).

A arquitetura deve permanecer desacoplada, testável, evolutiva e independente de frameworks.
