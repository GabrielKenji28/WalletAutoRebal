using AutoRebalCarteira.Data;
using AutoRebalCarteira.Data.Infrastructure.Cotahist;
using AutoRebalCarteira.Domain.Entities;
using AutoRebalCarteira.Domain.Exceptions;
using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoRebalCarteiraAPI.Services;

public class ClienteService : IClienteService
{
    private readonly AppDbContext _db;
    private readonly CotahistParser _parser;
    private readonly IConfiguration _config;

    public ClienteService(AppDbContext db, CotahistParser parser, IConfiguration config)
    {
        _db = db;
        _parser = parser;
        _config = config;
    }

    public async Task<AdesaoResponse> AderirAsync(AdesaoRequest request)
    {
        if (await _db.Clientes.IgnoreQueryFilters().AnyAsync(c => c.Cpf == request.Cpf))
            return new AdesaoResponse { Failed = true, ErrorMessage = "CPF ja cadastrado no sistema.", ErrorCode = "CLIENTE_CPF_DUPLICADO", StatusCode = 400 };

        if (request.ValorMensal < 100)
            return new AdesaoResponse { Failed = true, ErrorMessage = "O valor mensal minimo e de R$ 100,00.", ErrorCode = "VALOR_MENSAL_INVALIDO", StatusCode = 400 };

        var ultimaConta = await _db.ContasGraficas
            .Where(c => c.Tipo == "FILHOTE")
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();

        var proximoNumero = ultimaConta != null
            ? int.Parse(ultimaConta.NumeroConta.Replace("FLH-", "")) + 1
            : 1;

        var agora = DateTime.UtcNow;

        var cliente = new Cliente
        {
            Nome = request.Nome,
            Cpf = request.Cpf,
            Email = request.Email,
            ValorMensal = request.ValorMensal,
            Ativo = true,
            DataAdesao = agora,
            ContaGrafica = new ContaGrafica
            {
                NumeroConta = $"FLH-{proximoNumero:D6}",
                Tipo = "FILHOTE",
                DataCriacao = agora
            }
        };

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();

        return new AdesaoResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            Cpf = cliente.Cpf,
            Email = cliente.Email,
            ValorMensal = cliente.ValorMensal,
            Ativo = cliente.Ativo,
            DataAdesao = cliente.DataAdesao,
            ContaGrafica = new ContaGraficaDto
            {
                Id = cliente.ContaGrafica.Id,
                NumeroConta = cliente.ContaGrafica.NumeroConta,
                Tipo = cliente.ContaGrafica.Tipo,
                DataCriacao = cliente.ContaGrafica.DataCriacao
            }
        };
    }

    public async Task<SaidaResponse> SairAsync(int clienteId)
    {
        var cliente = await _db.Clientes.FindAsync(clienteId);
        if (cliente == null)
            return new SaidaResponse { Failed = true, ErrorMessage = "Cliente nao encontrado.", ErrorCode = "CLIENTE_NAO_ENCONTRADO", StatusCode = 404 };

        if (!cliente.Ativo)
            return new SaidaResponse { Failed = true, ErrorMessage = "Cliente ja havia saido do produto.", ErrorCode = "CLIENTE_JA_INATIVO", StatusCode = 400 };

        cliente.Ativo = false;
        cliente.DataSaida = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new SaidaResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            Ativo = false,
            DataSaida = cliente.DataSaida,
            Mensagem = "Adesao encerrada. Sua posicao em custodia foi mantida."
        };
    }

    public async Task<AlterarValorMensalResponse> AlterarValorMensalAsync(int clienteId, AlterarValorMensalRequest request)
    {
        var cliente = await _db.Clientes.FindAsync(clienteId);
        if (cliente == null)
            return new AlterarValorMensalResponse { Failed = true, ErrorMessage = "Cliente nao encontrado.", ErrorCode = "CLIENTE_NAO_ENCONTRADO", StatusCode = 404 };

        if (request.NovoValorMensal < 100)
            return new AlterarValorMensalResponse { Failed = true, ErrorMessage = "O valor mensal minimo e de R$ 100,00.", ErrorCode = "VALOR_MENSAL_INVALIDO", StatusCode = 400 };

        var valorAnterior = cliente.ValorMensal;
        var agora = DateTime.UtcNow;

        _db.HistoricoValoresMensais.Add(new HistoricoValorMensal
        {
            ClienteId = clienteId,
            ValorAnterior = valorAnterior,
            ValorNovo = request.NovoValorMensal,
            DataAlteracao = agora
        });

        cliente.ValorMensal = request.NovoValorMensal;
        await _db.SaveChangesAsync();

        return new AlterarValorMensalResponse
        {
            ClienteId = clienteId,
            ValorMensalAnterior = valorAnterior,
            ValorMensalNovo = request.NovoValorMensal,
            DataAlteracao = agora,
            Mensagem = "Valor mensal atualizado. O novo valor sera considerado a partir da proxima data de compra."
        };
    }

    public async Task<CarteiraResponse> ConsultarCarteiraAsync(int clienteId)
    {
        var cliente = await _db.Clientes
            .Include(c => c.ContaGrafica)
                .ThenInclude(cg => cg.Custodia)
            .FirstOrDefaultAsync(c => c.Id == clienteId);

        if (cliente == null)
            return new CarteiraResponse { Failed = true, ErrorMessage = "Cliente nao encontrado.", ErrorCode = "CLIENTE_NAO_ENCONTRADO", StatusCode = 404 };

        var custodia = cliente.ContaGrafica.Custodia.Where(c => c.Quantidade > 0).ToList();
        var tickers = custodia.Select(c => c.Ticker).ToList();

        var pastaCotacoes = _config["Cotahist:PastaCotacoes"] ?? "cotacoes";
        var cotacoes = _parser.ObterCotacoesFechamento(pastaCotacoes, tickers);

        var ativos = new List<AtivoCarteiraDto>();
        decimal valorTotalCarteira = 0;
        decimal valorTotalInvestido = 0;

        foreach (var item in custodia)
        {
            var cotacaoAtual = cotacoes.GetValueOrDefault(item.Ticker, item.PrecoMedio);
            var valorAtual = item.Quantidade * cotacaoAtual;
            var valorInvestido = item.Quantidade * item.PrecoMedio;
            var pl = valorAtual - valorInvestido;
            var plPercentual = valorInvestido > 0 ? (pl / valorInvestido) * 100 : 0;

            valorTotalCarteira += valorAtual;
            valorTotalInvestido += valorInvestido;

            ativos.Add(new AtivoCarteiraDto
            {
                Ticker = item.Ticker,
                Quantidade = item.Quantidade,
                PrecoMedio = Math.Round(item.PrecoMedio, 2),
                CotacaoAtual = Math.Round(cotacaoAtual, 2),
                ValorAtual = Math.Round(valorAtual, 2),
                Pl = Math.Round(pl, 2),
                PlPercentual = Math.Round(plPercentual, 2)
            });
        }

        // Composicao percentual
        foreach (var ativo in ativos)
        {
            ativo.ComposicaoCarteira = valorTotalCarteira > 0
                ? Math.Round((ativo.ValorAtual / valorTotalCarteira) * 100, 2)
                : 0;
        }

        var plTotal = valorTotalCarteira - valorTotalInvestido;
        var rentabilidade = valorTotalInvestido > 0
            ? Math.Round((plTotal / valorTotalInvestido) * 100, 2)
            : 0;

        return new CarteiraResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            ContaGrafica = cliente.ContaGrafica.NumeroConta,
            DataConsulta = DateTime.UtcNow,
            Resumo = new ResumoCarteira
            {
                ValorTotalInvestido = Math.Round(valorTotalInvestido, 2),
                ValorAtualCarteira = Math.Round(valorTotalCarteira, 2),
                PlTotal = Math.Round(plTotal, 2),
                RentabilidadePercentual = rentabilidade
            },
            Ativos = ativos
        };
    }

    public async Task<RentabilidadeResponse> ConsultarRentabilidadeAsync(int clienteId)
    {
        var carteira = await ConsultarCarteiraAsync(clienteId);
        if (carteira.Failed)
            return new RentabilidadeResponse { Failed = true, ErrorMessage = carteira.ErrorMessage, ErrorCode = carteira.ErrorCode, StatusCode = carteira.StatusCode };

        var distribuicoes = await _db.Distribuicoes
            .Where(d => d.ClienteId == clienteId)
            .OrderBy(d => d.DataDistribuicao)
            .ToListAsync();

        var historicoAportes = new List<HistoricoAporteDto>();
        int parcelaCount = 0;
        foreach (var dist in distribuicoes)
        {
            parcelaCount++;
            var parcelaNoCiclo = ((parcelaCount - 1) % 3) + 1;
            historicoAportes.Add(new HistoricoAporteDto
            {
                Data = dist.DataDistribuicao.ToString("yyyy-MM-dd"),
                Valor = dist.ValorAporte,
                Parcela = $"{parcelaNoCiclo}/3"
            });
        }

        decimal acumuladoInvestido = 0;
        var evolucao = new List<EvolucaoCarteiraDto>();
        foreach (var dist in distribuicoes)
        {
            acumuladoInvestido += dist.ValorAporte;
            evolucao.Add(new EvolucaoCarteiraDto
            {
                Data = dist.DataDistribuicao.ToString("yyyy-MM-dd"),
                ValorInvestido = Math.Round(acumuladoInvestido, 2),
                ValorCarteira = Math.Round(acumuladoInvestido, 2),
                Rentabilidade = 0
            });
        }

        if (evolucao.Count > 0)
        {
            var ultimo = evolucao[^1];
            ultimo.ValorCarteira = carteira.Resumo.ValorAtualCarteira;
            ultimo.Rentabilidade = carteira.Resumo.RentabilidadePercentual;
        }

        return new RentabilidadeResponse
        {
            ClienteId = carteira.ClienteId,
            Nome = carteira.Nome,
            DataConsulta = DateTime.UtcNow,
            Rentabilidade = carteira.Resumo,
            HistoricoAportes = historicoAportes,
            EvolucaoCarteira = evolucao
        };
    }
}
