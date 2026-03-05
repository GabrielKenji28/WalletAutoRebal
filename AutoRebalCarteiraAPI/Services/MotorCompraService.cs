using AutoRebalCarteira.Data;
using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteira.Data.Infrastructure.Kafka;
using AutoRebalCarteira.Data.Interfaces;
using AutoRebalCarteira.Domain.Entities;
using AutoRebalCarteira.Domain.Exceptions;
using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoRebalCarteiraAPI.Services;

public class MotorCompraService : IMotorCompraService
{
    private readonly AppDbContext _db;
    private readonly CotahistParser _parser;
    private readonly IKafkaProducerService _kafka;
    private readonly IConfiguration _config;
    private readonly ILogger<MotorCompraService> _logger;

    public MotorCompraService(
        AppDbContext db,
        CotahistParser parser,
        IKafkaProducerService kafka,
        IConfiguration config,
        ILogger<MotorCompraService> logger)
    {
        _db = db;
        _parser = parser;
        _kafka = kafka;
        _config = config;
        _logger = logger;
    }

    public async Task<ExecutarCompraResponse> ExecutarCompraAsync(DateOnly dataReferencia)
    {
        var dataExecucao = AjustarParaDiaUtil(dataReferencia);

        // Verificar se ja foi executada para esta data
        if (await _db.OrdensCompra.AnyAsync(o => o.DataExecucao.Date == dataExecucao.ToDateTime(TimeOnly.MinValue).Date))
            return new ExecutarCompraResponse { Failed = true, ErrorMessage = "Compra ja foi executada para esta data.", ErrorCode = "COMPRA_JA_EXECUTADA", StatusCode = 409 };

        // Obter cesta vigente
        var cesta = await _db.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstOrDefaultAsync();

        if (cesta == null)
            return new ExecutarCompraResponse { Failed = true, ErrorMessage = "Nenhuma cesta ativa encontrada.", ErrorCode = "CESTA_NAO_ENCONTRADA", StatusCode = 404 };

        // Obter clientes ativos
        var clientes = await _db.Clientes
            .Include(c => c.ContaGrafica)
                .ThenInclude(cg => cg.Custodia)
            .Where(c => c.Ativo)
            .ToListAsync();

        if (clientes.Count == 0)
            return new ExecutarCompraResponse { Failed = true, ErrorMessage = "Nenhum cliente ativo encontrado.", ErrorCode = "SEM_CLIENTES_ATIVOS", StatusCode = 400 };

        // 1. Agrupamento: 1/3 do valor mensal de cada cliente
        var aportesPorCliente = clientes.ToDictionary(c => c, c => Math.Round(c.ValorMensal / 3m, 2));
        var totalConsolidado = aportesPorCliente.Values.Sum();

        // 2. Obter cotacoes
        var pastaCotacoes = _config["Cotahist:PastaCotacoes"] ?? "cotacoes";
        var tickers = cesta.Itens.Select(i => i.Ticker).ToList();
        var cotacoes = _parser.ObterCotacoesFechamento(pastaCotacoes, tickers);

        if (cotacoes.Count == 0)
            return new ExecutarCompraResponse { Failed = true, ErrorMessage = "Arquivo COTAHIST nao encontrado ou sem cotacoes.", ErrorCode = "COTACAO_NAO_ENCONTRADA", StatusCode = 404 };

        // 3. Obter conta master e custodia
        var contaMaster = await _db.ContasGraficas
            .Include(c => c.Custodia)
            .FirstAsync(c => c.Tipo == "MASTER");

        // 4. Calcular quantidade a comprar por ativo
        var ordensCompra = new List<OrdemCompraItem>();
        var quantidadeTotalPorTicker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cestaItem in cesta.Itens)
        {
            var valorParaAtivo = totalConsolidado * (cestaItem.Percentual / 100m);
            var cotacao = cotacoes.GetValueOrDefault(cestaItem.Ticker, 0);
            if (cotacao <= 0) continue;

            var quantidadeNecessaria = (int)Math.Truncate(valorParaAtivo / cotacao);

            // Descontar saldo da custodia master
            var saldoMaster = contaMaster.Custodia
                .FirstOrDefault(c => c.Ticker.Equals(cestaItem.Ticker, StringComparison.OrdinalIgnoreCase));
            var saldoExistente = saldoMaster?.Quantidade ?? 0;

            var quantidadeAComprar = Math.Max(0, quantidadeNecessaria - saldoExistente);
            var quantidadeTotalDisponivel = quantidadeAComprar + saldoExistente;

            // Separar lote padrao vs fracionario
            var lotePadrao = (quantidadeAComprar / 100) * 100;
            var fracionario = quantidadeAComprar % 100;

            if (quantidadeAComprar > 0)
            {
                ordensCompra.Add(new OrdemCompraItem
                {
                    Ticker = cestaItem.Ticker,
                    QuantidadeLotePadrao = lotePadrao,
                    QuantidadeFracionario = fracionario,
                    QuantidadeTotal = quantidadeAComprar,
                    PrecoUnitario = cotacao,
                    ValorTotal = quantidadeAComprar * cotacao
                });
            }

            quantidadeTotalPorTicker[cestaItem.Ticker] = quantidadeTotalDisponivel;
        }

        // 5. Registrar ordem de compra
        var agora = DateTime.UtcNow;
        var ordemCompra = new OrdemCompra
        {
            DataExecucao = dataExecucao.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ValorTotalConsolidado = totalConsolidado,
            TotalClientes = clientes.Count,
            Itens = ordensCompra
        };
        _db.OrdensCompra.Add(ordemCompra);

        // Atualizar custodia master: adicionar compras
        foreach (var item in ordensCompra)
        {
            var custMaster = contaMaster.Custodia
                .FirstOrDefault(c => c.Ticker.Equals(item.Ticker, StringComparison.OrdinalIgnoreCase));

            if (custMaster != null)
            {
                // Atualizar preco medio
                var qtdAnterior = custMaster.Quantidade;
                var pmAnterior = custMaster.PrecoMedio;
                var qtdNova = item.QuantidadeTotal;
                var precoNovo = item.PrecoUnitario;
                custMaster.PrecoMedio = qtdAnterior + qtdNova > 0
                    ? (qtdAnterior * pmAnterior + qtdNova * precoNovo) / (qtdAnterior + qtdNova)
                    : precoNovo;
                custMaster.Quantidade += qtdNova;
            }
            else
            {
                contaMaster.Custodia.Add(new CustodiaItem
                {
                    Ticker = item.Ticker,
                    Quantidade = item.QuantidadeTotal,
                    PrecoMedio = item.PrecoUnitario
                });
            }
        }

        await _db.SaveChangesAsync();

        // Recarregar custodia master
        await _db.Entry(contaMaster).Collection(c => c.Custodia).LoadAsync();

        // 6. Distribuicao para contas filhotes
        var distribuicoes = new List<Distribuicao>();
        var distribuicoesDto = new List<DistribuicaoDto>();
        int eventosIR = 0;

        foreach (var cliente in clientes)
        {
            var aporteCliente = aportesPorCliente[cliente];
            var proporcao = totalConsolidado > 0 ? aporteCliente / totalConsolidado : 0;

            var distribuicao = new Distribuicao
            {
                OrdemCompraId = ordemCompra.Id,
                ClienteId = cliente.Id,
                ValorAporte = aporteCliente,
                DataDistribuicao = agora,
                Itens = []
            };

            var ativosDistribuidos = new List<AtivoDistribuidoDto>();

            foreach (var ticker in quantidadeTotalPorTicker.Keys)
            {
                var totalDisponivel = quantidadeTotalPorTicker[ticker];
                var qtdCliente = (int)Math.Truncate(totalDisponivel * proporcao);

                if (qtdCliente <= 0) continue;

                var cotacao = cotacoes.GetValueOrDefault(ticker, 0);

                distribuicao.Itens.Add(new DistribuicaoItem
                {
                    Ticker = ticker,
                    Quantidade = qtdCliente,
                    PrecoUnitario = cotacao
                });

                // Atualizar custodia filhote
                var custFilhote = cliente.ContaGrafica.Custodia
                    .FirstOrDefault(c => c.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase));

                if (custFilhote != null)
                {
                    var qtdAnterior = custFilhote.Quantidade;
                    var pmAnterior = custFilhote.PrecoMedio;
                    custFilhote.PrecoMedio = (qtdAnterior * pmAnterior + qtdCliente * cotacao) / (qtdAnterior + qtdCliente);
                    custFilhote.Quantidade += qtdCliente;
                }
                else
                {
                    cliente.ContaGrafica.Custodia.Add(new CustodiaItem
                    {
                        Ticker = ticker,
                        Quantidade = qtdCliente,
                        PrecoMedio = cotacao
                    });
                }

                // Descontar da custodia master
                var custMaster = contaMaster.Custodia
                    .First(c => c.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase));
                custMaster.Quantidade -= qtdCliente;

                // IR Dedo-duro
                var valorOperacao = qtdCliente * cotacao;
                var valorIR = Math.Round(valorOperacao * 0.00005m, 2);

                await _kafka.PublicarIRDedoDuroAsync(new IRDedoDuroMessage
                {
                    ClienteId = cliente.Id,
                    Cpf = cliente.Cpf,
                    Ticker = ticker,
                    TipoOperacao = "COMPRA",
                    Quantidade = qtdCliente,
                    PrecoUnitario = cotacao,
                    ValorOperacao = valorOperacao,
                    ValorIR = valorIR,
                    DataOperacao = agora
                });
                eventosIR++;

                ativosDistribuidos.Add(new AtivoDistribuidoDto
                {
                    Ticker = ticker,
                    Quantidade = qtdCliente
                });
            }

            distribuicoes.Add(distribuicao);
            distribuicoesDto.Add(new DistribuicaoDto
            {
                ClienteId = cliente.Id,
                Nome = cliente.Nome,
                ValorAporte = aporteCliente,
                Ativos = ativosDistribuidos
            });
        }

        _db.Distribuicoes.AddRange(distribuicoes);

        // Soft-delete itens zerados da custodia master
        var itensZerados = contaMaster.Custodia.Where(c => c.Quantidade <= 0).ToList();
        foreach (var item in itensZerados)
        {
            item.Ativo = false;
        }

        await _db.SaveChangesAsync();

        // 7. Preparar residuos
        await _db.Entry(contaMaster).Collection(c => c.Custodia).LoadAsync();
        var residuos = contaMaster.Custodia
            .Where(c => c.Quantidade > 0)
            .Select(c => new ResiduoDto { Ticker = c.Ticker, Quantidade = c.Quantidade })
            .ToList();

        return new ExecutarCompraResponse
        {
            DataExecucao = agora,
            TotalClientes = clientes.Count,
            TotalConsolidado = totalConsolidado,
            OrdensCompra = ordensCompra.Select(o => new OrdemCompraDto
            {
                Ticker = o.Ticker,
                QuantidadeTotal = o.QuantidadeTotal,
                PrecoUnitario = o.PrecoUnitario,
                ValorTotal = o.ValorTotal,
                Detalhes = BuildDetalhesCompra(o)
            }).ToList(),
            Distribuicoes = distribuicoesDto,
            ResiduosCustMaster = residuos,
            EventosIRPublicados = eventosIR,
            Mensagem = $"Compra programada executada com sucesso para {clientes.Count} clientes."
        };
    }
    //TODO: Não gostei desse método
    private static DateOnly AjustarParaDiaUtil(DateOnly data)
    {
        while (data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday)
        {
            data = data.AddDays(1);
        }
        return data;
    }

    private static List<DetalheCompraDto> BuildDetalhesCompra(OrdemCompraItem item)
    {
        var detalhes = new List<DetalheCompraDto>();
        if (item.QuantidadeLotePadrao > 0)
        {
            detalhes.Add(new DetalheCompraDto
            {
                Tipo = "LOTE_PADRAO",
                Ticker = item.Ticker,
                Quantidade = item.QuantidadeLotePadrao
            });
        }
        if (item.QuantidadeFracionario > 0)
        {
            detalhes.Add(new DetalheCompraDto
            {
                Tipo = "FRACIONARIO",
                Ticker = $"{item.Ticker}F",
                Quantidade = item.QuantidadeFracionario
            });
        }
        return detalhes;
    }
}
