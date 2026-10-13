using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SfWebAPIHeraldo.IntegrationTests;

[Collection(PartnerDatabaseCollection.Name)]
public sealed class PartnerApiIntegrationTests(SqlServerApiFixture fixture) : IAsyncLifetime
{
    private HttpClient Client => fixture.Client!;

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [SkippableFact]
    public async Task Criar_cliente_normaliza_nome_e_nif_e_mantem_nif_reservado_quando_inativo()
    {
        Skip.IfNot(
            fixture.IsConfigured,
            "Set SFWEBAPI_TEST_CONNECTIONSTRING to run SQL Server integration tests."
        );

        using var created = await CreateAsync("  Ana Silva  ", "001.234.567");
        Assert.Equal("Ana Silva", created.RootElement.GetProperty("name").GetString());
        Assert.Equal("001234567", created.RootElement.GetProperty("taxNumber").GetString());

        var partnerId = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("version").GetString()!;
        using var deactivated = await UpdateAsync(
            partnerId,
            "Ana Silva",
            "001234567",
            "CUSTOMER",
            false,
            version
        );
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        using var duplicate = await PostAsync("Outra Ana", "001 234 567");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var duplicateBody = await ReadBodyAsync(duplicate);
        Assert.Equal(
            "DUPLICATE_TAX_NUMBER",
            duplicateBody.RootElement.GetProperty("errorCode").GetString()
        );
    }

    [SkippableFact]
    public async Task Entidade_both_aparece_em_customer_supplier_e_both_e_customer_nao_aparece_em_supplier()
    {
        Skip.IfNot(
            fixture.IsConfigured,
            "Set SFWEBAPI_TEST_CONNECTIONSTRING to run SQL Server integration tests."
        );

        using var both = await CreateAsync("Bia Ambos", "100.000.001", "BOTH");
        using var customer = await CreateAsync("Ana Cliente", "100.000.002", "CUSTOMER");
        using var supplier = await CreateAsync("Sofia Fornecedor", "100.000.003", "SUPPLIER");

        using var customerFilter = await GetListAsync("?type=CUSTOMER");
        Assert.Contains(
            ReadItems(customerFilter),
            item => item.GetProperty("id").GetGuid() == both.RootElement.GetProperty("id").GetGuid()
        );
        Assert.Contains(
            ReadItems(customerFilter),
            item =>
                item.GetProperty("id").GetGuid() == customer.RootElement.GetProperty("id").GetGuid()
        );

        using var supplierFilter = await GetListAsync("?type=SUPPLIER");
        Assert.Contains(
            ReadItems(supplierFilter),
            item => item.GetProperty("id").GetGuid() == both.RootElement.GetProperty("id").GetGuid()
        );
        Assert.Contains(
            ReadItems(supplierFilter),
            item =>
                item.GetProperty("id").GetGuid() == supplier.RootElement.GetProperty("id").GetGuid()
        );
        Assert.DoesNotContain(
            ReadItems(supplierFilter),
            item =>
                item.GetProperty("id").GetGuid() == customer.RootElement.GetProperty("id").GetGuid()
        );

        using var bothFilter = await GetListAsync("?type=BOTH");
        Assert.Collection(
            ReadItems(bothFilter),
            item =>
                Assert.Equal(
                    both.RootElement.GetProperty("id").GetGuid(),
                    item.GetProperty("id").GetGuid()
                )
        );
    }

    [SkippableFact]
    public async Task Desativacao_esconde_registo_por_defeito_mas_mantem_acesso_por_id_e_reativacao_retorna_a_listagem()
    {
        Skip.IfNot(
            fixture.IsConfigured,
            "Set SFWEBAPI_TEST_CONNECTIONSTRING to run SQL Server integration tests."
        );

        using var created = await CreateAsync("Ana Silva", "200.000.001");
        var id = created.RootElement.GetProperty("id").GetGuid();
        using var deactivated = await UpdateAsync(
            id,
            "Ana Silva",
            "200000001",
            "CUSTOMER",
            false,
            created.RootElement.GetProperty("version").GetString()!
        );
        using var hidden = await GetListAsync();
        Assert.DoesNotContain(ReadItems(hidden), item => item.GetProperty("id").GetGuid() == id);

        using var inactive = await GetListAsync("?includeInactive=true");
        Assert.Contains(ReadItems(inactive), item => item.GetProperty("id").GetGuid() == id);

        using var byId = await Client.GetAsync($"/api/partners/{id}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);

        using var reactivated = await UpdateAsync(
            id,
            "Ana Silva",
            "200000001",
            "CUSTOMER",
            true,
            (await ReadBodyAsync(deactivated)).RootElement.GetProperty("version").GetString()!
        );
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);

        using var listed = await GetListAsync();
        Assert.Contains(ReadItems(listed), item => item.GetProperty("id").GetGuid() == id);
    }

    [SkippableFact]
    public async Task Atualizar_permite_manter_o_proprio_nif_e_rejeita_nif_de_outra_entidade_sem_alterar_dados()
    {
        Skip.IfNot(
            fixture.IsConfigured,
            "Set SFWEBAPI_TEST_CONNECTIONSTRING to run SQL Server integration tests."
        );

        using var first = await CreateAsync("Ana Original", "300.000.001");
        using var second = await CreateAsync("Bia Outra", "300.000.002");
        var firstId = first.RootElement.GetProperty("id").GetGuid();

        using var ownNumberUpdate = await UpdateAsync(
            firstId,
            "Ana Atualizada",
            "300000001",
            "CUSTOMER",
            true,
            first.RootElement.GetProperty("version").GetString()!
        );
        Assert.Equal(HttpStatusCode.OK, ownNumberUpdate.StatusCode);
        using var updated = await ReadBodyAsync(ownNumberUpdate);

        using var duplicateUpdate = await UpdateAsync(
            firstId,
            "Nome Não Guardado",
            second.RootElement.GetProperty("taxNumber").GetString()!,
            "SUPPLIER",
            true,
            updated.RootElement.GetProperty("version").GetString()!
        );
        Assert.Equal(HttpStatusCode.Conflict, duplicateUpdate.StatusCode);

        using var unchanged = await Client.GetAsync($"/api/partners/{firstId}");
        using var unchangedBody = await ReadBodyAsync(unchanged);
        Assert.Equal("Ana Atualizada", unchangedBody.RootElement.GetProperty("name").GetString());
        Assert.Equal("300000001", unchangedBody.RootElement.GetProperty("taxNumber").GetString());
    }

    [SkippableFact]
    public async Task Criacoes_e_atualizacoes_concorrentes_com_mesmo_nif_e_version_devolvem_conflito()
    {
        Skip.IfNot(
            fixture.IsConfigured,
            "Set SFWEBAPI_TEST_CONNECTIONSTRING to run SQL Server integration tests."
        );

        var creates = await Task.WhenAll(
            PostAsync("Ana Concorrente A", "400.000.001"),
            PostAsync("Ana Concorrente B", "400 000 001")
        );
        Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in creates)
            response.Dispose();

        using var list = await GetListAsync("?search=400000001");
        Assert.Equal(1, list.RootElement.GetProperty("totalCount").GetInt32());
        using var created = ReadItems(list).Single().CloneDocument();
        var id = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("version").GetString()!;

        var updates = await Task.WhenAll(
            UpdateAsync(id, "Atualização A", "400000001", "CUSTOMER", true, version),
            UpdateAsync(id, "Atualização B", "400000001", "CUSTOMER", true, version)
        );
        Assert.Single(updates, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(updates, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in updates)
            response.Dispose();
    }

    [SkippableFact]
    public async Task Credito_nif_pagina_tipo_e_version_invalidos_devolvem_400_sem_gravar()
    {
        Skip.IfNot(
            fixture.IsConfigured,
            "Set SFWEBAPI_TEST_CONNECTIONSTRING to run SQL Server integration tests."
        );

        using var existing = await CreateAsync("Ana Original", "500.000.001");

        using var badCredit = await PostAsync(
            "Crédito Inválido",
            "500.000.002",
            creditLimit: 10.999m
        );
        Assert.Equal(HttpStatusCode.BadRequest, badCredit.StatusCode);

        using var badTaxNumber = await PostAsync("NIF Inválido", "50A.000.003");
        Assert.Equal(HttpStatusCode.BadRequest, badTaxNumber.StatusCode);

        using var badPage = await GetRawAsync("?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);

        using var badType = await GetRawAsync("?type=NOT_A_TYPE");
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);

        var existingId = existing.RootElement.GetProperty("id").GetGuid();
        using var badVersion = await UpdateAsync(
            existingId,
            "Nome Não Alterado",
            "500000001",
            "CUSTOMER",
            true,
            "not-base64"
        );
        Assert.Equal(HttpStatusCode.BadRequest, badVersion.StatusCode);
        using var validationBody = await ReadBodyAsync(badVersion);
        Assert.Equal(
            "VALIDATION_ERROR",
            validationBody.RootElement.GetProperty("errorCode").GetString()
        );

        using var byId = await Client.GetAsync($"/api/partners/{existingId}");
        using var persisted = await ReadBodyAsync(byId);
        Assert.Equal("Ana Original", persisted.RootElement.GetProperty("name").GetString());
        using var all = await GetListAsync("?includeInactive=true");
        Assert.Equal(1, all.RootElement.GetProperty("totalCount").GetInt32());
    }

    [SkippableFact]
    public async Task Filtros_combinados_total_count_antes_da_paginacao_search_case_insensitive_e_desempate_por_id()
    {
        Skip.IfNot(
            fixture.IsConfigured,
            "Set SFWEBAPI_TEST_CONNECTIONSTRING to run SQL Server integration tests."
        );

        using var both = await CreateAsync("Ana Silva", "600.000.001", "BOTH");
        using var customer = await CreateAsync("Ana Silva", "600.000.002", "CUSTOMER");
        using var supplier = await CreateAsync("Ana Sousa", "600.000.003", "SUPPLIER");
        using var other = await CreateAsync("Bia Cliente", "600.000.004", "CUSTOMER");

        var expectedTiedIds = (await fixture.GetIdsOrderedByNameThenIdAsync())
            .Where(id =>
                id == both.RootElement.GetProperty("id").GetGuid()
                || id == customer.RootElement.GetProperty("id").GetGuid()
            )
            .ToArray();

        using var firstPage = await GetListAsync("?search=ana&type=CUSTOMER&page=1&pageSize=1");
        Assert.Equal(2, firstPage.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Single(ReadItems(firstPage));
        Assert.Equal(expectedTiedIds[0], ReadItems(firstPage)[0].GetProperty("id").GetGuid());

        using var secondPage = await GetListAsync("?search=ANA&type=CUSTOMER&page=2&pageSize=1");
        Assert.Equal(expectedTiedIds[1], ReadItems(secondPage)[0].GetProperty("id").GetGuid());

        using var nameSearch = await GetListAsync("?search=Ana&pageSize=20");
        Assert.Equal(3, nameSearch.RootElement.GetProperty("totalCount").GetInt32());
        Assert.DoesNotContain(
            ReadItems(nameSearch),
            item =>
                item.GetProperty("id").GetGuid() == other.RootElement.GetProperty("id").GetGuid()
        );
    }

    private async Task<JsonDocument> CreateAsync(
        string name,
        string taxNumber,
        string type = "CUSTOMER",
        decimal creditLimit = 10m
    )
    {
        using var response = await PostAsync(name, taxNumber, type, creditLimit);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadBodyAsync(response);
    }

    private Task<HttpResponseMessage> PostAsync(
        string name,
        string taxNumber,
        string type = "CUSTOMER",
        decimal creditLimit = 10m
    ) =>
        Client.PostAsJsonAsync(
            "/api/partners",
            new
            {
                Name = name,
                TaxNumber = taxNumber,
                Type = type,
                CreditLimit = creditLimit,
            }
        );

    private Task<HttpResponseMessage> UpdateAsync(
        Guid id,
        string name,
        string taxNumber,
        string type,
        bool isActive,
        string version
    ) =>
        Client.PutAsJsonAsync(
            $"/api/partners/{id}",
            new
            {
                Name = name,
                TaxNumber = taxNumber,
                Type = type,
                CreditLimit = 10m,
                IsActive = isActive,
                Version = version,
            }
        );

    private async Task<JsonDocument> GetListAsync(string query = "")
    {
        using var response = await GetRawAsync(query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadBodyAsync(response);
    }

    private Task<HttpResponseMessage> GetRawAsync(string query = "") =>
        Client.GetAsync($"/api/partners{query}");

    private static async Task<JsonDocument> ReadBodyAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    private static JsonElement[] ReadItems(JsonDocument document) =>
        document
            .RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
}

internal static class JsonElementTestExtensions
{
    public static JsonDocument CloneDocument(this JsonElement element) =>
        JsonDocument.Parse(element.GetRawText());
}
