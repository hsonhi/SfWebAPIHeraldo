using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SfWebAPIHeraldo.Data;
using Xunit;

namespace SfWebAPIHeraldo.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PartnerDatabaseCollection : ICollectionFixture<SqlServerApiFixture>
{
    public const string Name = "Partner SQL Server integration";
}

public sealed class SqlServerApiFixture : IAsyncLifetime
{
    private readonly string? _adminConnectionString = Environment.GetEnvironmentVariable(
        "SFWEBAPI_TEST_CONNECTIONSTRING"
    );
    private string? _databaseName;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_adminConnectionString);
    public string? TestConnectionString { get; private set; }
    public HttpClient? Client { get; private set; }

    private PartnerApiFactory? _factory;

    public async Task InitializeAsync()
    {
        if (!IsConfigured)
            return;

        _databaseName = $"SfWebAPITest_{Guid.NewGuid():N}";
        var adminBuilder = new SqlConnectionStringBuilder(_adminConnectionString)
        {
            InitialCatalog = "master",
        };
        var testBuilder = new SqlConnectionStringBuilder(_adminConnectionString)
        {
            InitialCatalog = _databaseName,
        };
        TestConnectionString = testBuilder.ConnectionString;

        await using (var connection = new SqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        _factory = new PartnerApiFactory(TestConnectionString);
        Client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        if (!IsConfigured || _factory is null)
            return;

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Partners.ExecuteDeleteAsync();
    }

    public async Task<IReadOnlyList<Guid>> GetIdsOrderedByNameThenIdAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext
            .Partners.OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id)
            .Select(partner => partner.Id)
            .ToListAsync();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();

        if (!IsConfigured || _databaseName is null)
            return;

        var adminBuilder = new SqlConnectionStringBuilder(_adminConnectionString)
        {
            InitialCatalog = "master",
        };
        await using var connection = new SqlConnection(adminBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class PartnerApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] = connectionString,
                        }
                    )
            );
        }
    }
}
