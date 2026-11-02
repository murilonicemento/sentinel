# Constituição do Projeto Sentinel

## 1. Objetivo

Estabelecer princípios arquiteturais, padrões de desenvolvimento e regras obrigatórias para o projeto Sentinel, garantindo consistência, segurança, manutenibilidade, escalabilidade e autonomia entre os microsserviços.

Esta constituição é a principal referência para decisões técnicas e deve ser respeitada por desenvolvedores, agentes de IA e ferramentas automatizadas que atuem no repositório.

Em caso de conflito entre uma implementação existente e esta constituição, a constituição prevalece, salvo quando uma exceção for explicitamente documentada e aprovada.

## 2. Arquitetura do Sistema

O Sentinel é uma plataforma baseada em microsserviços, desenvolvida em .NET, organizada em um monorepo.

### 2.1. Princípios arquiteturais

- **Microservices Architecture:** cada serviço possui uma responsabilidade de negócio bem definida.
- **Domain-Driven Design (DDD):** o domínio deve orientar a organização e as regras de negócio.
- **Clean Architecture:** as dependências devem apontar para dentro, em direção ao domínio.
- **CQRS:** aplicar a separação entre comandos e consultas quando houver benefício arquitetural.
- **Event-Driven Architecture:** priorizar comunicação assíncrona por eventos quando apropriado.
- **Database per Service:** cada microsserviço é responsável por seus próprios dados.
- **Observability by Default:** logs estruturados, métricas e rastreamento distribuído devem ser considerados desde o início.
- **Resilience by Design:** implementar timeouts, tratamento de falhas, retries controlados e idempotência conforme a necessidade.
- **Test-Driven Development (TDD):** priorizar testes automatizados para validar comportamentos e regras de negócio.

### 2.2. Organização do monorepo

A estrutura principal do repositório deve seguir este padrão:

- `Sentinel.sln`: única solution de todo o monorepo.
- `global.json`: define a versão do SDK .NET utilizada.
- `Directory.Build.props`: configurações compartilhadas de build.
- `Directory.Packages.props`: gerenciamento centralizado de versões de pacotes NuGet, quando aplicável.
- `services/`: diretório contendo todos os microsserviços.
- `platform/`: infraestrutura local e componentes de suporte à plataforma.
- `docs/`: documentação do projeto.
- `.claude/`: regras, skills e configurações utilizadas pelo Claude Code.

Cada microsserviço deve manter seus próprios projetos de código-fonte e testes dentro de seu diretório.

A organização interna de um microsserviço não cria uma solution independente.

### 2.3. Microsserviços

Os serviços incluem, entre outros:

- `api-gateway`
- `ingestion`
- `risk-catalog`
- `geospatial`
- `risk-evaluation`
- `alert-orchestrator`
- `channels`
- `reporting`
- `tenants-billing`
- `audit-compliance`

A lista deve ser atualizada quando novos microsserviços forem adicionados ou removidos.

Cada serviço deve possuir limites de responsabilidade explícitos, contratos bem definidos e autonomia sobre seus dados.

### 2.4. Solution única

O monorepo deve possuir **uma única solution .NET, chamada `Sentinel.sln`, localizada na raiz do repositório**.

Todos os projetos `.csproj` de todos os microsserviços devem fazer parte dessa solution, incluindo:

- Projetos de API ou Presentation.
- Projetos de Application.
- Projetos de Domain.
- Projetos de Infrastructure.
- Projetos de integração.
- Projetos de testes unitários.
- Projetos de testes de integração.
- Outros projetos .NET pertencentes ao monorepo.

Regras obrigatórias:

1. Não criar arquivos `.sln` específicos para microsserviços.
2. Não manter solutions independentes dentro de `services/<nome-servico>/`.
3. Ao criar um projeto `.csproj`, adicioná-lo à `Sentinel.sln`.
4. Ao criar um novo microsserviço, todos os seus projetos devem ser registrados na solution raiz.
5. Não excluir projetos da `Sentinel.sln` apenas porque pertencem a outro microsserviço.
6. Antes de finalizar alterações na estrutura de projetos, verificar a lista de projetos da solution raiz.

Comandos oficiais:

```bash
# Listar todos os projetos registrados
dotnet sln Sentinel.sln list

# Adicionar um projeto existente à solution
dotnet sln Sentinel.sln add services/<nome-servico>/src/<Projeto>/<Projeto>.csproj

# Adicionar um projeto de testes à solution
dotnet sln Sentinel.sln add services/<nome-servico>/tests/<ProjetoTestes>/<ProjetoTestes>.csproj

# Compilar todo o monorepo
dotnet build Sentinel.sln

# Executar os testes da solution
dotnet test Sentinel.sln
```

Os caminhos acima são exemplos e devem ser substituídos pelos caminhos reais dos projetos.

Se existir uma solution específica de algum serviço, ela deve ser migrada para a solution raiz. Depois de verificar que todos os projetos foram registrados corretamente e que o build e os testes funcionam, o arquivo `.sln` específico deve ser removido.

## 3. Estrutura dos Microsserviços

Cada microsserviço deve ser organizado por responsabilidade, utilizando as camadas da Clean Architecture quando aplicável.

Estrutura de referência:

```text
services/
└── <nome-servico>/
    ├── src/
    │   ├── <NomeServico>.Api/
    │   ├── <NomeServico>.Application/
    │   ├── <NomeServico>.Domain/
    │   └── <NomeServico>.Infrastructure/
    └── tests/
        ├── <NomeServico>.UnitTests/
        └── <NomeServico>.IntegrationTests/
```

As camadas e os projetos devem ser criados conforme as necessidades reais do serviço. Não adicionar projetos vazios ou camadas sem responsabilidade definida apenas para preencher a estrutura.

Os projetos de um mesmo serviço devem permanecer agrupados no diretório desse serviço, mesmo que todos estejam registrados na `Sentinel.sln`.

## 4. Clean Architecture e Dependências

### 4.1. Domain

Responsável pelas regras de negócio fundamentais.

Deve conter, quando aplicável:

- Entidades.
- Value Objects.
- Agregados.
- Domain Services.
- Eventos de domínio.
- Interfaces que representem necessidades do domínio.
- Regras e invariantes de negócio.

Restrições:

- Não depender de Application, Infrastructure ou API.
- Não depender de frameworks de persistência.
- Não conter detalhes de infraestrutura.
- Não depender de serviços externos.

### 4.2. Application

Responsável pela execução dos casos de uso.

Deve conter, quando aplicável:

- Commands e Queries.
- Handlers.
- DTOs de aplicação.
- Interfaces de serviços externos.
- Interfaces de repositórios.
- Validação dos casos de uso.
- Orquestração das operações de negócio.
- Regras de autorização específicas dos casos de uso, quando aplicável.

Restrições:

- Pode depender de Domain.
- Não deve depender de Infrastructure.
- Não deve depender de API ou Presentation.
- Não deve conter detalhes concretos de persistência ou transporte.

### 4.3. Infrastructure

Responsável por implementações técnicas.

Pode conter:

- Repositórios concretos.
- Persistência e acesso a dados.
- Integrações externas.
- Publicação e consumo de mensagens.
- Implementações de cache.
- Implementações de serviços técnicos.
- Configurações de infraestrutura.

Restrições:

- Pode depender de Application e Domain.
- Não deve conter regras de negócio que pertençam ao domínio.
- Não deve ser referenciada diretamente por Domain ou Application.

### 4.4. API / Presentation

Responsável pela exposição dos casos de uso por meio de contratos externos.

Pode conter:

- Controllers ou endpoints.
- DTOs de entrada e saída da API.
- Configuração de autenticação e autorização.
- Middleware.
- Tratamento de erros HTTP.
- Configuração e inicialização da aplicação.

A API pode depender de Application.

O acesso direto à Infrastructure deve ser evitado. Caso a inicialização da aplicação exija uma extensão de registro de dependências, como `Add<Service>Infrastructure()`, a referência à Infrastructure deve se limitar à composição da aplicação e ao registro dos serviços.

Essa exceção não autoriza controllers, endpoints ou handlers da API a acessarem diretamente repositórios ou implementações de infraestrutura.

### 4.5. Direção das dependências

A direção padrão das dependências deve ser:

```text
API → Application → Domain
Infrastructure → Application
Infrastructure → Domain
```

A API pode referenciar Infrastructure exclusivamente para a composição e o registro das dependências, conforme a exceção descrita na seção 4.4.

Domain não depende de nenhuma camada externa.

Application não depende de Infrastructure ou API.

Infrastructure implementa contratos definidos pelas camadas internas.

## 5. Organização de Projetos e Convenções

### 5.1. Nomenclatura

- Projetos .NET devem utilizar PascalCase.
- Classes e interfaces devem utilizar PascalCase.
- Métodos e propriedades devem utilizar PascalCase.
- Variáveis locais e parâmetros devem utilizar camelCase.
- Constantes devem utilizar UPPER_SNAKE_CASE quando esse padrão for adotado no contexto correspondente.
- Enums devem terminar com o sufixo `Enum`, conforme a convenção do projeto.
- Diretórios de microsserviços devem utilizar kebab-case.
- Namespaces devem acompanhar a organização lógica dos projetos.

A nomenclatura deve ser consistente com o código existente e com as convenções oficiais do C# sempre que não houver regra específica do projeto.

### 5.2. Separação por responsabilidade

Cada projeto deve ter uma responsabilidade clara.

Evitar:

- Misturar regras de negócio com controllers.
- Misturar persistência com casos de uso.
- Colocar lógica de domínio em DTOs.
- Criar dependências circulares entre projetos.
- Compartilhar entidades de domínio entre microsserviços.
- Criar bibliotecas compartilhadas que acoplem indevidamente os serviços.

### 5.3. Isolamento dos microsserviços

Cada serviço deve ser independente em relação a:

- Seu domínio.
- Seus contratos.
- Sua persistência.
- Sua lógica de negócio.
- Suas políticas de acesso aos dados.

Não compartilhar banco de dados entre microsserviços.

Não utilizar tabelas de outro serviço como mecanismo de integração.

A comunicação entre serviços deve ocorrer por APIs ou mensagens/eventos com contratos explícitos.

## 6. Criação de Novos Microsserviços e Projetos

### 6.1. Processo obrigatório

Ao criar um novo microsserviço:

1. Criar o diretório `services/<nome-servico>/`.
2. Criar a estrutura `src/` e `tests/`, conforme as necessidades do serviço.
3. Criar os projetos `.csproj` necessários.
4. Adicionar cada projeto à `Sentinel.sln`, localizada na raiz.
5. Configurar as referências entre projetos respeitando a Clean Architecture.
6. Configurar os testes automatizados.
7. Implementar os pontos necessários de configuração, logging e observabilidade.
8. Atualizar a documentação afetada.
9. Executar a compilação da solution raiz.
10. Executar os testes relevantes e verificar o resultado.

**É proibido criar uma nova solution para o microsserviço.**

Não executar `dotnet new sln` durante a criação de serviços ou projetos comuns. A única solution do monorepo já é a `Sentinel.sln`.

### 6.2. Registro de projetos

Os projetos devem ser adicionados explicitamente à solution raiz:

```bash
dotnet sln Sentinel.sln add services/<nome-servico>/src/<Projeto>/<Projeto>.csproj
```

Para testes:

```bash
dotnet sln Sentinel.sln add services/<nome-servico>/tests/<ProjetoTestes>/<ProjetoTestes>.csproj
```

Antes de adicionar um projeto, verificar se ele já está registrado para evitar duplicações ou tentativas desnecessárias.

### 6.3. Referências entre projetos

As referências devem seguir a direção definida na seção 4.

Exemplo conceitual:

```text
Servico.Api
    └── Servico.Application

Servico.Application
    └── Servico.Domain

Servico.Infrastructure
    ├── Servico.Application
    └── Servico.Domain

Servico.UnitTests
    └── Projeto sob teste
```

A referência à Infrastructure pela API deve ser limitada à composição e à configuração de dependências, quando necessária.

Os testes de integração podem referenciar os projetos e componentes necessários para validar o comportamento integrado do serviço.

## 7. API Design

### 7.1. Padrões REST

As APIs devem seguir princípios RESTful.

- Utilizar métodos HTTP de acordo com a semântica da operação.
- Utilizar nomes de recursos em kebab-case.
- Utilizar o prefixo `/api/<nome-servico>`.
- Utilizar JSON como formato padrão de comunicação, exceto quando a operação exigir outro formato ou não retornar conteúdo.
- Utilizar códigos HTTP apropriados.
- Manter contratos claros, explícitos e documentados.

### 7.2. DTOs

As APIs devem utilizar DTOs explícitos para entrada e saída.

É proibido expor diretamente:

- Entidades de domínio.
- Modelos internos de persistência.
- Objetos de infraestrutura.
- Estruturas internas que não façam parte do contrato público.

Os DTOs devem conter somente os dados necessários para a operação.

### 7.3. Respostas e erros

As respostas devem seguir um padrão consistente dentro de cada API.

Quando existir um envelope de resposta, ele deve manter uma estrutura uniforme e documentada.

Erros devem utilizar códigos HTTP apropriados e mensagens que não exponham detalhes internos, segredos ou informações sensíveis.

Respostas sem conteúdo devem utilizar o status HTTP apropriado, como `204 No Content`, quando aplicável.

### 7.4. Contexto de tenant

Quando uma operação exigir contexto de tenant:

- O tenant deve ser identificado e validado no contexto autenticado.
- A associação entre usuário e tenant deve ser verificada.
- O `tenantId` enviado pelo cliente não deve ser considerado confiável por si só.
- A autorização deve ser aplicada antes de permitir o acesso aos dados.
- Operações que não necessitem de contexto de tenant não devem exigir esse dado artificialmente.
- Requisições GET não devem utilizar corpo para transportar o contexto do tenant.

O contexto de tenant deve ser obtido preferencialmente de claims autenticadas, de contexto validado ou de mecanismos equivalentes definidos pela plataforma.

## 8. Segurança

### 8.1. Princípios gerais

A segurança deve ser considerada desde o início do desenvolvimento.

Aplicar:

- Princípio do menor privilégio.
- Autenticação e autorização explícitas.
- Validação de entradas.
- Proteção contra exposição de dados sensíveis.
- Tratamento seguro de erros.
- Gerenciamento adequado de segredos.
- Isolamento entre tenants.
- Registro de eventos relevantes de segurança.

### 8.2. Segredos e configurações

- Não versionar senhas, tokens, chaves privadas ou credenciais reais.
- Utilizar variáveis de ambiente ou mecanismos apropriados de gerenciamento de segredos.
- Utilizar arquivos locais de desenvolvimento somente quando protegidos e fora do controle de versão, conforme necessário.
- Nunca registrar credenciais ou tokens em logs.
- Separar configurações de desenvolvimento, teste e produção.

### 8.3. Multi-tenancy

A aplicação deve garantir que um tenant não consiga acessar dados de outro tenant sem autorização explícita.

A validação de tenant deve ocorrer no servidor.

Não confiar exclusivamente em filtros enviados pelo cliente para limitar o acesso aos dados.

As consultas, comandos, repositórios e integrações devem respeitar o contexto de autorização aplicável.

## 9. Testes

### 9.1. Estratégia

Os microsserviços devem possuir testes automatizados proporcionais à complexidade e ao risco das funcionalidades.

A estratégia deve incluir, conforme aplicável:

- Testes unitários.
- Testes de integração.
- Testes de contrato.
- Testes de autorização e isolamento entre tenants.
- Testes de idempotência.
- Testes de resiliência.
- Testes de desempenho para operações críticas.

### 9.2. Organização

Os testes devem permanecer dentro do diretório do respectivo microsserviço:

```text
services/<nome-servico>/
├── src/
│   └── ...
└── tests/
    ├── <NomeServico>.UnitTests/
    └── <NomeServico>.IntegrationTests/
```

Os nomes são exemplos. Projetos adicionais de teste podem ser criados quando houver uma necessidade concreta.

Todos os projetos de testes .NET devem ser adicionados à `Sentinel.sln`.

### 9.3. Testes unitários

Devem validar comportamentos isolados, especialmente:

- Regras de negócio.
- Invariantes do domínio.
- Validações.
- Casos de uso.
- Tratamento de cenários de erro.
- Limites e condições extremas.

Os testes unitários devem ser rápidos e evitar dependências externas desnecessárias.

### 9.4. Testes de integração

Devem validar a interação real entre componentes quando aplicável, incluindo:

- Persistência.
- Mensageria.
- Integrações externas.
- Configuração de dependências.
- Endpoints.
- Autenticação e autorização.
- Contratos de comunicação.

Os testes devem ter configuração e isolamento adequados para não depender acidentalmente de dados ou ambientes externos não controlados.

### 9.5. Execução

A compilação oficial do monorepo deve utilizar a solution raiz:

```bash
dotnet build Sentinel.sln
```

A execução oficial dos testes deve utilizar:

```bash
dotnet test Sentinel.sln
```

É permitido executar testes de um projeto individual durante o desenvolvimento e a investigação de falhas, desde que isso não substitua a validação final necessária.

## 10. Comunicação entre Microsserviços

### 10.1. Comunicação assíncrona

Priorizar comunicação assíncrona por eventos quando isso contribuir para reduzir acoplamento e permitir autonomia entre serviços.

A comunicação síncrona por API pode ser utilizada quando houver necessidade de resposta imediata ou quando o caso de uso justificar essa escolha.

### 10.2. Contratos de eventos

Os eventos devem possuir contratos explícitos, versionamento quando necessário e documentação.

Um contrato de evento deve definir, conforme aplicável:

- Identificador do evento.
- Tipo ou nome do evento.
- Versão do contrato.
- Data e hora de ocorrência.
- Identificador do produtor.
- Identificador de correlação.
- Identificador de causalidade.
- Dados do evento.
- Contexto de tenant, quando necessário.

Os contratos não devem expor detalhes internos de implementação ou persistência do produtor.

### 10.3. Resiliência da mensageria

Os consumidores devem considerar:

- Entrega repetida de mensagens.
- Falhas temporárias.
- Mensagens inválidas.
- Reprocessamento.
- Dead-letter queues, quando aplicável.
- Idempotência.
- Rastreamento de mensagens.
- Tratamento de mensagens fora de ordem, quando relevante.

Não assumir que a entrega de mensagens ocorre exatamente uma única vez.

Quando a perda de eventos for inaceitável, avaliar mecanismos como Transactional Outbox e estratégias equivalentes para manter a consistência entre persistência e publicação.

## 11. Persistência e Dados

### 11.1. Propriedade dos dados

Cada microsserviço é responsável por sua própria persistência.

Acesso direto ao banco de dados de outro microsserviço é proibido.

Integrações devem ocorrer por contratos explícitos, APIs ou mensagens.

### 11.2. Modelagem

A modelagem dos dados deve refletir as necessidades do domínio.

- Aplicar DDD quando houver benefício real.
- Definir invariantes de negócio no domínio.
- Evitar acoplamento excessivo entre modelo de domínio e modelo de persistência.
- Evitar abstrações genéricas que dificultem a compreensão do código.
- Criar índices e otimizações com base em consultas e medições reais.
- Considerar transações, concorrência e integridade dos dados.

### 11.3. Migrações

Alterações de esquema devem ser versionadas e reproduzíveis.

As migrações devem ser revisadas para identificar riscos de perda de dados, bloqueios, incompatibilidade e impacto no ambiente de produção.

Alterações potencialmente destrutivas devem possuir estratégia de migração e recuperação adequada.

## 12. Observabilidade e Resiliência

### 12.1. Logs

Os serviços devem utilizar logs estruturados.

Os logs devem permitir identificar:

- Serviço e operação.
- Resultado da execução.
- Falhas e exceções relevantes.
- Identificadores de correlação.
- Contexto necessário para diagnóstico.

Não registrar segredos, tokens ou dados pessoais desnecessários.

### 12.2. Métricas e rastreamento

Implementar métricas e rastreamento distribuído conforme as necessidades do serviço e da plataforma.

Quando aplicável, utilizar padrões compatíveis com OpenTelemetry.

A observabilidade deve facilitar a investigação de falhas e a identificação de gargalos.

### 12.3. Resiliência

As integrações devem considerar:

- Timeouts explícitos.
- Retries limitados.
- Backoff exponencial com jitter quando apropriado.
- Circuit breakers quando justificados.
- Cancelamento de operações.
- Idempotência.
- Limites de concorrência.
- Tratamento de indisponibilidade de dependências.

Retries não devem ser aplicados indiscriminadamente a operações não idempotentes.

Os valores de timeout, tentativas e demais limites devem considerar o comportamento real da operação e as necessidades do serviço, em vez de utilizar valores universais sem justificativa.

## 13. Regras de Validação e Conformidade

Um microsserviço ou alteração é considerado em desacordo com esta constituição quando, sem exceção aprovada e documentada:

- Viola a direção das dependências da Clean Architecture.
- Compartilha diretamente a persistência com outro microsserviço.
- Expõe modelos internos no contrato público da API.
- Permite acesso a dados de outro tenant sem autorização.
- Introduz segredos reais no controle de versão.
- Não possui testes adequados para comportamentos críticos.
- Não trata falhas previsíveis de integrações relevantes.
- Introduz dependências circulares entre projetos.
- Cria uma solution própria para o microsserviço.
- Deixa projetos .NET do monorepo fora da `Sentinel.sln`.
- Não atualiza a documentação afetada por alterações relevantes.
- Introduz mudanças incompatíveis sem avaliar seus impactos e a estratégia de migração.

A conformidade deve ser verificada por revisão de código, testes automatizados, análise da estrutura dos projetos e validações de build.

### 13.1. Verificações obrigatórias da solution

Antes de considerar concluída uma alteração estrutural:

1. Confirmar que `Sentinel.sln` existe na raiz.
2. Executar `dotnet sln Sentinel.sln list`.
3. Verificar se todos os projetos criados ou modificados estão registrados.
4. Confirmar que não foram criadas solutions específicas de microsserviços.
5. Executar `dotnet build Sentinel.sln`.
6. Executar `dotnet test Sentinel.sln`.
7. Investigar e corrigir falhas causadas pelas alterações.

A existência de projetos dentro de `services/` não é suficiente: eles precisam estar registrados na solution raiz.

## 14. Documentação

A documentação deve permanecer alinhada ao comportamento implementado.

Regras:

- Atualizar a documentação afetada por alterações de comportamento, arquitetura ou contratos.
- Documentar decisões técnicas relevantes e restrições não óbvias.
- Evitar comentários que apenas repetem o código.
- Utilizar exemplos que possam ser verificados.
- Não documentar comportamento pretendido como se já estivesse implementado sem verificar a implementação.
- Registrar decisões pendentes quando uma escolha ainda não tiver sido definida.
- Manter os comandos de build, teste e configuração alinhados à organização real do monorepo.

A documentação deve refletir o uso de uma única `Sentinel.sln` na raiz.

## 15. Gestão de Dependências e Ferramentas

### 15.1. SDK e pacotes

A versão do SDK .NET deve ser controlada pelo `global.json`.

As versões de pacotes devem ser mantidas de forma consistente entre projetos, utilizando gerenciamento centralizado quando aplicável.

Evitar atualizações de dependências sem avaliar compatibilidade, vulnerabilidades e impactos no restante do monorepo.

### 15.2. Ferramentas de desenvolvimento

Utilizar ferramentas compatíveis com o ecossistema e os padrões do projeto.

A criação e manutenção dos projetos devem respeitar a estrutura da solution raiz.

Comandos como `dotnet new sln` não devem ser utilizados na criação de microsserviços ou projetos individuais, pois o repositório já possui sua solution oficial.

### 15.3. Automação e CI/CD

Os pipelines devem validar, conforme aplicável:

- Restauração de dependências.
- Build da `Sentinel.sln`.
- Execução dos testes automatizados.
- Análise de qualidade.
- Verificação de vulnerabilidades.
- Validação de contratos.
- Publicação e empacotamento dos serviços.

Os pipelines podem executar etapas específicas por microsserviço para otimizar o tempo, mas devem preservar a integridade e a consistência do monorepo.

## 16. Princípios para Decisões Técnicas

Quando houver mais de uma solução tecnicamente viável, priorizar nesta ordem:

1. Correção e segurança.
2. Clareza e simplicidade.
3. Baixo acoplamento.
4. Manutenibilidade.
5. Testabilidade.
6. Observabilidade.
7. Resiliência.
8. Desempenho comprovado.
9. Facilidade de evolução.

Não introduzir complexidade arquitetural sem uma necessidade identificável.

Padrões como CQRS, event sourcing, abstrações genéricas, caches distribuídos e mecanismos avançados de resiliência devem ser adotados quando houver justificativa técnica, e não apenas por estarem disponíveis.

Decisões que alterem significativamente a arquitetura, os contratos públicos ou os limites entre microsserviços devem ser documentadas.

## 17. Regra Final

Esta constituição é a referência obrigatória para o desenvolvimento do Sentinel.

Todos os novos projetos, alterações arquiteturais e mudanças na organização do monorepo devem respeitar estas regras.

**A organização oficial do repositório é um monorepo com uma única solution: `Sentinel.sln`, localizada na raiz e contendo todos os projetos .NET de todos os microsserviços, incluindo os projetos de testes.**

Nenhum microsserviço deve possuir uma solution independente.
