# Sf-Web API

API ASP.NET Core para gestão de clientes e fornecedores, Teste técnico — Fase 2 para a ******** Web.

![Version](https://img.shields.io/badge/version-1.0.0-blue)

## Inicialização rápida (do clone ao run)

1. Clone do repositório:

```bash
git clone <url-do-repositorio>
cd SfWebAPIHeraldo
```

2. Instale as dependências:

```bash
dotnet restore
```

3. Verifique a connection string do SQL Server em `appsettings.json`:

```json
"ConnectionStrings": {
## Pré-requisitos

Se estiver a iniciar o projeto numa máquina nova, confirme que tem instalado o SDK correto e as ferramentas de Entity Framework Core antes de executar a API ou as migrações.

1. Instale o SDK .NET 10.

```bash
dotnet --version
```

Se o comando não devolver uma versão 10.x, instale o .NET 10 SDK antes de continuar.

2. Instale a CLI do Entity Framework Core, caso o comando `dotnet ef` ainda não exista:

```bash
dotnet tool install --global dotnet-ef
```

3. Verifique se a ferramenta está disponível:

```bash
dotnet ef --version
```

4. Confirme também que o SQL Server local está acessível a partir da máquina e que a connection string configurada em `appsettings.json` aponta para uma instância válida.

  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SF_WEBAPI;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

4. Crie a base de dados e aplique as migrações:

```bash
dotnet ef database update
```


5. Inicie a API:

```bash
dotnet run
```

> Ao iniciar a aplicação, o código de bootstrap executa automaticamente o seeding inicial da base de dados. Isto significa que o `dotnet run` também preenche a tabela com dados de exemplo, caso a base esteja vazia.

6. Acesse a documentação Swagger:

- http://localhost:5092/swagger

> Se o SQL Server não estiver disponível, a aplicação não iniciará corretamente porque a API depende da connection string configurada.

## Stack tecnológica

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- FluentValidation
- Swagger / OpenAPI
- xUnit
- ASP.NET Core Testing / WebApplicationFactory

## Estrutura da solução

```text
SfWebAPIHeraldo/
├── Application/
│   └── Controllers/
│   |    └── PartnersController.cs
│   └── Services/
│       └── PartnerService.cs
├── Infrastructure/
│   ├── Data/
│   │   └── AppDbContext.
│   │   └── PartnerSeeder.cs
│   ├── Dto/
│   │   └── Partners/
│   │       ├── CreateDto.cs
│   │       └── UpdateDto.cs
│   ├── Models/
│   │   ├── Partner.cs
│   │   └── TaxNumberNormalizer.cs
│   └── Validators/
│       ├── CreatePartnerDtoValidator.cs
│       └── UpdatePartnerDtoValidator.cs
├── Middleware/
│   └── GlobalExceptionHandler.cs
├── Migrations/
├── tests/
│   └── SfWebAPIHeraldo.IntegrationTests/
│       ├── PartnerApiIntegrationTests.cs
│       ├── SqlServerApiFixture.cs
│       └── ...
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── SfWebAPIHeraldo.csproj
├── README.md
├── README-comandos.md
├── partners.http
├── .gitignore
└── Properties/
    └── launchSettings.json
```

## Endpoints principais

### GET /api/partners
Lista parceiros com paginação, filtro por texto e por tipo.

Query parameters:
- `search`: texto para filtrar por nome ou NIF
- `type`: `CUSTOMER`, `SUPPLIER` ou `BOTH`
- `includeInactive`: `true` para incluir inativos
- `page`: página atual
- `pageSize`: quantidade por página

### GET /api/partners/{id}
Retorna um parceiro por identificador.

### POST /api/partners
Cria um novo parceiro.

### PUT /api/partners/{id}
Atualiza um parceiro existente com controlo de concorrência por `version`.

## Exemplos HTTP

### Arquivo de exemplo
Crie um ficheiro `partners.http` com o seguinte conteúdo:

```http
@baseUrl = https://localhost:5001

### Listar parceiros
GET {{baseUrl}}/api/partners?page=1&pageSize=10
Accept: application/json

### Filtrar por nome
GET {{baseUrl}}/api/partners?search=ana&type=CUSTOMER&page=1&pageSize=10
Accept: application/json

### Consultar por id
GET {{baseUrl}}/api/partners/11111111-2222-3333-4444-555555666666
Accept: application/json

### Criar parceiro
POST {{baseUrl}}/api/partners
Content-Type: application/json

{
  "name": "Ana Silva",
  "taxNumber": "001.234.567",
  "type": "CUSTOMER",
  "email": "ana.silva@email.com",
  "creditLimit": 2500.00
}

### Atualizar parceiro
PUT {{baseUrl}}/api/partners/11111111-2222-3333-4444-555555666666
Content-Type: application/json

{
  "name": "Ana Silva",
  "taxNumber": "001234567",
  "type": "CUSTOMER",
  "email": "ana.silva@novaempresa.com",
  "creditLimit": 3200.00,
  "isActive": true,
  "version": "AQAA..."
}
```

### Exemplos com curl

```bash
curl -X GET "https://localhost:5001/api/partners?page=1&pageSize=5" \
  -H "Accept: application/json"

curl -X POST "https://localhost:5001/api/partners" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "João Costa",
    "taxNumber": "987.654.321",
    "type": "SUPPLIER",
    "email": "joao.costa@email.com",
    "creditLimit": 1500.50
  }'
```

### Estrutura da payload

```json
{
  "name": "Maria Santos",
  "taxNumber": "123456789",
  "type": "CUSTOMER",
  "email": "maria@exemplo.com",
  "creditLimit": 5000.00
}
```

## Testes Automatizados

Os testes de integração cumprem com o cenário mínimo de 7 testes, os mesmos exigem uma connection string para um SQL Server de testes. Defina a variável de ambiente via script e corra as demais scripts no mesmo terminal:

```powershell
$env:SFWEBAPI_TEST_CONNECTIONSTRING = "Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"
```

Depois execute (Todos os testes de integração):

```powershell
dotnet test .\tests\SfWebAPI.IntegrationTests\SfWebAPI.IntegrationTests.csproj
```

ou Executar um único teste pelo nome:

```powershell
dotnet test .\tests\SfWebAPI.IntegrationTests\SfWebAPI.IntegrationTests.csproj --filter "Criar_cliente_normaliza_nome_e_nif_e_mantem_nif_reservado_quando_inativo"
```

Nomes dos testes atuais:

- `Criar_cliente_normaliza_nome_e_nif_e_mantem_nif_reservado_quando_inativo`
- `Entidade_both_aparece_em_customer_supplier_e_both_e_customer_nao_aparece_em_supplier`
- `Desativacao_esconde_registo_por_defeito_mas_mantem_acesso_por_id_e_reativacao_retorna_a_listagem`
- `Atualizar_permite_manter_o_proprio_nif_e_rejeita_nif_de_outra_entidade_sem_alterar_dados`
- `Criacoes_e_atualizacoes_concorrentes_com_mesmo_nif_e_version_devolvem_conflito`
- `Credito_nif_pagina_tipo_e_version_invalidos_devolvem_400_sem_gravar`
- `Filtros_combinados_total_count_antes_da_paginacao_search_case_insensitive_e_desempate_por_id`

## Formato de erros e validações

A API responde com erros em JSON, seguindo este padrão:

```json
{
  "status": 400,
  "errorCode": "VALIDATION_ERROR",
  "message": "Um ou mais valores fornecidos são inválidos."
}
```

Quando há conflito de NIF ou de concorrência, por exemplo:

```json
{
  "status": 409,
  "errorCode": "DUPLICATE_TAX_NUMBER",
  "message": "Já existe um parceiro com este NIF."
}
```

ou

```json
{
  "status": 409,
  "errorCode": "CONCURRENCY_CONFLICT",
  "message": "O registo foi modificado por outro utilizador. Recarregue e tente novamente."
}
```

## Observações

- O campo `taxNumber` é normalizado automaticamente.
- O campo `version` é obrigatório em `PUT` para controlo de concorrência.
- O `type` é sensível a maiúsculas/minúsculas, mas a API aceita valores como `CUSTOMER`, `SUPPLIER` e `BOTH`.
- Se a aplicação estiver em ambiente de desenvolvimento, o Swagger estará disponível em `/swagger`.
