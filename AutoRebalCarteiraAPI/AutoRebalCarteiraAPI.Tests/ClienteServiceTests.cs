using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteira.Domain.Exceptions;
using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Services;

namespace AutoRebalCarteiraAPI.Tests;

public class ClienteServiceTests
{
    private readonly CotahistParser _parser = new();

    [Fact]
    public async Task AderirAsync_DeveRegistrarCliente()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var result = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 3000
        });

        Assert.Equal("Joao", result.Nome);
        Assert.True(result.Ativo);
        Assert.Equal(3000, result.ValorMensal);
        Assert.NotNull(result.ContaGrafica);
        Assert.Equal("FILHOTE", result.ContaGrafica.Tipo);
    }

    [Fact]
    public async Task AderirAsync_CpfDuplicado_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 3000
        });

        var result = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Maria",
            Cpf = "12345678901",
            Email = "maria@test.com",
            ValorMensal = 5000
        });

        Assert.True(result.Failed);
        Assert.Equal("CLIENTE_CPF_DUPLICADO", result.ErrorCode);
    }

    [Fact]
    public async Task AderirAsync_ValorMensalInvalido_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var result = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 50
        });

        Assert.True(result.Failed);
        Assert.Equal("VALOR_MENSAL_INVALIDO", result.ErrorCode);
    }

    [Fact]
    public async Task SairAsync_DeveDesativarCliente()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var adesao = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 3000
        });

        var result = await service.SairAsync(adesao.ClienteId);

        Assert.False(result.Ativo);
        Assert.NotNull(result.DataSaida);
    }

    [Fact]
    public async Task SairAsync_ClienteInexistente_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var result = await service.SairAsync(999);

        Assert.True(result.Failed);
        Assert.Equal("CLIENTE_NAO_ENCONTRADO", result.ErrorCode);
    }

    [Fact]
    public async Task SairAsync_ClienteJaInativo_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var adesao = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 3000
        });

        await service.SairAsync(adesao.ClienteId);

        var result = await service.SairAsync(adesao.ClienteId);

        Assert.True(result.Failed);
        Assert.Equal("CLIENTE_JA_INATIVO", result.ErrorCode);
    }

    [Fact]
    public async Task AlterarValorMensalAsync_DeveAtualizarValor()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var adesao = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 3000
        });

        var result = await service.AlterarValorMensalAsync(adesao.ClienteId, new AlterarValorMensalRequest
        {
            NovoValorMensal = 6000
        });

        Assert.Equal(3000, result.ValorMensalAnterior);
        Assert.Equal(6000, result.ValorMensalNovo);
    }

    [Fact]
    public async Task AlterarValorMensalAsync_ValorInvalido_DeveRetornarFailed()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var adesao = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 3000
        });

        var result = await service.AlterarValorMensalAsync(adesao.ClienteId, new AlterarValorMensalRequest
        {
            NovoValorMensal = 10
        });

        Assert.True(result.Failed);
        Assert.Equal("VALOR_MENSAL_INVALIDO", result.ErrorCode);
    }

    [Fact]
    public async Task ConsultarCarteiraAsync_ClienteSemAtivos_DeveRetornarVazia()
    {
        var db = TestHelper.CreateInMemoryDb();
        var config = TestHelper.CreateConfiguration();
        var service = new ClienteService(db, _parser, config);

        var adesao = await service.AderirAsync(new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "joao@test.com",
            ValorMensal = 3000
        });

        var result = await service.ConsultarCarteiraAsync(adesao.ClienteId);

        Assert.Equal("Joao", result.Nome);
        Assert.Empty(result.Ativos);
        Assert.Equal(0, result.Resumo.ValorTotalInvestido);
    }
}
