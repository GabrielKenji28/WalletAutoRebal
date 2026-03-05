using AutoRebalCarteira.Domain.Entities;
using AutoRebalCarteira.Domain.Exceptions;
using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteira.Data.Infrastructure.Kafka;
using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AutoRebalCarteiraAPI.Tests;

public class MotorCompraServiceTests
{
    private static string CreateTestCotacoesDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);

        var linhas = new[]
        {
            "00COTAHIST.2026 BOVESPA  20260225                                                                                                                                                                                                                    ",
            "01202602250200PETR4       010PETROBRAS   PN      N1   R$  0000000003520000000003650000000003480000000003560000000003580000000003570000000003590034561000000000150000000000000005376000000000000000000000000000000000000000BRPETRACNPR6180",
            "01202602250200VALE3       010VALE        ON      NM   R$  0000000006150000000006300000000006100000000006200000000006200000000006180000000006250025430000000000120000000000000007440000000000000000000000000000000000000000BRVALEACNOR0180",
            "01202602250200ITUB4       010ITAUUNIBANCOPN      N1   R$  0000000002950000000003050000000002920000000002980000000003000000000002990000000003010045670000000000180000000000000005400000000000000000000000000000000000000000BRITUBACNPR1180",
            "01202602250200BBDC4       010BRADESCO    PN      N1   R$  0000000001480000000001550000000001460000000001500000000001500000000001490000000001520056780000000000250000000000000003750000000000000000000000000000000000000000BRBBDCACNPR2180",
            "01202602250200WEGE3       010WEG         ON      NM   R$  0000000003950000000004050000000003900000000003980000000004000000000003970000000004020023450000000000100000000000000004000000000000000000000000000000000000000000BRWEGEACNOR6180",
            "99COTAHIST.2026 BOVESPA  2026022500000006                                                                                                                                                                                                        "
        };

        File.WriteAllLines(Path.Combine(dir, "COTAHIST_D20260225.TXT"), linhas);
        return dir;
    }

    [Fact]
    public async Task ExecutarCompra_SemClientesAtivos_DeveRetornarFailed()
    {
        var dir = CreateTestCotacoesDir();
        try
        {
            var db = TestHelper.CreateInMemoryDb();
            var config = TestHelper.CreateConfiguration(dir);
            var kafka = new Mock<IKafkaProducerService>();
            var logger = new Mock<ILogger<MotorCompraService>>();
            var parser = new CotahistParser();

            // Cadastrar cesta
            var cesta = new CestaRecomendacao
            {
                Nome = "Top Five",
                Ativo = true,
                DataCriacao = DateTime.UtcNow,
                Itens =
                [
                    new() { Ticker = "PETR4", Percentual = 30 },
                    new() { Ticker = "VALE3", Percentual = 25 },
                    new() { Ticker = "ITUB4", Percentual = 20 },
                    new() { Ticker = "BBDC4", Percentual = 15 },
                    new() { Ticker = "WEGE3", Percentual = 10 }
                ]
            };
            db.CestasRecomendacao.Add(cesta);
            await db.SaveChangesAsync();

            var service = new MotorCompraService(db, parser, kafka.Object, config, logger.Object);

            var result = await service.ExecutarCompraAsync(new DateOnly(2026, 2, 25));

            Assert.True(result.Failed);
            Assert.Equal("SEM_CLIENTES_ATIVOS", result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExecutarCompra_SemCesta_DeveRetornarFailed()
    {
        var dir = CreateTestCotacoesDir();
        try
        {
            var db = TestHelper.CreateInMemoryDb();
            var config = TestHelper.CreateConfiguration(dir);
            var kafka = new Mock<IKafkaProducerService>();
            var logger = new Mock<ILogger<MotorCompraService>>();
            var parser = new CotahistParser();

            var service = new MotorCompraService(db, parser, kafka.Object, config, logger.Object);

            var result = await service.ExecutarCompraAsync(new DateOnly(2026, 2, 25));

            Assert.True(result.Failed);
            Assert.Equal("CESTA_NAO_ENCONTRADA", result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExecutarCompra_DeveComprarEDistribuir()
    {
        var dir = CreateTestCotacoesDir();
        try
        {
            var db = TestHelper.CreateInMemoryDb();
            var config = TestHelper.CreateConfiguration(dir);
            var kafka = new Mock<IKafkaProducerService>();
            kafka.Setup(k => k.PublicarIRDedoDuroAsync(It.IsAny<IRDedoDuroMessage>()))
                .Returns(Task.CompletedTask);
            var logger = new Mock<ILogger<MotorCompraService>>();
            var parser = new CotahistParser();

            // Cesta
            var cesta = new CestaRecomendacao
            {
                Nome = "Top Five",
                Ativo = true,
                DataCriacao = DateTime.UtcNow,
                Itens =
                [
                    new() { Ticker = "PETR4", Percentual = 30 },
                    new() { Ticker = "VALE3", Percentual = 25 },
                    new() { Ticker = "ITUB4", Percentual = 20 },
                    new() { Ticker = "BBDC4", Percentual = 15 },
                    new() { Ticker = "WEGE3", Percentual = 10 }
                ]
            };
            db.CestasRecomendacao.Add(cesta);

            // Clientes
            var clienteA = new Cliente
            {
                Nome = "Cliente A",
                Cpf = "11111111111",
                Email = "a@test.com",
                ValorMensal = 3000,
                Ativo = true,
                DataAdesao = DateTime.UtcNow,
                ContaGrafica = new ContaGrafica
                {
                    NumeroConta = "FLH-000001",
                    Tipo = "FILHOTE",
                    DataCriacao = DateTime.UtcNow
                }
            };
            var clienteB = new Cliente
            {
                Nome = "Cliente B",
                Cpf = "22222222222",
                Email = "b@test.com",
                ValorMensal = 6000,
                Ativo = true,
                DataAdesao = DateTime.UtcNow,
                ContaGrafica = new ContaGrafica
                {
                    NumeroConta = "FLH-000002",
                    Tipo = "FILHOTE",
                    DataCriacao = DateTime.UtcNow
                }
            };
            db.Clientes.AddRange(clienteA, clienteB);
            await db.SaveChangesAsync();

            var service = new MotorCompraService(db, parser, kafka.Object, config, logger.Object);
            var result = await service.ExecutarCompraAsync(new DateOnly(2026, 2, 25));

            Assert.Equal(2, result.TotalClientes);
            Assert.Equal(3000, result.TotalConsolidado); // 1000 + 2000
            Assert.NotEmpty(result.OrdensCompra);
            Assert.Equal(2, result.Distribuicoes.Count);
            Assert.True(result.EventosIRPublicados > 0);

            // Verificar que IR dedo-duro foi publicado
            kafka.Verify(k => k.PublicarIRDedoDuroAsync(It.IsAny<IRDedoDuroMessage>()), Times.AtLeastOnce);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExecutarCompra_DuplicadaMesmaData_DeveRetornarFailed()
    {
        var dir = CreateTestCotacoesDir();
        try
        {
            var db = TestHelper.CreateInMemoryDb();
            var config = TestHelper.CreateConfiguration(dir);
            var kafka = new Mock<IKafkaProducerService>();
            kafka.Setup(k => k.PublicarIRDedoDuroAsync(It.IsAny<IRDedoDuroMessage>()))
                .Returns(Task.CompletedTask);
            var logger = new Mock<ILogger<MotorCompraService>>();
            var parser = new CotahistParser();

            var cesta = new CestaRecomendacao
            {
                Nome = "Top Five",
                Ativo = true,
                DataCriacao = DateTime.UtcNow,
                Itens =
                [
                    new() { Ticker = "PETR4", Percentual = 30 },
                    new() { Ticker = "VALE3", Percentual = 25 },
                    new() { Ticker = "ITUB4", Percentual = 20 },
                    new() { Ticker = "BBDC4", Percentual = 15 },
                    new() { Ticker = "WEGE3", Percentual = 10 }
                ]
            };
            db.CestasRecomendacao.Add(cesta);

            db.Clientes.Add(new Cliente
            {
                Nome = "Test",
                Cpf = "99999999999",
                Email = "t@t.com",
                ValorMensal = 300,
                Ativo = true,
                DataAdesao = DateTime.UtcNow,
                ContaGrafica = new ContaGrafica
                {
                    NumeroConta = "FLH-000099",
                    Tipo = "FILHOTE",
                    DataCriacao = DateTime.UtcNow
                }
            });
            await db.SaveChangesAsync();

            var service = new MotorCompraService(db, parser, kafka.Object, config, logger.Object);
            await service.ExecutarCompraAsync(new DateOnly(2026, 2, 25));

            var result = await service.ExecutarCompraAsync(new DateOnly(2026, 2, 25));

            Assert.True(result.Failed);
            Assert.Equal("COMPRA_JA_EXECUTADA", result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExecutarCompra_DistribuicaoProporcional_DeveRespeitarProporcao()
    {
        var dir = CreateTestCotacoesDir();
        try
        {
            var db = TestHelper.CreateInMemoryDb();
            var config = TestHelper.CreateConfiguration(dir);
            var kafka = new Mock<IKafkaProducerService>();
            kafka.Setup(k => k.PublicarIRDedoDuroAsync(It.IsAny<IRDedoDuroMessage>()))
                .Returns(Task.CompletedTask);
            var logger = new Mock<ILogger<MotorCompraService>>();
            var parser = new CotahistParser();

            var cesta = new CestaRecomendacao
            {
                Nome = "Top Five",
                Ativo = true,
                DataCriacao = DateTime.UtcNow,
                Itens =
                [
                    new() { Ticker = "PETR4", Percentual = 30 },
                    new() { Ticker = "VALE3", Percentual = 25 },
                    new() { Ticker = "ITUB4", Percentual = 20 },
                    new() { Ticker = "BBDC4", Percentual = 15 },
                    new() { Ticker = "WEGE3", Percentual = 10 }
                ]
            };
            db.CestasRecomendacao.Add(cesta);

            // Cliente A: 3000/3 = 1000 (33.33%)
            // Cliente B: 6000/3 = 2000 (66.67%)
            db.Clientes.Add(new Cliente
            {
                Nome = "A",
                Cpf = "11111111111",
                Email = "a@t.com",
                ValorMensal = 3000,
                Ativo = true,
                DataAdesao = DateTime.UtcNow,
                ContaGrafica = new ContaGrafica
                {
                    NumeroConta = "FLH-000001",
                    Tipo = "FILHOTE",
                    DataCriacao = DateTime.UtcNow
                }
            });
            db.Clientes.Add(new Cliente
            {
                Nome = "B",
                Cpf = "22222222222",
                Email = "b@t.com",
                ValorMensal = 6000,
                Ativo = true,
                DataAdesao = DateTime.UtcNow,
                ContaGrafica = new ContaGrafica
                {
                    NumeroConta = "FLH-000002",
                    Tipo = "FILHOTE",
                    DataCriacao = DateTime.UtcNow
                }
            });
            await db.SaveChangesAsync();

            var service = new MotorCompraService(db, parser, kafka.Object, config, logger.Object);
            var result = await service.ExecutarCompraAsync(new DateOnly(2026, 2, 25));

            var distA = result.Distribuicoes.First(d => d.Nome == "A");
            var distB = result.Distribuicoes.First(d => d.Nome == "B");

            // B deve receber aproximadamente o dobro de A
            var totalA = distA.Ativos.Sum(a => a.Quantidade);
            var totalB = distB.Ativos.Sum(a => a.Quantidade);
            Assert.True(totalB > totalA, "Cliente B deve receber mais acoes que Cliente A");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
