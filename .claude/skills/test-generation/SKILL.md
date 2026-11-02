---
name: test-generation
description: Procedimento para escrever testes unitários e de integração no Sentinel (xUnit, Moq, WebApplicationFactory, Testcontainers) cobrindo comportamento, casos positivos, negativos e de borda, invariantes de domínio, handlers, consumers, endpoints e isolamento multi-tenant. Use ao adicionar testes para código novo ou corrigido.
argument-hint: <serviço> <alvo (classe, endpoint ou comportamento)>
---

# Gerar testes

## 1. Defina o comportamento

1. Leia `docs/constitution.md` §9 e `.claude/rules/testing.md`.
2. Liste em uma frase cada comportamento observável a garantir (regra, entrada → saída, efeito colateral). Teste
   comportamento, não implementação: nada de verificar método privado ou ordem interna de chamadas sem efeito.
3. Para bug corrigido, escreva primeiro o teste que reproduz a falha e confirme que ele falha sem a correção.

## 2. Escolha o nível

| Alvo | Nível | Projeto |
| --- | --- | --- |
| Invariantes, value objects, transições de agregado | Unitário, sem mocks | `<Serviço>.UnitTests/Domain` |
| Handler / serviço de aplicação | Unitário com Moq nas portas | `<Serviço>.UnitTests/Handlers` ou equivalente |
| Contrato de evento (JSON de exemplo) | Unitário de desserialização | `<Serviço>.UnitTests/Contracts` |
| Endpoint, autenticação, tenant | Integração com a `WebApplicationFactory` do serviço | `<Serviço>.IntegrationTests` |
| Repositório, SQL, Mongo, Redis, Kafka, MinIO | Integração com Testcontainers | `<Serviço>.IntegrationTests` |

Siga a organização e os helpers existentes (`Support/*ApiFactory`, `TestTokens`, coleções `ICollectionFixture`
para compartilhar contêiner). Não crie projeto de teste novo sem necessidade.

## 3. Convenções do repositório

- xUnit (`[Fact]`, `[Theory]` + `[InlineData]` para limites) e asserções `Assert.*`, o padrão dominante; use
  FluentAssertions só no projeto que já o referencia.
- Moq para portas; `NullLogger`/`Mock<ILogger<T>>` para logs. Não mocke a unidade sob teste nem o domínio.
- Nome: `Metodo_Cenario_ResultadoEsperado` ou a forma já usada na classe vizinha.
- Arrange/Act/Assert visíveis; dados mínimos e sintéticos; ids únicos por teste para permitir paralelismo.
- Tempo controlado (instante passado como parâmetro ou relógio injetável), sem `Thread.Sleep`/`Task.Delay` para
  sincronizar; aguarde condições de forma determinística.
- Chaves de teste no padrão `integration-tests-...-0123456789`; nunca credenciais reais.
- Pacotes só de `Directory.Packages.props`.

## 4. Matriz mínima de cenários

- Caminho feliz e cada erro esperado (validação, não encontrado, conflito de estado).
- Limites: nulo, vazio, zero, máximo, paginação 0/1/50/51, fuso e fronteira de mês quando houver data.
- Multi-tenant: tenant A não lê/altera dados de B; sem claim de tenant não vê nada; principal de serviço conforme
  a regra; contagens e páginas sem vazamento.
- Mensageria: duplicata sem efeito duplo, inválido → dead-letter, falha transitória → retry limitado.
- Autenticação: 401 sem token, 403 com papel insuficiente.

## 5. Execução

```bash
dotnet test services/<serviço>/tests/<Serviço>.UnitTests --filter "FullyQualifiedName~<Classe>"
dotnet test services/<serviço>/tests/<Serviço>.IntegrationTests --filter "FullyQualifiedName~<Classe>"  # requer Docker
```

Relate o resultado real. Teste de integração que não rodou (sem Docker) é "não executado", nunca "aprovado".
Não pule, enfraqueça ou remova testes existentes para obter sucesso. O hook `validate-build` roda os testes
unitários dos serviços alterados ao fim do turno.
