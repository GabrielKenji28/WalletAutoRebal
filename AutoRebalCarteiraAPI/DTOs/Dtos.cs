using System.Text.Json.Serialization;

namespace AutoRebalCarteiraAPI.DTOs;

public class BaseResponse
{
    public bool Failed { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    [JsonIgnore]
    public int StatusCode { get; set; } = 200;
}

// === CLIENTE ===

public class AdesaoRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
}

public class AdesaoResponse : BaseResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataAdesao { get; set; }
    public ContaGraficaDto ContaGrafica { get; set; } = null!;
}

public class ContaGraficaDto
{
    public int Id { get; set; }
    public string NumeroConta { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}

public class SaidaResponse : BaseResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime? DataSaida { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class AlterarValorMensalRequest
{
    public decimal NovoValorMensal { get; set; }
}

public class AlterarValorMensalResponse : BaseResponse
{
    public int ClienteId { get; set; }
    public decimal ValorMensalAnterior { get; set; }
    public decimal ValorMensalNovo { get; set; }
    public DateTime DataAlteracao { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class CarteiraResponse : BaseResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string ContaGrafica { get; set; } = string.Empty;
    public DateTime DataConsulta { get; set; }
    public ResumoCarteira Resumo { get; set; } = null!;
    public List<AtivoCarteiraDto> Ativos { get; set; } = [];
}

public class ResumoCarteira
{
    public decimal ValorTotalInvestido { get; set; }
    public decimal ValorAtualCarteira { get; set; }
    public decimal PlTotal { get; set; }
    public decimal RentabilidadePercentual { get; set; }
}

public class AtivoCarteiraDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal CotacaoAtual { get; set; }
    public decimal ValorAtual { get; set; }
    public decimal Pl { get; set; }
    public decimal PlPercentual { get; set; }
    public decimal ComposicaoCarteira { get; set; }
}

public class RentabilidadeResponse : BaseResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataConsulta { get; set; }
    public ResumoCarteira Rentabilidade { get; set; } = null!;
    public List<HistoricoAporteDto> HistoricoAportes { get; set; } = [];
    public List<EvolucaoCarteiraDto> EvolucaoCarteira { get; set; } = [];
}

public class HistoricoAporteDto
{
    public string Data { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Parcela { get; set; } = string.Empty;
}

public class EvolucaoCarteiraDto
{
    public string Data { get; set; } = string.Empty;
    public decimal ValorCarteira { get; set; }
    public decimal ValorInvestido { get; set; }
    public decimal Rentabilidade { get; set; }
}

// === ADMIN / CESTA ===

public class CadastrarCestaRequest
{
    public string Nome { get; set; } = string.Empty;
    public List<CestaItemDto> Itens { get; set; } = [];
}

public class CestaItemDto
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
}

public class CadastrarCestaResponse : BaseResponse
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public List<CestaItemDto> Itens { get; set; } = [];
    public CestaDesativadaDto? CestaAnteriorDesativada { get; set; }
    public bool RebalanceamentoDisparado { get; set; }
    public List<string>? AtivosRemovidos { get; set; }
    public List<string>? AtivosAdicionados { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class CestaDesativadaDto
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime? DataDesativacao { get; set; }
}

public class CestaAtualResponse : BaseResponse
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public List<CestaItemCotacaoDto> Itens { get; set; } = [];
}

public class CestaItemCotacaoDto
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
    public decimal CotacaoAtual { get; set; }
}

public class HistoricoCestasResponse : BaseResponse
{
    public List<CestaHistoricoDto> Cestas { get; set; } = [];
}

public class CestaHistoricoDto
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataDesativacao { get; set; }
    public List<CestaItemDto> Itens { get; set; } = [];
}

// === CUSTODIA MASTER ===

public class CustodiaMasterResponse : BaseResponse
{
    public ContaGraficaDto ContaMaster { get; set; } = null!;
    public List<CustodiaItemDto> Custodia { get; set; } = [];
    public decimal ValorTotalResiduo { get; set; }
}

public class CustodiaItemDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal ValorAtual { get; set; }
}

// === MOTOR DE COMPRA ===

public class ExecutarCompraRequest
{
    public string DataReferencia { get; set; } = string.Empty;
}

public class ExecutarCompraResponse : BaseResponse
{
    public DateTime DataExecucao { get; set; }
    public int TotalClientes { get; set; }
    public decimal TotalConsolidado { get; set; }
    public List<OrdemCompraDto> OrdensCompra { get; set; } = [];
    public List<DistribuicaoDto> Distribuicoes { get; set; } = [];
    public List<ResiduoDto> ResiduosCustMaster { get; set; } = [];
    public int EventosIRPublicados { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class OrdemCompraDto
{
    public string Ticker { get; set; } = string.Empty;
    public int QuantidadeTotal { get; set; }
    public List<DetalheCompraDto> Detalhes { get; set; } = [];
    public decimal PrecoUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}

public class DetalheCompraDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

public class DistribuicaoDto
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal ValorAporte { get; set; }
    public List<AtivoDistribuidoDto> Ativos { get; set; } = [];
}

public class AtivoDistribuidoDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

public class ResiduoDto
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}

// === ERRO ===

public class ErrorResponse
{
    public string Erro { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
}
