using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AutoRebalCarteiraAPI.Tests;

public class CestaServiceTests
{
    private readonly CotahistParser _parser = new();

    private CestaService CreateService(AutoRebalCarteira.Data.AppDbContext db)
    {
        var config = TestHelper.CreateConfiguration();
        var serviceCollection = new ServiceCollection();
        var sp = serviceCollection.BuildServiceProvider();
        return new CestaService(db, _parser, config, sp);
    }

    [Fact]
    public async Task CadastrarCesta_PrimeiraCesta_DeveCriarComSucesso()
    {
        var db = TestHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var result = await service.CadastrarOuAlterarAsync(new CadastrarCestaRequest
        {
            Nome = "Top Five - Fev 2026",
            Itens =
            [
                new() { Ticker = "PETR4", Percentual = 30 },
                new() { Ticker = "VALE3", Percentual = 25 },
                new() { Ticker = "ITUB4", Percentual = 20 },
                new() { Ticker = "BBDC4", Percentual = 15 },
                new() { Ticker = "WEGE3", Percentual = 10 }
            ]
        });

        Assert.True(result.Ativa);
        Assert.False(result.RebalanceamentoDisparado);
        Assert.Equal(5, result.Itens.Count);
    }

    [Fact]
    public async Task CadastrarCesta_Menos5Ativos_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var result = await service.CadastrarOuAlterarAsync(new CadastrarCestaRequest
        {
            Nome = "Invalida",
            Itens =
            [
                new() { Ticker = "PETR4", Percentual = 50 },
                new() { Ticker = "VALE3", Percentual = 50 }
            ]
        });

        Assert.True(result.Failed);
        Assert.Equal("QUANTIDADE_ATIVOS_INVALIDA", result.ErrorCode);
    }

    [Fact]
    public async Task CadastrarCesta_PercentualNao100_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var result = await service.CadastrarOuAlterarAsync(new CadastrarCestaRequest
        {
            Nome = "Invalida",
            Itens =
            [
                new() { Ticker = "PETR4", Percentual = 30 },
                new() { Ticker = "VALE3", Percentual = 25 },
                new() { Ticker = "ITUB4", Percentual = 20 },
                new() { Ticker = "BBDC4", Percentual = 15 },
                new() { Ticker = "WEGE3", Percentual = 5 }
            ]
        });

        Assert.True(result.Failed);
        Assert.Equal("PERCENTUAIS_INVALIDOS", result.ErrorCode);
    }

    [Fact]
    public async Task CadastrarCesta_PercentualZero_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var result = await service.CadastrarOuAlterarAsync(new CadastrarCestaRequest
        {
            Nome = "Invalida",
            Itens =
            [
                new() { Ticker = "PETR4", Percentual = 40 },
                new() { Ticker = "VALE3", Percentual = 25 },
                new() { Ticker = "ITUB4", Percentual = 20 },
                new() { Ticker = "BBDC4", Percentual = 15 },
                new() { Ticker = "WEGE3", Percentual = 0 }
            ]
        });

        Assert.True(result.Failed);
        Assert.Equal("PERCENTUAIS_INVALIDOS", result.ErrorCode);
    }

    [Fact]
    public async Task ObterCestaAtual_SemCesta_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var service = CreateService(db);

        var result = await service.ObterCestaAtualAsync();

        Assert.True(result.Failed);
        Assert.Equal("CESTA_NAO_ENCONTRADA", result.ErrorCode);
    }

    [Fact]
    public async Task ObterCestaAtual_ComCesta_DeveRetornarCesta()
    {
        var db = TestHelper.CreateInMemoryDb();
        var service = CreateService(db);

        await service.CadastrarOuAlterarAsync(new CadastrarCestaRequest
        {
            Nome = "Top Five",
            Itens =
            [
                new() { Ticker = "PETR4", Percentual = 30 },
                new() { Ticker = "VALE3", Percentual = 25 },
                new() { Ticker = "ITUB4", Percentual = 20 },
                new() { Ticker = "BBDC4", Percentual = 15 },
                new() { Ticker = "WEGE3", Percentual = 10 }
            ]
        });

        var result = await service.ObterCestaAtualAsync();

        Assert.True(result.Ativa);
        Assert.Equal(5, result.Itens.Count);
    }

    [Fact]
    public async Task ObterHistorico_DeveRetornarTodasCestas()
    {
        var db = TestHelper.CreateInMemoryDb();
        var service = CreateService(db);

        await service.CadastrarOuAlterarAsync(new CadastrarCestaRequest
        {
            Nome = "Cesta 1",
            Itens =
            [
                new() { Ticker = "PETR4", Percentual = 30 },
                new() { Ticker = "VALE3", Percentual = 25 },
                new() { Ticker = "ITUB4", Percentual = 20 },
                new() { Ticker = "BBDC4", Percentual = 15 },
                new() { Ticker = "WEGE3", Percentual = 10 }
            ]
        });

        await service.CadastrarOuAlterarAsync(new CadastrarCestaRequest
        {
            Nome = "Cesta 2",
            Itens =
            [
                new() { Ticker = "PETR4", Percentual = 25 },
                new() { Ticker = "VALE3", Percentual = 20 },
                new() { Ticker = "ITUB4", Percentual = 20 },
                new() { Ticker = "ABEV3", Percentual = 20 },
                new() { Ticker = "RENT3", Percentual = 15 }
            ]
        });

        var result = await service.ObterHistoricoAsync();

        Assert.Equal(2, result.Cestas.Count);
        Assert.Single(result.Cestas.Where(c => c.Ativa));
    }
}
