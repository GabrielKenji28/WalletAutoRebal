using AutoRebalCarteira.Data;
using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteira.Domain.Entities;
using AutoRebalCarteira.Domain.Exceptions;
using AutoRebalCarteiraAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoRebalCarteiraAPI.Services;

public interface ICestaService
{
    Task<CadastrarCestaResponse> CadastrarOuAlterarAsync(CadastrarCestaRequest request);
    Task<CestaAtualResponse> ObterCestaAtualAsync();
    Task<HistoricoCestasResponse> ObterHistoricoAsync();
}

public class CestaService : ICestaService
{
    private readonly AppDbContext _db;
    private readonly CotahistParser _parser;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _serviceProvider;

    public CestaService(AppDbContext db, CotahistParser parser, IConfiguration config, IServiceProvider serviceProvider)
    {
        _db = db;
        _parser = parser;
        _config = config;
        _serviceProvider = serviceProvider;
    }

    public async Task<CadastrarCestaResponse> CadastrarOuAlterarAsync(CadastrarCestaRequest request)
    {
        if (request.Itens.Count != 5)
            return new CadastrarCestaResponse
            {
                Failed = true,
                ErrorMessage = $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {request.Itens.Count}.",
                ErrorCode = "QUANTIDADE_ATIVOS_INVALIDA",
                StatusCode = 400
            };

        var somaPercentuais = request.Itens.Sum(i => i.Percentual);
        if (somaPercentuais != 100)
            return new CadastrarCestaResponse
            {
                Failed = true,
                ErrorMessage = $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {somaPercentuais}%.",
                ErrorCode = "PERCENTUAIS_INVALIDOS",
                StatusCode = 400
            };

        if (request.Itens.Any(i => i.Percentual <= 0))
            return new CadastrarCestaResponse
            {
                Failed = true,
                ErrorMessage = "Cada percentual deve ser maior que 0%.",
                ErrorCode = "PERCENTUAIS_INVALIDOS",
                StatusCode = 400
            };

        var cestaAnterior = await _db.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstOrDefaultAsync();

        var agora = DateTime.UtcNow;
        CestaDesativadaDto? cestaDesativada = null;
        List<string>? ativosRemovidos = null;
        List<string>? ativosAdicionados = null;
        bool rebalanceamentoDisparado = false;

        if (cestaAnterior != null)
        {
            cestaAnterior.Ativo = false;
            cestaAnterior.DataDesativacao = agora;

            var tickersAntigos = cestaAnterior.Itens.Select(i => i.Ticker).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var tickersNovos = request.Itens.Select(i => i.Ticker).ToHashSet(StringComparer.OrdinalIgnoreCase);

            ativosRemovidos = tickersAntigos.Except(tickersNovos, StringComparer.OrdinalIgnoreCase).ToList();
            ativosAdicionados = tickersNovos.Except(tickersAntigos, StringComparer.OrdinalIgnoreCase).ToList();

            cestaDesativada = new CestaDesativadaDto
            {
                CestaId = cestaAnterior.Id,
                Nome = cestaAnterior.Nome,
                DataDesativacao = agora
            };

            rebalanceamentoDisparado = true;
        }

        var novaCesta = new CestaRecomendacao
        {
            Nome = request.Nome,
            Ativo = true,
            DataCriacao = agora,
            Itens = request.Itens.Select(i => new CestaItem
            {
                Ticker = i.Ticker.ToUpperInvariant(),
                Percentual = i.Percentual
            }).ToList()
        };

        _db.CestasRecomendacao.Add(novaCesta);
        await _db.SaveChangesAsync();

        if (rebalanceamentoDisparado && cestaAnterior != null)
        {
            _ = Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var rebalService = scope.ServiceProvider.GetRequiredService<IMotorRebalanceamentoService>();
                await rebalService.RebalancearPorMudancaCestaAsync(cestaAnterior.Id, novaCesta.Id);
            });
        }

        var totalClientes = rebalanceamentoDisparado
            ? await _db.Clientes.CountAsync(c => c.Ativo)
            : 0;

        return new CadastrarCestaResponse
        {
            CestaId = novaCesta.Id,
            Nome = novaCesta.Nome,
            Ativa = true,
            DataCriacao = novaCesta.DataCriacao,
            Itens = novaCesta.Itens.Select(i => new CestaItemDto
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual
            }).ToList(),
            CestaAnteriorDesativada = cestaDesativada,
            RebalanceamentoDisparado = rebalanceamentoDisparado,
            AtivosRemovidos = ativosRemovidos,
            AtivosAdicionados = ativosAdicionados,
            Mensagem = rebalanceamentoDisparado
                ? $"Cesta atualizada. Rebalanceamento disparado para {totalClientes} clientes ativos."
                : "Primeira cesta cadastrada com sucesso."
        };
    }

    public async Task<CestaAtualResponse> ObterCestaAtualAsync()
    {
        var cesta = await _db.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstOrDefaultAsync();

        if (cesta == null)
            return new CestaAtualResponse { Failed = true, ErrorMessage = "Nenhuma cesta ativa encontrada.", ErrorCode = "CESTA_NAO_ENCONTRADA", StatusCode = 404 };

        var pastaCotacoes = _config["Cotahist:PastaCotacoes"] ?? "cotacoes";
        var tickers = cesta.Itens.Select(i => i.Ticker).ToList();
        var cotacoes = _parser.ObterCotacoesFechamento(pastaCotacoes, tickers);

        return new CestaAtualResponse
        {
            CestaId = cesta.Id,
            Nome = cesta.Nome,
            Ativa = true,
            DataCriacao = cesta.DataCriacao,
            Itens = cesta.Itens.Select(i => new CestaItemCotacaoDto
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual,
                CotacaoAtual = cotacoes.GetValueOrDefault(i.Ticker, 0)
            }).ToList()
        };
    }

    public async Task<HistoricoCestasResponse> ObterHistoricoAsync()
    {
        var cestas = await _db.CestasRecomendacao
            .IgnoreQueryFilters()
            .Include(c => c.Itens)
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();

        return new HistoricoCestasResponse
        {
            Cestas = cestas.Select(c => new CestaHistoricoDto
            {
                CestaId = c.Id,
                Nome = c.Nome,
                Ativa = c.Ativo,
                DataCriacao = c.DataCriacao,
                DataDesativacao = c.DataDesativacao,
                Itens = c.Itens.Select(i => new CestaItemDto
                {
                    Ticker = i.Ticker,
                    Percentual = i.Percentual
                }).ToList()
            }).ToList()
        };
    }
}
