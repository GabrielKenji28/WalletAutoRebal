using AutoRebalCarteira.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AutoRebalCarteiraAPI.Tests;

public static class TestHelper
{
    public static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static IConfiguration CreateConfiguration(string pastaCotacoes = "cotacoes")
    {
        var configData = new Dictionary<string, string?>
        {
            ["Cotahist:PastaCotacoes"] = pastaCotacoes,
            ["Kafka:BootstrapServers"] = "localhost:9092"
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }
}
