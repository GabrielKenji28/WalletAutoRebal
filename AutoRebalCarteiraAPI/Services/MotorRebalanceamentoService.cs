using AutoRebalCarteira.Data;
using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteira.Data.Infrastructure.Kafka;
using AutoRebalCarteira.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoRebalCarteiraAPI.Services;

public interface IMotorRebalanceamentoService
{
    Task RebalancearPorMudancaCestaAsync(int cestaAntigaId, int cestaNovaId);
    Task RebalancearPorDesvioProporcaoAsync();
}

public class MotorRebalanceamentoService : IMotorRebalanceamentoService
{
    private readonly AppDbContext _db;
    private readonly CotahistParser _parser;
    private readonly IKafkaProducerService _kafka;
    private readonly IConfiguration _config;
    private readonly ILogger<MotorRebalanceamentoService> _logger;
    private const decimal LimiarDesvio = 5m; // 5 pontos percentuais

    public MotorRebalanceamentoService(
        AppDbContext db,
        CotahistParser parser,
        IKafkaProducerService kafka,
        IConfiguration config,
        ILogger<MotorRebalanceamentoService> logger)
    {
        _db = db;
        _parser = parser;
        _kafka = kafka;
        _config = config;
        _logger = logger;
    }

    public async Task RebalancearPorMudancaCestaAsync(int cestaAntigaId, int cestaNovaId)
    {
        var cestaAntiga = await _db.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstAsync(c => c.Id == cestaAntigaId);

        var cestaNova = await _db.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstAsync(c => c.Id == cestaNovaId);

        var tickersAntigos = cestaAntiga.Itens.Select(i => i.Ticker).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tickersNovos = cestaNova.Itens.Select(i => i.Ticker).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tickersRemovidos = tickersAntigos.Except(tickersNovos, StringComparer.OrdinalIgnoreCase).ToList();
        var tickersAdicionados = tickersNovos.Except(tickersAntigos, StringComparer.OrdinalIgnoreCase).ToList();

        var pastaCotacoes = _config["Cotahist:PastaCotacoes"] ?? "cotacoes";
        var todosTickers = tickersAntigos.Union(tickersNovos, StringComparer.OrdinalIgnoreCase).ToList();
        var cotacoes = _parser.ObterCotacoesFechamento(pastaCotacoes, todosTickers);

        var clientes = await _db.Clientes
            .Include(c => c.ContaGrafica)
                .ThenInclude(cg => cg.Custodia)
            .Where(c => c.Ativo)
            .ToListAsync();

        var agora = DateTime.UtcNow;

        foreach (var cliente in clientes)
        {
            decimal totalVendasMes = 0;
            var detalhesVenda = new List<DetalheVenda>();

            // Vender ativos removidos
            decimal valorObtidoVendas = 0;
            foreach (var tickerRemovido in tickersRemovidos)
            {
                var custodia = cliente.ContaGrafica.Custodia
                    .FirstOrDefault(c => c.Ticker.Equals(tickerRemovido, StringComparison.OrdinalIgnoreCase));

                if (custodia == null || custodia.Quantidade <= 0) continue;

                var cotacao = cotacoes.GetValueOrDefault(tickerRemovido, custodia.PrecoMedio);
                var valorVenda = custodia.Quantidade * cotacao;
                var lucro = custodia.Quantidade * (cotacao - custodia.PrecoMedio);

                valorObtidoVendas += valorVenda;
                totalVendasMes += valorVenda;

                detalhesVenda.Add(new DetalheVenda
                {
                    Ticker = tickerRemovido,
                    Quantidade = custodia.Quantidade,
                    PrecoVenda = cotacao,
                    PrecoMedio = custodia.PrecoMedio,
                    Lucro = lucro
                });

                custodia.Quantidade = 0;
            }

            // Rebalancear ativos que permaneceram mas mudaram de percentual
            var valorTotalCarteira = cliente.ContaGrafica.Custodia
                .Where(c => c.Quantidade > 0)
                .Sum(c => c.Quantidade * cotacoes.GetValueOrDefault(c.Ticker, c.PrecoMedio));

            valorTotalCarteira += valorObtidoVendas;

            foreach (var itemNovo in cestaNova.Itens)
            {
                var itemAntigo = cestaAntiga.Itens
                    .FirstOrDefault(i => i.Ticker.Equals(itemNovo.Ticker, StringComparison.OrdinalIgnoreCase));

                if (itemAntigo == null) continue; // Ativo novo, sera comprado depois
                if (tickersRemovidos.Contains(itemNovo.Ticker, StringComparer.OrdinalIgnoreCase)) continue;

                var custodia = cliente.ContaGrafica.Custodia
                    .FirstOrDefault(c => c.Ticker.Equals(itemNovo.Ticker, StringComparison.OrdinalIgnoreCase));

                if (custodia == null || custodia.Quantidade <= 0) continue;

                var cotacao = cotacoes.GetValueOrDefault(itemNovo.Ticker, custodia.PrecoMedio);
                var valorAtual = custodia.Quantidade * cotacao;
                var valorAlvo = valorTotalCarteira * (itemNovo.Percentual / 100m);
                var qtdAlvo = (int)Math.Truncate(valorAlvo / cotacao);

                if (custodia.Quantidade > qtdAlvo)
                {
                    // Vender excesso
                    var qtdVender = custodia.Quantidade - qtdAlvo;
                    var valorVenda = qtdVender * cotacao;
                    var lucro = qtdVender * (cotacao - custodia.PrecoMedio);

                    totalVendasMes += valorVenda;
                    valorObtidoVendas += valorVenda;

                    detalhesVenda.Add(new DetalheVenda
                    {
                        Ticker = itemNovo.Ticker,
                        Quantidade = qtdVender,
                        PrecoVenda = cotacao,
                        PrecoMedio = custodia.PrecoMedio,
                        Lucro = lucro
                    });

                    custodia.Quantidade = qtdAlvo;
                }
            }

            // Comprar novos ativos com o valor das vendas
            if (valorObtidoVendas > 0 && tickersAdicionados.Count > 0)
            {
                var percentualNovos = cestaNova.Itens
                    .Where(i => tickersAdicionados.Contains(i.Ticker, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                var somaPercentualNovos = percentualNovos.Sum(p => p.Percentual);

                foreach (var novoItem in percentualNovos)
                {
                    var proporcao = somaPercentualNovos > 0 ? novoItem.Percentual / somaPercentualNovos : 0;
                    var valorParaComprar = valorObtidoVendas * proporcao;
                    var cotacao = cotacoes.GetValueOrDefault(novoItem.Ticker, 0);

                    if (cotacao <= 0) continue;

                    var qtdComprar = (int)Math.Truncate(valorParaComprar / cotacao);
                    if (qtdComprar <= 0) continue;

                    var custExistente = cliente.ContaGrafica.Custodia
                        .FirstOrDefault(c => c.Ticker.Equals(novoItem.Ticker, StringComparison.OrdinalIgnoreCase));

                    if (custExistente != null)
                    {
                        var qtdAnterior = custExistente.Quantidade;
                        var pmAnterior = custExistente.PrecoMedio;
                        custExistente.PrecoMedio = qtdAnterior + qtdComprar > 0
                            ? (qtdAnterior * pmAnterior + qtdComprar * cotacao) / (qtdAnterior + qtdComprar)
                            : cotacao;
                        custExistente.Quantidade += qtdComprar;
                    }
                    else
                    {
                        cliente.ContaGrafica.Custodia.Add(new CustodiaItem
                        {
                            ContaGraficaId = cliente.ContaGrafica.Id,
                            Ticker = novoItem.Ticker,
                            Quantidade = qtdComprar,
                            PrecoMedio = cotacao
                        });
                    }
                }
            }

            // Calcular IR sobre vendas
            if (totalVendasMes > 20000m)
            {
                var lucroTotal = detalhesVenda.Sum(d => d.Lucro);
                var valorIR = lucroTotal > 0 ? Math.Round(lucroTotal * 0.20m, 2) : 0;

                await _kafka.PublicarIRVendaAsync(new IRVendaMessage
                {
                    ClienteId = cliente.Id,
                    Cpf = cliente.Cpf,
                    MesReferencia = agora.ToString("yyyy-MM"),
                    TotalVendasMes = totalVendasMes,
                    LucroLiquido = lucroTotal,
                    ValorIR = valorIR,
                    Detalhes = detalhesVenda,
                    DataCalculo = agora
                });
            }

            // Limpar custodia zerada
            var zerados = cliente.ContaGrafica.Custodia.Where(c => c.Quantidade <= 0 && c.Id > 0).ToList();
            foreach (var z in zerados)
                _db.CustodiaItens.Remove(z);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Rebalanceamento por mudanca de cesta concluido para {Count} clientes", clientes.Count);
    }

    public async Task RebalancearPorDesvioProporcaoAsync()
    {
        var cesta = await _db.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Ativa);

        if (cesta == null) return;

        var pastaCotacoes = _config["Cotahist:PastaCotacoes"] ?? "cotacoes";
        var tickers = cesta.Itens.Select(i => i.Ticker).ToList();
        var cotacoes = _parser.ObterCotacoesFechamento(pastaCotacoes, tickers);

        var clientes = await _db.Clientes
            .Include(c => c.ContaGrafica)
                .ThenInclude(cg => cg.Custodia)
            .Where(c => c.Ativo)
            .ToListAsync();

        var agora = DateTime.UtcNow;

        foreach (var cliente in clientes)
        {
            var custodia = cliente.ContaGrafica.Custodia.Where(c => c.Quantidade > 0).ToList();
            if (custodia.Count == 0) continue;

            var valorTotal = custodia.Sum(c =>
                c.Quantidade * cotacoes.GetValueOrDefault(c.Ticker, c.PrecoMedio));

            if (valorTotal <= 0) continue;

            bool precisaRebalancear = false;
            foreach (var item in cesta.Itens)
            {
                var cust = custodia.FirstOrDefault(c => c.Ticker.Equals(item.Ticker, StringComparison.OrdinalIgnoreCase));
                if (cust == null) continue;

                var cotacao = cotacoes.GetValueOrDefault(item.Ticker, cust.PrecoMedio);
                var proporcaoReal = (cust.Quantidade * cotacao / valorTotal) * 100m;
                var desvio = Math.Abs(proporcaoReal - item.Percentual);

                if (desvio > LimiarDesvio)
                {
                    precisaRebalancear = true;
                    break;
                }
            }

            if (!precisaRebalancear) continue;

            decimal totalVendasMes = 0;
            var detalhesVenda = new List<DetalheVenda>();

            // Vender sobre-alocados, comprar sub-alocados
            foreach (var item in cesta.Itens)
            {
                var cust = custodia.FirstOrDefault(c => c.Ticker.Equals(item.Ticker, StringComparison.OrdinalIgnoreCase));
                if (cust == null) continue;

                var cotacao = cotacoes.GetValueOrDefault(item.Ticker, cust.PrecoMedio);
                var valorAlvo = valorTotal * (item.Percentual / 100m);
                var qtdAlvo = (int)Math.Truncate(valorAlvo / cotacao);

                if (cust.Quantidade > qtdAlvo)
                {
                    // Vender excesso
                    var qtdVender = cust.Quantidade - qtdAlvo;
                    var valorVenda = qtdVender * cotacao;
                    var lucro = qtdVender * (cotacao - cust.PrecoMedio);

                    totalVendasMes += valorVenda;
                    detalhesVenda.Add(new DetalheVenda
                    {
                        Ticker = item.Ticker,
                        Quantidade = qtdVender,
                        PrecoVenda = cotacao,
                        PrecoMedio = cust.PrecoMedio,
                        Lucro = lucro
                    });

                    cust.Quantidade = qtdAlvo;
                }
                else if (cust.Quantidade < qtdAlvo)
                {
                    // Comprar deficit
                    var qtdComprar = qtdAlvo - cust.Quantidade;
                    var qtdAnterior = cust.Quantidade;
                    var pmAnterior = cust.PrecoMedio;
                    cust.PrecoMedio = qtdAnterior + qtdComprar > 0
                        ? (qtdAnterior * pmAnterior + qtdComprar * cotacao) / (qtdAnterior + qtdComprar)
                        : cotacao;
                    cust.Quantidade = qtdAlvo;
                }
            }

            // IR
            if (totalVendasMes > 20000m)
            {
                var lucroTotal = detalhesVenda.Sum(d => d.Lucro);
                var valorIR = lucroTotal > 0 ? Math.Round(lucroTotal * 0.20m, 2) : 0;

                await _kafka.PublicarIRVendaAsync(new IRVendaMessage
                {
                    ClienteId = cliente.Id,
                    Cpf = cliente.Cpf,
                    MesReferencia = agora.ToString("yyyy-MM"),
                    TotalVendasMes = totalVendasMes,
                    LucroLiquido = lucroTotal,
                    ValorIR = valorIR,
                    Detalhes = detalhesVenda,
                    DataCalculo = agora
                });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Rebalanceamento por desvio de proporcao concluido");
    }
}
