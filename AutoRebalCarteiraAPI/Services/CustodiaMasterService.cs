using AutoRebalCarteira.Data;
using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoRebalCarteiraAPI.Services;

public class CustodiaMasterService : ICustodiaMasterService
{
    private readonly AppDbContext _db;
    private readonly CotahistParser _parser;
    private readonly IConfiguration _config;

    public CustodiaMasterService(AppDbContext db, CotahistParser parser, IConfiguration config)
    {
        _db = db;
        _parser = parser;
        _config = config;
    }

    public async Task<CustodiaMasterResponse> ObterCustodiaMasterAsync()
    {
        var contaMaster = await _db.ContasGraficas
            .Include(c => c.Custodia)
            .FirstAsync(c => c.Tipo == "MASTER");

        var itens = contaMaster.Custodia.Where(c => c.Quantidade > 0).ToList();

        var pastaCotacoes = _config["Cotahist:PastaCotacoes"] ?? "cotacoes";
        var tickers = itens.Select(i => i.Ticker).ToList();
        var cotacoes = _parser.ObterCotacoesFechamento(pastaCotacoes, tickers);

        var custodiaDto = itens.Select(i =>
        {
            var cotacao = cotacoes.GetValueOrDefault(i.Ticker, i.PrecoMedio);
            return new CustodiaItemDto
            {
                Ticker = i.Ticker,
                Quantidade = i.Quantidade,
                PrecoMedio = Math.Round(i.PrecoMedio, 2),
                ValorAtual = Math.Round(i.Quantidade * cotacao, 2)
            };
        }).ToList();

        return new CustodiaMasterResponse
        {
            ContaMaster = new ContaGraficaDto
            {
                Id = contaMaster.Id,
                NumeroConta = contaMaster.NumeroConta,
                Tipo = contaMaster.Tipo,
                DataCriacao = contaMaster.DataCriacao
            },
            Custodia = custodiaDto,
            ValorTotalResiduo = custodiaDto.Sum(c => c.ValorAtual)
        };
    }
}
